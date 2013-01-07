// -----------------------------------------------------------------------
//  <copyright file="MediaParser.cs" company="Henric Jungheim">
//  Copyright (c) 2012.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012 Henric Jungheim <software@henric.org>
// 
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using SM.Media.Pes;
using SM.Media.Utility;
using SM.TsParser;

namespace SM.Media
{
    public sealed class MediaParser : IDisposable
    {
        #region Delegates

        public delegate MediaStream PacketHandlerFactory(uint pid, TsStreamType streamType, IStreamSource streamBuffer, Action<TsPesPacket> nextHandler);

        #endregion

        readonly IBufferingManager _bufferingManager;
        readonly Action<IMediaParserMediaStream> _mediaParserStreamHandler;
        readonly List<IMediaParserMediaStream> _mediaStreams = new List<IMediaParserMediaStream>();
        readonly object _mediaStreamsLock = new object();
        readonly PesHandlers _pesHandlers;
        readonly TsDecoder _tsDecoder;
        TimeSpan? _timestampOffset;

        public TimeSpan StartPosition { get; set; }

        public MediaParser(IBufferingManager bufferingManager, Action<IMediaParserMediaStream> mediaParserStreamHandler, Func<uint, TsStreamType, Action<TsPesPacket>> handlerFactory = null)
            : this(mediaParserStreamHandler, handlerFactory)
        {
            if (null == bufferingManager)
                throw new ArgumentNullException("bufferingManager");

            _bufferingManager = bufferingManager;
        }

        public MediaParser(Action<IMediaParserMediaStream> mediaParserStreamHandler, Func<uint, TsStreamType, Action<TsPesPacket>> handlerFactory = null)
        {
            if (null == handlerFactory)
            {
                var phf = new PesHandlerFactory();

                RegisterHandlers(phf, TsMediaHandlerFactories.DefaultFactories);

                handlerFactory = phf.CreateHandler;
            }

            _pesHandlers = new PesHandlers(handlerFactory);

            _mediaParserStreamHandler = mediaParserStreamHandler;

            //var packetCount = 0;

            _tsDecoder = new TsDecoder(new BufferPool(5 * 64 * 1024, 2), _pesHandlers.GetPesHandler)
                         {
                             //PacketMonitor = p => Debug.WriteLine("{0}: {1}", ++packetCount, p)
                         };
        }

        public IMediaParserMediaStream[] MediaStreams
        {
            get
            {
                lock (_mediaStreamsLock)
                {
                    return _mediaStreams.ToArray();
                }
            }
        }

        public TsDecoder Decoder
        {
            get { return _tsDecoder; }
        }

        public TimeSpan BufferPosition
        {
            get { return _bufferingManager.BufferPosition; }
        }

        #region IDisposable Members

        /// <summary>
        ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            using (_pesHandlers)
            { }

            CleanupStreams();

            using (_tsDecoder)
            { }
        }

        #endregion

        void CleanupStreams()
        {
            IMediaParserMediaStream[] oldStreams;

            lock (_mediaStreamsLock)
            {
                oldStreams = _mediaStreams.ToArray();
                _mediaStreams.Clear();
            }

            foreach (var ms in oldStreams)
            {
                using (ms)
                { }
            }
        }

        public void Initialize()
        {
            //Clear(Decoder.Initialize);
            Decoder.Initialize();
        }

        public void FlushBuffers()
        {
            Decoder.FlushBuffers();
            _timestampOffset = null;
        }

        public void ProcessData(byte[] buffer, int length)
        {
            if (null == buffer)
                _tsDecoder.ParseEnd();
            else
                _tsDecoder.Parse(buffer, 0, length);
        }

        void AddMediaStream(IMediaParserMediaStream mediaParserMediaStream)
        {
            lock (_mediaStreamsLock)
            {
                _mediaStreams.Add(mediaParserMediaStream);
            }

            var h = _mediaParserStreamHandler;

            if (null == h)
                return;

            h(mediaParserMediaStream);
        }

        void RegisterHandlers(PesHandlerFactory pesHandlerFactory, IEnumerable<KeyValuePair<byte, PacketHandlerFactory>> factories)
        {
            foreach (var handlerFactory in factories)
            {
                var id = handlerFactory.Key;
                var factory = handlerFactory.Value;

                pesHandlerFactory.RegisterHandlerFactory(id, (pid, streamType) => CreatePacketHandler(factory, pid, streamType));
            }
        }

        Action<TsPesPacket> CreatePacketHandler(PacketHandlerFactory streamHandlerFactory, uint pid, TsStreamType streamType)
        {
            var streamBuffer = new StreamBuffer(_tsDecoder.FreePesPacket, _bufferingManager);

            var ms = streamHandlerFactory(pid, streamType, streamBuffer,
                                          packet =>
                                          {
                                              if (null != packet)
                                              {
                                                  if (!_timestampOffset.HasValue)
                                                  {
                                                      _timestampOffset = packet.Timestamp - StartPosition;
                                                      packet.Timestamp = StartPosition;
                                                  }
                                                  else
                                                      packet.Timestamp -= _timestampOffset.Value;

                                                  Debug.Assert(packet.Timestamp >= StartPosition, string.Format("packet.Timestamp >= StartPosition: {0} >= {1} is {2}", packet.Timestamp, StartPosition, packet.Timestamp >= StartPosition));
                                              }

                                              streamBuffer.Enqueue(packet);
                                          });

            AddMediaStream(ms);

            return ms.PacketHandler;
        }

        public void ReportPosition(TimeSpan position)
        {
            _bufferingManager.ReportPosition(position);
        }
    }
}

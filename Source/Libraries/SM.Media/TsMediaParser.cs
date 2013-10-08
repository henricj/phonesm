// -----------------------------------------------------------------------
//  <copyright file="TsMediaParser.cs" company="Henric Jungheim">
//  Copyright (c) 2012, 2013.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012, 2013 Henric Jungheim <software@henric.org>
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
    public sealed class TsMediaParser : IMediaParser
    {
        #region Delegates

        public delegate MediaStream PacketHandlerFactory(TsDecoder tsDecoder, uint pid, TsStreamType streamType, IStreamSource streamBuffer, Action<TsPesPacket> nextHandler);

        #endregion

        readonly IBufferingManager _bufferingManager;
        readonly Action _checkForSamples;
        readonly Action<IMediaParserMediaStream> _mediaParserStreamHandler;
        readonly List<IMediaParserMediaStream> _mediaStreams = new List<IMediaParserMediaStream>();
        readonly object _mediaStreamsLock = new object();
        readonly PesHandlers _pesHandlers;
        readonly List<Action<TimeSpan>> _timestampOffsetHandlers = new List<Action<TimeSpan>>();
        readonly TsDecoder _tsDecoder;
        readonly ITsTimestamp _tsTimemestamp;

        public TsMediaParser(IBufferingManager bufferingManager, ITsTimestamp tsTimemestamp, Action checkForSamples, Action<IMediaParserMediaStream> mediaParserStreamHandler, Func<uint, TsStreamType, Action<TsPesPacket>> handlerFactory = null)
        {
            if (null == bufferingManager)
                throw new ArgumentNullException("bufferingManager");
            if (checkForSamples == null)
                throw new ArgumentNullException("checkForSamples");
            if (tsTimemestamp == null)
                throw new ArgumentNullException("tsTimemestamp");

            _bufferingManager = bufferingManager;
            _checkForSamples = checkForSamples;
            _tsTimemestamp = tsTimemestamp;

            if (null == handlerFactory)
            {
                var phf = new PesHandlerFactory();

                RegisterHandlers(phf, TsMediaHandlerFactories.DefaultFactories);

                handlerFactory = phf.CreateHandler;
            }

            _pesHandlers = new PesHandlers(handlerFactory);

            _mediaParserStreamHandler = mediaParserStreamHandler;

            _tsDecoder = new TsDecoder(new BufferPool(5 * 64 * 1024, 2), _pesHandlers.GetPesHandler);
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

        #region IMediaParser Members

        public TimeSpan StartPosition
        {
            get { return _tsTimemestamp.StartPosition; }
            set { _tsTimemestamp.StartPosition = value; }
        }

        public bool EnableProcessing
        {
            get { return _tsDecoder.EnableProcessing; }
            set { _tsDecoder.EnableProcessing = value; }
        }

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

        public void Initialize(Action<IProgramStreams> programStreamsHandler = null)
        {
            //Clear(Decoder.Initialize);
            Decoder.Initialize(programStreamsHandler);
        }

        public void FlushBuffers()
        {
            Decoder.FlushBuffers();
            _tsTimemestamp.Flush();
        }

        public void ProcessEndOfData()
        {
            _tsDecoder.ParseEnd();
        }

        public void ProcessData(byte[] buffer, int length)
        {
            _tsDecoder.Parse(buffer, 0, length);
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
            var streamBuffer = new StreamBuffer(streamType, _tsDecoder.PesPacketPool.FreePesPacket, _bufferingManager, _checkForSamples);

            var localStreamBuffer = streamBuffer;

            lock (_mediaStreamsLock)
            {
                _timestampOffsetHandlers.Add(ts => localStreamBuffer.TimestampOffset = ts);
            }

            var gotFirstPacket = false;

            var ms = streamHandlerFactory(_tsDecoder, pid, streamType, streamBuffer,
                packet =>
                {
                    if (null != packet)
                    {
                        if (_tsTimemestamp.Update(packet, !gotFirstPacket))
                        {
                            if (_tsTimemestamp.Offset.HasValue)
                            {
                                var offset = _tsTimemestamp.Offset.Value;

                                lock (_mediaStreamsLock)
                                {
                                    foreach (var timestampOffsetHandler in _timestampOffsetHandlers)
                                        timestampOffsetHandler(offset);
                                }
                            }
                        }

                        gotFirstPacket = true;

                        Debug.Assert(packet.PresentationTimestamp >= StartPosition, string.Format("packet.Timestamp >= StartPosition: {0} >= {1} is {2}", packet.PresentationTimestamp, StartPosition, packet.PresentationTimestamp >= StartPosition));
                    }

                    streamBuffer.Enqueue(packet);
                });

            AddMediaStream(ms);

            return ms.PacketHandler;
        }
    }
}

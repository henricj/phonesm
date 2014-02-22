// -----------------------------------------------------------------------
//  <copyright file="TsMediaParser.cs" company="Henric Jungheim">
//  Copyright (c) 2012-2014.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012-2014 Henric Jungheim <software@henric.org>
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
using System.Linq;
using System.Threading.Tasks;
using SM.Media.MediaParser;
using SM.Media.Pes;
using SM.Media.Utility;
using SM.TsParser;
using SM.TsParser.Utility;

namespace SM.Media
{
    public sealed class TsMediaParser : IMediaParser
    {
        readonly IBufferPool _bufferPool;
        readonly List<IMediaParserMediaStream> _mediaStreams = new List<IMediaParserMediaStream>();
        readonly object _mediaStreamsLock = new object();
        readonly IPesHandlers _pesHandlers;
        readonly List<Action<TimeSpan>> _timestampOffsetHandlers = new List<Action<TimeSpan>>();
        readonly ITsDecoder _tsDecoder;
        readonly ITsPesPacketPool _tsPesPacketPool;
        readonly ITsTimestamp _tsTimemestamp;
        Func<TsStreamType, Action<TsPesPacket>, StreamBuffer> _streamBufferFactory;
        int? _streamCount;

        public TsMediaParser(ITsDecoder tsDecoder, ITsPesPacketPool tsPesPacketPool, IBufferPool bufferPool, ITsTimestamp tsTimemestamp, IPesHandlers pesHandlers)
        {
            if (null == tsDecoder)
                throw new ArgumentNullException("tsDecoder");
            if (null == tsPesPacketPool)
                throw new ArgumentNullException("tsPesPacketPool");
            if (null == bufferPool)
                throw new ArgumentNullException("bufferPool");
            if (null == tsTimemestamp)
                throw new ArgumentNullException("tsTimemestamp");
            if (null == pesHandlers)
                throw new ArgumentNullException("pesHandlers");

            _tsPesPacketPool = tsPesPacketPool;
            _bufferPool = bufferPool;
            _tsDecoder = tsDecoder;
            _tsTimemestamp = tsTimemestamp;
            _pesHandlers = pesHandlers;
        }

        #region IMediaParser Members

        public ICollection<IMediaParserMediaStream> MediaStreams
        {
            get
            {
                lock (_mediaStreamsLock)
                {
                    return _mediaStreams.ToArray();
                }
            }
        }

        public TimeSpan StartPosition
        {
            get { return _tsTimemestamp.StartPosition; }
            set { _tsTimemestamp.StartPosition = value; }
        }

        public event EventHandler ConfigurationComplete;

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
            CleanupStreams();
        }

        public void Initialize(Func<TsStreamType, Action<TsPesPacket>, StreamBuffer> streamBufferFactory, Action<IProgramStreams> programStreamsHandler = null)
        {
            if (streamBufferFactory == null)
                throw new ArgumentNullException("streamBufferFactory");

            _streamBufferFactory = streamBufferFactory;

            var handler = programStreamsHandler ?? DefaultProgramStreamsHandler;

            programStreamsHandler = pss =>
                                    {
                                        handler(pss);

                                        _streamCount = pss.Streams.Count(s => !s.BlockStream);
                                    };

            _tsDecoder.Initialize(CreatePacketizedElementaryStream, programStreamsHandler);
        }

        public void FlushBuffers()
        {
            _tsDecoder.FlushBuffers();
            _tsTimemestamp.Flush();
        }

        public void ProcessEndOfData()
        {
            _tsDecoder.ParseEnd();
        }

        public void ProcessData(byte[] buffer, int offset, int length)
        {
            _tsDecoder.Parse(buffer, offset, length);
        }

        #endregion

        static void DefaultProgramStreamsHandler(IProgramStreams pss)
        {
            var hasAudio = false;
            var hasVideo = false;

            foreach (var stream in pss.Streams)
            {
                switch (stream.StreamType.Contents)
                {
                    case TsStreamType.StreamContents.Audio:
                        if (hasAudio)
                            stream.BlockStream = true;
                        else
                            hasAudio = true;
                        break;
                    case TsStreamType.StreamContents.Video:
                        if (hasVideo)
                            stream.BlockStream = true;
                        else
                            hasVideo = true;
                        break;
                    default:
                        stream.BlockStream = true;
                        break;
                }
            }
        }

        void CleanupStreams()
        {
            lock (_mediaStreamsLock)
            {
                _mediaStreams.Clear();
            }
        }

        void AddMediaStream(MediaStream mediaStream)
        {
            lock (_mediaStreamsLock)
            {
                _mediaStreams.Add(mediaStream);
            }

            CheckConfigurationComplete();
        }

        void CheckConfigurationComplete()
        {
            var streams = MediaStreams;

            if (!_streamCount.HasValue || _streamCount.Value != streams.Count)
                return;

            if (streams.Any(stream => null == stream.ConfigurationSource || !stream.ConfigurationSource.IsConfigured))
                return;

            FireConfigurationComplete();
        }

        void FireConfigurationComplete()
        {
            var cc = ConfigurationComplete;

            if (null == cc)
                return;

            ConfigurationComplete = null;

            var task = TaskEx.Run(() => cc(this, EventArgs.Empty));

            TaskCollector.Default.Add(task, "TsMediaParser.FireConfigurationComplete()");
        }

        TsPacketizedElementaryStream CreatePacketizedElementaryStream(TsStreamType streamType, uint pid)
        {
            var streamBuffer = _streamBufferFactory(streamType, _tsPesPacketPool.FreePesPacket);

            lock (_mediaStreamsLock)
            {
                _timestampOffsetHandlers.Add(ts => streamBuffer.TimestampOffset = ts);
            }

            var gotFirstPacket = false;

            var pesStreamHandler = _pesHandlers.GetPesHandler(streamType, pid,
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

                        Debug.Assert(packet.PresentationTimestamp >= StartPosition, String.Format("packet.Timestamp >= StartPosition: {0} >= {1} is {2}", packet.PresentationTimestamp, StartPosition, packet.PresentationTimestamp >= StartPosition));
                    }

                    streamBuffer.Enqueue(packet);
                });

            var pes = new TsPacketizedElementaryStream(_bufferPool, _tsPesPacketPool, pesStreamHandler.PacketHandler, streamType, pid);

            var configurator = pesStreamHandler.Configurator;

            EventHandler configuratorOnConfigurationComplete = null;

            configuratorOnConfigurationComplete = (o, e) =>
                                                  {
                                                      configurator.ConfigurationComplete -= configuratorOnConfigurationComplete;

                                                      CheckConfigurationComplete();
                                                  };

            configurator.ConfigurationComplete += configuratorOnConfigurationComplete;

            var ms = new MediaStream(configurator, streamBuffer);

            AddMediaStream(ms);

            return pes;
        }
    }
}

// -----------------------------------------------------------------------
//  <copyright file="AudioStreamHandler.cs" company="Henric Jungheim">
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
using System.Diagnostics;
using SM.Media.Configuration;
using SM.Media.Pes;
using SM.TsParser;
using SM.TsParser.Utility;

namespace SM.Media.Audio
{
    public abstract class AudioStreamHandler : PesStreamHandler
    {
        protected readonly IAudioConfigurator _configurator;
        readonly IAudioFrameHeader _frameHeader;
        readonly int _minimumPacketSize;
        protected readonly Action<TsPesPacket> _nextHandler;
        readonly ITsPesPacketPool _pesPacketPool;
        bool _isConfigured;
        protected AudioParserBase _parser;

        protected AudioStreamHandler(uint pid, TsStreamType streamType, IAudioFrameHeader frameHeader, IAudioConfigurator configurator, int minimumPacketSize, ITsPesPacketPool pesPacketPool, Action<TsPesPacket> nextHandler)
            : base(pid, streamType)
        {
            if (pesPacketPool == null)
                throw new ArgumentNullException("pesPacketPool");
            if (nextHandler == null)
                throw new ArgumentNullException("nextHandler");
            if (minimumPacketSize < 1)
                throw new ArgumentOutOfRangeException("minimumPacketSize", "minimumPacketSize must be positive: " + minimumPacketSize);

            _pesPacketPool = pesPacketPool;
            _nextHandler = nextHandler;

            if (null == frameHeader)
                throw new ArgumentNullException("frameHeader");

            _frameHeader = frameHeader;
            _configurator = configurator;
            _minimumPacketSize = minimumPacketSize;
        }

        public override IConfigurationSource Configurator
        {
            get { return _configurator; }
        }

        public override TimeSpan? GetDuration(TsPesPacket packet)
        {
            if (packet.Duration.HasValue)
                return packet.Duration;

            var duration = TimeSpan.Zero;

            var length = packet.Length;
            var endOffset = packet.Index + length;
            int nextFrameOffset;
            var skipLength = 0;

            for (var i = packet.Index; i < endOffset; i += nextFrameOffset, length -= nextFrameOffset)
            {
                if (_frameHeader.Parse(packet.Buffer, i, length))
                {
                    duration += _frameHeader.Duration;

                    if (_frameHeader.HeaderOffset > 0)
                        Debug.WriteLine("AudioStreamHandler.GetDuration() skipping {0} bytes before frame", _frameHeader.HeaderOffset);

                    nextFrameOffset = _frameHeader.HeaderOffset + _frameHeader.FrameLength;
                    skipLength = 0;
                }
                else
                {
                    if (length > _frameHeader.HeaderOffset + _minimumPacketSize)
                    {
                        nextFrameOffset = _frameHeader.HeaderOffset + 1;
                        skipLength += nextFrameOffset;
                        continue;
                    }

                    Debug.WriteLine("AudioStreamHandler.GetDuration() unable to find frame, skipping {0} bytes", length + skipLength);
                    break;
                }
            }

            packet.Duration = duration;

            return duration;
        }

        public override void PacketHandler(TsPesPacket packet)
        {
            base.PacketHandler(packet);

            if (null == packet)
            {
                if (null != _parser)
                    _parser.FlushBuffers();

                if (null != _nextHandler)
                    _nextHandler(null);

                return;
            }

            if (null != _parser)
            {
                _parser.Position = packet.PresentationTimestamp;
                _parser.ProcessData(packet.Buffer, packet.Index, packet.Length);

                _pesPacketPool.FreePesPacket(packet);

                return;
            }

            //Reject garbage packet
            if (packet.Length < _minimumPacketSize)
            {
                _pesPacketPool.FreePesPacket(packet);
                return;
            }

            if (!_isConfigured)
            {
                if (_frameHeader.Parse(packet.Buffer, packet.Index, packet.Length, true))
                {
                    _isConfigured = true;
                    _configurator.Configure(_frameHeader);
                }
            }

            _nextHandler(packet);
        }
    }
}

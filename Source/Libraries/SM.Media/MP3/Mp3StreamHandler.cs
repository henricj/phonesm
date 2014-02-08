// -----------------------------------------------------------------------
//  <copyright file="Mp3StreamHandler.cs" company="Henric Jungheim">
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
using SM.Media.Audio;
using SM.Media.Pes;
using SM.TsParser;
using SM.TsParser.Utility;

namespace SM.Media.MP3
{
    class Mp3StreamHandler : PesStreamHandler
    {
        const bool UseParser = false; // Have Mp3Parser parse the stream and submit frames to the OS.
        readonly Action<IAudioFrameHeader> _configurator;
        readonly Mp3FrameHeader _frameHeader = new Mp3FrameHeader();
        readonly Action<TsPesPacket> _nextHandler;
        readonly Mp3Parser _parser;
        readonly ITsPesPacketPool _pesPacketPool;
        bool _foundFrame;

        public Mp3StreamHandler(ITsPesPacketPool pesPacketPool, uint pid, TsStreamType streamType, Action<TsPesPacket> nextHandler, Action<IAudioFrameHeader> configurator)
            : base(pid, streamType)
        {
            if (pesPacketPool == null)
                throw new ArgumentNullException("pesPacketPool");
            if (nextHandler == null)
                throw new ArgumentNullException("nextHandler");
            if (configurator == null)
                throw new ArgumentNullException("configurator");

            _pesPacketPool = pesPacketPool;
            _nextHandler = nextHandler;
            _configurator = configurator;

            if (UseParser)
                _parser = new Mp3Parser(pesPacketPool, _configurator, _nextHandler);
        }

        public override void PacketHandler(TsPesPacket packet)
        {
            base.PacketHandler(packet);

            if (null == packet)
            {
                if (null != _parser)
                    _parser.FlushBuffers();

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

            if (false)
            {
                FrameSplittingHandler(packet);

                return;
            }

            if (!_foundFrame)
            {
                if (_frameHeader.Parse(packet.Buffer, packet.Index, packet.Length, true))
                {
                    _configurator(_frameHeader);
                    _foundFrame = true;
                }
            }

            _nextHandler(packet);
        }

        void FrameSplittingHandler(TsPesPacket packet)
        {
            var index = packet.Index;
            var length = packet.Length;
            var timestamp = packet.PresentationTimestamp;

            for (; ; )
            {
                if (!_frameHeader.Parse(packet.Buffer, index, length))
                {
                    if (_frameHeader.MarkerIndex.HasValue && _frameHeader.MarkerIndex.Value + 7 < packet.Index + packet.Length)
                    {
                        // We saw a bad header, but we did find a marker.  Skip the marker and try
                        // to find another packet.
                        var newIndex = _frameHeader.MarkerIndex.Value + 1;
                        var newLength = length - (newIndex - index);

                        length = newLength;
                        index = newIndex;

                        continue;
                    }

                    _pesPacketPool.FreePesPacket(packet);

                    return;
                }

                if (!_foundFrame)
                {
                    _foundFrame = true;

                    _configurator(_frameHeader);
                }

                Debug.Assert(_frameHeader.MarkerIndex.HasValue);

                index = _frameHeader.MarkerIndex.Value;
                var endIndex = _frameHeader.EndIndex.Value;
                var frameLength = endIndex - index;

                Debug.Assert(frameLength <= length);
                Debug.Assert(index >= 0 && index >= packet.Index);
                Debug.Assert(endIndex >= index && endIndex <= packet.Index + packet.Length);
                Debug.Assert(frameLength <= packet.Length);

                if (endIndex + 4 >= packet.Index + packet.Length)
                {
                    packet.Index = index;
                    packet.Length = length;
                    packet.PresentationTimestamp = timestamp;

                    _nextHandler(packet);

                    return;
                }

                var copyPacket = _pesPacketPool.CopyPesPacket(packet, index, frameLength);

                copyPacket.PresentationTimestamp = timestamp;

                _nextHandler(copyPacket);

                index += frameLength;
                length -= frameLength;
                timestamp += _frameHeader.Duration;
            }
        }
    }
}

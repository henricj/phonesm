// -----------------------------------------------------------------------
//  <copyright file="AacStreamHandler.cs" company="Henric Jungheim">
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
using SM.Media.Audio;
using SM.Media.Pes;
using SM.TsParser;
using SM.TsParser.Utility;

namespace SM.Media.AAC
{
    class AacStreamHandler : PesStreamHandler
    {
        readonly Action<IAudioFrameHeader> _configurator;
        readonly AacFrameHeader _frameHeader = new AacFrameHeader();
        readonly Action<TsPesPacket> _nextHandler;
        readonly AacParser _parser;
        readonly ITsPesPacketPool _pesPacketPool;
        bool _isConfigured;

        public AacStreamHandler(ITsPesPacketPool pesPacketPool, uint pid, TsStreamType streamType, Action<TsPesPacket> nextHandler, Action<IAudioFrameHeader> configurator)
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

            if (AacDecoderSettings.Parameters.UseParser)
                _parser = new AacParser(pesPacketPool, configurator, _nextHandler);
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

                return;
            }

            //Reject garbage packet
            if (packet.Length < 7)
            {
                _pesPacketPool.FreePesPacket(packet);
                return;
            }

            if (!_isConfigured)
            {
                if (_frameHeader.Parse(packet.Buffer, packet.Index, packet.Length, true))
                {
                    _isConfigured = true;
                    _configurator(_frameHeader);
                }
            }

            _nextHandler(packet);
        }
    }
}

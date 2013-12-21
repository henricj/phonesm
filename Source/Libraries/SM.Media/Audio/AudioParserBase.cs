// -----------------------------------------------------------------------
//  <copyright file="AudioParserBase.cs" company="Henric Jungheim">
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
using SM.TsParser;
using SM.TsParser.Utility;

namespace SM.Media.Audio
{
    abstract class AudioParserBase
    {
        protected readonly IAudioFrameHeader _frameHeader;
        protected Action<IAudioFrameHeader> _configurationHandler;
        protected int _index;
        protected bool _isConfigured;
        protected TsPesPacket _packet;
        protected ITsPesPacketPool _pesPacketPool;
        TimeSpan? _position;
        protected int _startIndex;
        protected Action<TsPesPacket> _submitPacket;

        protected AudioParserBase(IAudioFrameHeader frameHeader, ITsPesPacketPool pesPacketPool, Action<IAudioFrameHeader> configurationHandler, Action<TsPesPacket> submitPacket)
        {
            if (frameHeader == null)
                throw new ArgumentNullException("frameHeader");
            if (pesPacketPool == null)
                throw new ArgumentNullException("pesPacketPool");
            if (configurationHandler == null)
                throw new ArgumentNullException("configurationHandler");
            if (submitPacket == null)
                throw new ArgumentNullException("submitPacket");

            _frameHeader = frameHeader;
            _pesPacketPool = pesPacketPool;
            _configurationHandler = configurationHandler;
            _submitPacket = submitPacket;
        }

        public TimeSpan StartPosition { get; set; }

        public TimeSpan? Position
        {
            get { return _position; }
            set { _position = value; }
        }

        protected void SubmitFrame()
        {
            if (_index > _startIndex)
            {
                var packet = _pesPacketPool.CopyPesPacket(_packet, _startIndex, _index - _startIndex);

                if (!Position.HasValue)
                    Position = StartPosition;

                packet.PresentationTimestamp = Position.Value;
                packet.Duration = _frameHeader.Duration;

                Position += _frameHeader.Duration;

                _submitPacket(packet);
            }

            _startIndex = _index;

            EnsureBufferSpace(128);
        }

        protected void EnsureBufferSpace(int length)
        {
            if (null == _packet)
            {
                _packet = CreatePacket(length);
                _index = 0;
                _startIndex = 0;

                return;
            }

            if (_index + length <= _packet.Buffer.Length)
                return;

            var newBuffer = CreatePacket(length);

            if (_index > _startIndex)
            {
                // Copy the partial frame to the new buffer.
                _index -= _startIndex;

                Array.Copy(_packet.Buffer, _startIndex, newBuffer.Buffer, 0, _index);
            }
            else
                _index = 0;

            _startIndex = 0;

            _packet.Length = 0;
            _pesPacketPool.FreePesPacket(_packet);

            _packet = newBuffer;
        }

        TsPesPacket CreatePacket(int length)
        {
            var packet = _pesPacketPool.AllocatePesPacket(length);

            packet.Length = packet.Buffer.Length;

            return packet;
        }

        public void FlushBuffers()
        {
            FreeBuffer();
        }

        void FreeBuffer()
        {
            if (null != _packet)
            {
                var packet = _packet;

                _packet = null;

                packet.Length = 0;
                _pesPacketPool.FreePesPacket(packet);
            }

            _startIndex = 0;
            _index = 0;
        }

        public abstract void ProcessData(byte[] buffer, int offset, int length);
    }
}
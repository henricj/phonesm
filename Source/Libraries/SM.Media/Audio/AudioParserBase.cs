// -----------------------------------------------------------------------
//  <copyright file="AudioParserBase.cs" company="Henric Jungheim">
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
using System.Threading;
using SM.TsParser;
using SM.TsParser.Utility;

namespace SM.Media.Audio
{
    public abstract class AudioParserBase : IAudioParser
    {
        protected readonly IAudioFrameHeader _frameHeader;
        protected Action<IAudioFrameHeader> _configurationHandler;
        protected int _index;
        protected bool _isConfigured;
        int _isDisposed;
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

        #region IAudioParser Members

        public TimeSpan StartPosition { get; set; }

        public TimeSpan? Position
        {
            get { return _position; }
            set { _position = value; }
        }

        public void Dispose()
        {
            if (0 != Interlocked.Exchange(ref _isDisposed, 1))
                return;

            Dispose(true);

            GC.SuppressFinalize(this);
        }

        public virtual void FlushBuffers()
        {
            FreeBuffer();
        }

        public abstract void ProcessData(byte[] buffer, int offset, int length);

        #endregion

        protected virtual void SubmitFrame()
        {
            var length = _index - _startIndex;

            if (length > 0)
            {
                TsPesPacket packet;

                if (_index + 128 >= _packet.Buffer.Length)
                {
                    packet = _packet;
                    _packet = null;

                    packet.Length = length;
                    packet.Index = _startIndex;
                }
                else
                    packet = _pesPacketPool.CopyPesPacket(_packet, _startIndex, length);

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
                _index = 0;
                _startIndex = 0;
                _packet = null;
                _packet = CreatePacket(length);

                return;
            }

            if (_index + length <= _packet.Buffer.Length)
                return;

            var newPacket = CreatePacket(length);

            if (_index > _startIndex)
            {
                // Copy the partial frame to the new buffer.
                _index -= _startIndex;

                Array.Copy(_packet.Buffer, _startIndex, newPacket.Buffer, 0, _index);
            }
            else
                _index = 0;

            _startIndex = 0;

            _packet.Length = 0;
            _pesPacketPool.FreePesPacket(_packet);

            _packet = newPacket;
        }

        TsPesPacket CreatePacket(int length)
        {
            var packet = _pesPacketPool.AllocatePesPacket(length);

            packet.Length = packet.Buffer.Length;

            return packet;
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

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            FreeBuffer();

            _pesPacketPool = null;
            _configurationHandler = null;
            _submitPacket = null;
        }
    }
}

//-----------------------------------------------------------------------
// <copyright file="TsStream.cs" company="Henric Jungheim">
// Copyright (c) 2012.
// <author>Henric Jungheim</author>
// </copyright>
//-----------------------------------------------------------------------
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

namespace SM.TsParser
{
    public class TsStream
    {
        readonly uint _pid;
        int _count;
        readonly byte[] _buffer = new byte[32 * 1024];
        int _index;
        readonly Action<TsStream> _handler;
        readonly TsDecoder _decoder;

        public uint PID { get { return _pid; } }
        public int Length { get { return _index; } }
        public byte[] Buffer { get { return _buffer; } }

        public TsStream(TsDecoder decoder, uint pid, Action<TsStream> handler)
        {
            _decoder = decoder;
            _pid = pid;
            _handler = handler;
        }

        public void Add(TsPacket packet)
        {
            if (packet.IsStart)
                _index = 0;
            else
            {
                // Ignore duplicates and other nonsense
                var nextCount = (_count + 1) & 0x0f;

                if (packet.ContinuityCount != nextCount)
                    return;
            }

            _count = packet.ContinuityCount;

            var length = packet.PayloadLength;

            if (_index + length <= _buffer.Length)
            {
                packet.CopyTo(_buffer, _index);
                _index += length;

                if (null != _handler)
                    _handler(this);
            }
        }
    }
}

// -----------------------------------------------------------------------
//  <copyright file="PesStream.cs" company="Henric Jungheim">
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
using System.IO;
using SM.TsParser;

namespace SM.Media.Pes
{
    public sealed class PesStream : Stream
    {
        int _location;
        TsPesPacket _packet;

        public TsPesPacket Packet
        {
            get { return _packet; }
            set
            {
                _packet = value;
                _location = 0;
            }
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override long Length
        {
            get { return Packet.Length; }
        }

        public override long Position
        {
            get { return _location; }
            set { Seek(value, SeekOrigin.Begin); }
        }

        public override void Flush()
        { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var p = Packet;

            count = Math.Min(count, p.Length - _location);

            if (count < 1)
                return 0;

            Array.Copy(p.Buffer, p.Index + _location, buffer, offset, count);

            _location += count;

            return count;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    if (offset > Packet.Length || offset < 0)
                        throw new ArgumentOutOfRangeException("offset");

                    _location = (int)offset;

                    break;
                case SeekOrigin.End:
                    if (offset > Packet.Length || offset < 0)
                        throw new ArgumentOutOfRangeException("offset");

                    _location = Packet.Length - (int)offset;

                    break;

                case SeekOrigin.Current:
                    var newOffset = _location + offset;

                    if (newOffset < 0 || newOffset > Packet.Length)
                        throw new ArgumentOutOfRangeException("offset");

                    _location = (int)newOffset;

                    break;
                default:
                    throw new ArgumentException("origin");
            }

            return _location;
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}

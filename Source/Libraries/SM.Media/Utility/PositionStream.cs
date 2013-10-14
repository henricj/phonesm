// -----------------------------------------------------------------------
//  <copyright file="PositionStream.cs" company="Henric Jungheim">
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
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SM.Media.Utility
{
    sealed class PositionStream : Stream
    {
        readonly Stream _parent;
        long _position;

        public PositionStream(Stream parent)
        {
            if (parent == null)
                throw new ArgumentNullException("parent");

            _parent = parent;
        }

        public override bool CanRead
        {
            get { return _parent.CanRead; }
        }

        public override bool CanSeek
        {
            get { return _parent.CanSeek; }
        }

        public override bool CanWrite
        {
            get { return _parent.CanWrite; }
        }

        public override long Length
        {
            get { return _parent.Length; }
        }

        public override long Position
        {
            get { return _position; }
            set { _position = value; }
        }

        public override bool CanTimeout
        {
            get { return _parent.CanTimeout; }
        }

        public override void Flush()
        {
            _parent.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var length = _parent.Read(buffer, offset, count);

            _position += length;

            return length;
        }

#if NETFX_CORE
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var length = await _parent.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);

            _position += length;

            return length;
        }
#else
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return _parent.BeginRead(buffer, offset, count, callback, state);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            var length = _parent.EndRead(asyncResult);

            _position += length;

            return length;
        }
#endif

        public override long Seek(long offset, SeekOrigin origin)
        {
            var position = _parent.Seek(offset, origin);

            _position = position;

            return position;
        }

        public override void SetLength(long value)
        {
            _parent.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _parent.Write(buffer, offset, count);
        }

        public override int GetHashCode()
        {
            return _parent.GetHashCode();
        }

        public override int ReadByte()
        {
            var x = _parent.ReadByte();

            if (-1 == x)
                return x;

            ++_position;

            return x;
        }

        public override void WriteByte(byte value)
        {
            _parent.WriteByte(value);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
                _parent.Dispose();
        }
    }
}

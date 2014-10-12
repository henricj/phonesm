// -----------------------------------------------------------------------
//  <copyright file="ContentLengthStream.cs" company="Henric Jungheim">
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
using System.Threading.Tasks;

namespace SM.Media.Web.HttpConnection
{
    public class ContentLengthStream : AsyncReaderStream
    {
        readonly long? _contentLength;
        readonly IHttpReader _reader;

        public ContentLengthStream(IHttpReader reader, long? contentLength)
        {
            if (null == reader)
                throw new ArgumentNullException("reader");

            _reader = reader;
            _contentLength = contentLength;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (null == buffer)
                throw new ArgumentNullException("buffer");
            if (offset < 0 || offset > buffer.Length)
                throw new ArgumentOutOfRangeException("offset");
            if (count < 1 || count + offset > buffer.Length)
                throw new ArgumentOutOfRangeException("count");

            if (_contentLength.HasValue)
            {
                var remaining = _contentLength.Value - Position;

                if (count > remaining)
                    count = (int)remaining;
            }

            if (count < 1)
                return 0;

            var length = await _reader.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);

            //Debug.WriteLine("ContentLengthStream.ReadAsync() {0}/{1}", length, count);

            if (length > 0)
                IncrementPosition(length);

            return length;
        }
    }
}

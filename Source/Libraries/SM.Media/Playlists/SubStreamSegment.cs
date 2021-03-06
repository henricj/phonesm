// -----------------------------------------------------------------------
//  <copyright file="SubStreamSegment.cs" company="Henric Jungheim">
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
using System.Threading;
using System.Threading.Tasks;
using SM.Media.Segments;

namespace SM.Media.Playlists
{
    public class SubStreamSegment : ISegment
    {
        readonly Uri _parentUrl;
        readonly Uri _url;

        public SubStreamSegment(Uri url, Uri parentUrl)
        {
            if (null == url)
                throw new ArgumentNullException(nameof(url));
            if (null == parentUrl)
                throw new ArgumentNullException(nameof(parentUrl));

            _url = url;
            _parentUrl = parentUrl;
        }

        public Func<Stream, CancellationToken, Task<Stream>> AsyncStreamFilter { get; set; }

        #region ISegment Members

        public TimeSpan? Duration { get; set; }

        public long? MediaSequence { get; set; }

        public Task<Stream> CreateFilterAsync(Stream stream, CancellationToken cancellationToken)
        {
            if (null == AsyncStreamFilter)
                return null;

            return AsyncStreamFilter(stream, cancellationToken);
        }

        public long Offset { get; set; }

        public long Length { get; set; }

        public Uri Url
        {
            get { return _url; }
        }

        public Uri ParentUrl
        {
            get { return _parentUrl; }
        }

        #endregion

        public override string ToString()
        {
            if (Length > 0)
                return string.Format("{0} {1} {2} [offset {3} length {4}]", MediaSequence, Duration, Url, Offset, Offset + Length);

            return string.Format("{0} {1} {2}", MediaSequence, Duration, Url);
        }
    }
}

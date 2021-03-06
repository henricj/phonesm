// -----------------------------------------------------------------------
//  <copyright file="SimpleSegmentManagerBase.cs" company="Henric Jungheim">
//  Copyright (c) 2012-2016.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012-2016 Henric Jungheim <software@henric.org>
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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SM.Media.Content;
using SM.Media.Metadata;
using SM.Media.Utility;
using SM.Media.Web;

namespace SM.Media.Segments
{
    public class SimpleSegmentManagerBase : ISegmentManager, IAsyncEnumerable<ISegment>
    {
        static readonly Task<TimeSpan> TimeSpanZeroTask = Task.FromResult(TimeSpan.Zero);
        readonly ICollection<ISegment> _segments;
        int _isDisposed;

        protected SimpleSegmentManagerBase(IWebReader webReader, ICollection<ISegment> segments, ContentType contentType, ContentType streamContentType)
        {
            if (null == webReader)
                throw new ArgumentNullException(nameof(webReader));
            if (null == segments)
                throw new ArgumentNullException(nameof(segments));

            WebReader = webReader;
            _segments = segments;
            ContentType = contentType;
            StreamContentType = streamContentType;
        }

        #region IAsyncEnumerable<ISegment> Members

        public IAsyncEnumerator<ISegment> GetEnumerator()
        {
            return new SimpleEnumerator(_segments);
        }

        #endregion

        #region ISegmentManager Members

        public void Dispose()
        {
            if (0 != Interlocked.Exchange(ref _isDisposed, 1))
                return;

            Dispose(true);

            GC.SuppressFinalize(this);
        }

        public Task<TimeSpan> SeekAsync(TimeSpan timestamp)
        {
            return TimeSpanZeroTask;
        }

        public Task StartAsync()
        {
            return TplTaskExtensions.CompletedTask;
        }

        public Task StopAsync()
        {
            return TplTaskExtensions.CompletedTask;
        }

        public IStreamMetadata StreamMetadata
        {
            get
            {
                var url = WebReader.BaseAddress;

                if (null == url)
                {
                    var segment = _segments.FirstOrDefault();

                    if (null != segment)
                        url = segment.Url;
                }

                return new StreamMetadata
                {
                    Url = url,
                    ContentType = ContentType,
                    StreamContentType = StreamContentType,
                    Duration = Duration
                };
            }
        }

        public Task CloseAsync()
        {
            return TplTaskExtensions.CompletedTask;
        }

        public IWebReader WebReader { get; }

        public TimeSpan StartPosition => TimeSpan.Zero;

        public TimeSpan? Duration => null;

        public ContentType ContentType { get; }

        public ContentType StreamContentType { get; }

        public IAsyncEnumerable<ISegment> Playlist => this;

        #endregion

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            WebReader.Dispose();
        }

        #region Nested type: SimpleEnumerator

        class SimpleEnumerator : IAsyncEnumerator<ISegment>
        {
            readonly IEnumerator<ISegment> _enumerator;

            public SimpleEnumerator(IEnumerable<ISegment> segments)
            {
                _enumerator = segments.GetEnumerator();
            }

            #region IAsyncEnumerator<ISegment> Members

            public void Dispose()
            {
                using (_enumerator)
                { }
            }

            public ISegment Current { get; private set; }

            public Task<bool> MoveNextAsync()
            {
                if (!_enumerator.MoveNext())
                    return TplTaskExtensions.FalseTask;

                Current = _enumerator.Current;

                return TplTaskExtensions.TrueTask;
            }

            #endregion
        }

        #endregion
    }
}

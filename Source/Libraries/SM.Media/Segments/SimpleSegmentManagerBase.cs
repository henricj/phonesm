// -----------------------------------------------------------------------
//  <copyright file="SimpleSegmentManagerBase.cs" company="Henric Jungheim">
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
using System.Collections.Generic;
using System.Threading.Tasks;
using SM.Media.Content;
using SM.Media.Utility;

namespace SM.Media.Segments
{
    public class SimpleSegmentManagerBase : ISegmentManager, IAsyncEnumerable<ISegment>
    {
        static readonly Task<TimeSpan> TimeSpanZeroTask = TaskEx.FromResult(TimeSpan.Zero);
        readonly Uri _baseUrl;
        readonly ContentType _contentType;
        readonly ICollection<ISegment> _segments;

        public SimpleSegmentManagerBase(Uri baseUrl, ICollection<ISegment> segments, ContentType contentType)
        {
            if (segments == null)
                throw new ArgumentNullException("segments");

            _baseUrl = baseUrl;
            _contentType = contentType;
            _segments = segments;
        }

        #region IAsyncEnumerable<ISegment> Members

        IAsyncEnumerator<ISegment> IAsyncEnumerable<ISegment>.GetEnumerator()
        {
            return new SimpleEnumerator(_segments);
        }

        #endregion

        #region ISegmentManager Members

        public void Dispose()
        { }

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

        public Task CloseAsync()
        {
            return TplTaskExtensions.CompletedTask;
        }

        public Uri Url
        {
            get { return _baseUrl; }
        }

        public TimeSpan StartPosition
        {
            get { return TimeSpan.Zero; }
        }

        public TimeSpan? Duration
        {
            get { return null; }
        }

        public ContentType ContentType
        {
            get { return _contentType; }
        }

        public IAsyncEnumerable<ISegment> Playlist
        {
            get { return this; }
        }

        #endregion

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

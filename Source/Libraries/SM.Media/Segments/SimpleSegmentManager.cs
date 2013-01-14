// -----------------------------------------------------------------------
//  <copyright file="SimpleSegmentManager.cs" company="Henric Jungheim">
//  Copyright (c) 2012.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
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
using System.Collections.Generic;
using System.Threading.Tasks;
using SM.Media.Utility;

namespace SM.Media.Segments
{
    public sealed class SimpleSegmentManager : ISegmentManager, IDisposable, IAsyncEnumerable<ISegment>
    {
        static readonly Task<TimeSpan> TimeSpanZeroTask;
        readonly IEnumerable<Uri> _urls;

        static SimpleSegmentManager()
        {
            var tcs = new TaskCompletionSource<TimeSpan>();
            tcs.SetResult(TimeSpan.Zero);

            TimeSpanZeroTask = tcs.Task;
        }

        public SimpleSegmentManager(IEnumerable<Uri> urls)
        {
            _urls = urls;
        }

        #region IAsyncEnumerable<ISegment> Members

        IAsyncEnumerator<ISegment> IAsyncEnumerable<ISegment>.GetEnumerator()
        {
            return new SimpleEnumerator(_urls);
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        { }

        #endregion

        #region ISegmentManager Members

        public Task<TimeSpan> SeekAsync(TimeSpan timestamp)
        {
            return TimeSpanZeroTask;
        }

        public Uri Url
        {
            get { return null; }
        }

        public TimeSpan StartPosition
        {
            get { return TimeSpan.Zero; }
        }

        public TimeSpan? Duration
        {
            get { return null; }
        }

        public IAsyncEnumerable<ISegment> Playlist
        {
            get { return this; }
        }

        #endregion

        #region Nested type: SimpleEnumerator

        class SimpleEnumerator : IAsyncEnumerator<ISegment>
        {
            readonly IEnumerator<Uri> _enumerator;

            public SimpleEnumerator(IEnumerable<Uri> urls)
            {
                _enumerator = urls.GetEnumerator();
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

                Current = new SimpleSegment(_enumerator.Current);

                return TplTaskExtensions.TrueTask;
            }

            #endregion
        }

        #endregion
    }
}

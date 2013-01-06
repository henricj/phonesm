// -----------------------------------------------------------------------
//  <copyright file="SegmentReaderManager.cs" company="Henric Jungheim">
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SM.Media.Utility;

namespace SM.Media.Segments
{
    public sealed class SegmentReaderManager : ISegmentReaderManager
    {
        readonly ISegmentManager[] _segmentManagers;
        readonly SegmentReaderEnumerable[] _segmentReaders;

        public SegmentReaderManager(IEnumerable<ISegmentManager> segmentManagers, IHttpWebRequestFactory webRequestFactory)
        {
            if (null == webRequestFactory)
                throw new ArgumentNullException("webRequestFactory");

            if (null == segmentManagers)
                throw new ArgumentNullException("segmentManagers");

            _segmentManagers = segmentManagers.ToArray();

            if (_segmentManagers.Length < 1)
                throw new ArgumentException("No segment managers provided");

            _segmentReaders = _segmentManagers
                .Select(sm => new SegmentReaderEnumerable(sm, webRequestFactory))
                .ToArray();
        }

        #region ISegmentReaderManager Members

        public void Dispose()
        { }

        public ICollection<IAsyncEnumerable<ISegmentReader>> SegmentReaders
        {
            get { return _segmentReaders; }
        }

        #endregion

        public async Task<TimeSpan> SeekAsync(TimeSpan timestamp, CancellationToken cancellationToken)
        {
            var tasks = _segmentManagers
                .Select(sm => sm.SeekAsync(timestamp));

#if WINDOWS_PHONE7
            var results = await TaskEx.WhenAll(tasks);
#else
            var results = await Task.WhenAll(tasks);
#endif

            return results.Min();
        }

        #region Nested type: SegmentReaderEnumearator

        class SegmentReaderEnumearator : IAsyncEnumerator<ISegmentReader>
        {
            readonly ISegmentManager _segmentManager;
            readonly IHttpWebRequestFactory _webRequestFactory;
            ISegmentReader _segmentReader;

            public SegmentReaderEnumearator(ISegmentManager segmentManager, IHttpWebRequestFactory webRequestFactory)
            {
                if (null == segmentManager)
                    throw new ArgumentNullException("segmentManager");

                if (null == webRequestFactory)
                    throw new ArgumentNullException("webRequestFactory");

                _segmentManager = segmentManager;
                _webRequestFactory = webRequestFactory;
            }

            #region IAsyncEnumerator<ISegmentReader> Members

            public void Dispose()
            {
                CloseReader();
            }

            public ISegmentReader Current
            {
                get { return _segmentReader; }
            }

            #endregion

            void CloseReader()
            {
                var segmentReader = _segmentReader;

                _segmentReader = null;

                using (segmentReader)
                { }
            }

            public async Task<bool> MoveNextAsync()
            {
                var segment = await _segmentManager.NextAsync();

                if (null == segment)
                    return false;

                CloseReader();

                _segmentReader = new SegmentReader(segment, _webRequestFactory.CreateChildFactory(_segmentManager.Url).Create);

                return true;
            }
        }

        #endregion

        #region Nested type: SegmentReaderEnumerable

        class SegmentReaderEnumerable : IAsyncEnumerable<ISegmentReader>
        {
            readonly ISegmentManager _segmentManager;
            readonly IHttpWebRequestFactory _webRequestFactory;

            public SegmentReaderEnumerable(ISegmentManager segmentManager, IHttpWebRequestFactory webRequestFactory)
            {
                if (null == segmentManager)
                    throw new ArgumentNullException("segmentManager");

                if (null == webRequestFactory)
                    throw new ArgumentNullException("webRequestFactory");

                _segmentManager = segmentManager;
                _webRequestFactory = webRequestFactory;
            }

            #region IAsyncEnumerable<ISegmentReader> Members

            public IAsyncEnumerator<ISegmentReader> GetEnumerator()
            {
                return new SegmentReaderEnumearator(_segmentManager, _webRequestFactory);
            }

            #endregion
        }

        #endregion
    }
}

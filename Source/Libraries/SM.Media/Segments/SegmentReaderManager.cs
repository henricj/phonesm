// -----------------------------------------------------------------------
//  <copyright file="SegmentReaderManager.cs" company="Henric Jungheim">
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
using SM.Media.Metadata;
using SM.Media.Utility;
using SM.Media.Web;

namespace SM.Media.Segments
{
    public sealed class SegmentReaderManager : ISegmentReaderManager
    {
        readonly ISegmentManager[] _segmentManagers;
        readonly ManagerReaders[] _segmentReaders;

        public SegmentReaderManager(IEnumerable<ISegmentManager> segmentManagers, IWebMetadataFactory webMetadataFactory, IRetryManager retryManager, IPlatformServices platformServices)
        {
            if (null == segmentManagers)
                throw new ArgumentNullException(nameof(segmentManagers));
            if (null == webMetadataFactory)
                throw new ArgumentNullException(nameof(webMetadataFactory));
            if (null == retryManager)
                throw new ArgumentNullException(nameof(retryManager));
            if (null == platformServices)
                throw new ArgumentNullException(nameof(platformServices));

            _segmentManagers = segmentManagers.ToArray();

            if (_segmentManagers.Length < 1)
                throw new ArgumentException("No segment managers provided");

            _segmentReaders = _segmentManagers
                .Select(sm => new ManagerReaders
                {
                    Manager = sm,
                    Readers = new SegmentReaderEnumerable(sm, webMetadataFactory, retryManager, platformServices)
                })
                .ToArray();
        }

        #region ISegmentReaderManager Members

        public void Dispose()
        {
            if (null != _segmentManagers)
            {
                foreach (var segmentManager in _segmentManagers)
                {
                    using (segmentManager)
                    { }
                }
            }
        }

        public ICollection<ISegmentManagerReaders> SegmentManagerReaders
        {
            get { return _segmentReaders; }
        }

        public Task StartAsync()
        {
            var tasks = _segmentManagers.Select(sm => sm.StartAsync());

            return Task.WhenAll(tasks);
        }

        public Task StopAsync()
        {
            var tasks = _segmentManagers.Select(sm => sm.StopAsync());

            return Task.WhenAll(tasks);
        }

        public async Task<TimeSpan> SeekAsync(TimeSpan timestamp, CancellationToken cancellationToken)
        {
            var tasks = _segmentManagers
                .Select(sm => sm.SeekAsync(timestamp));

            var results = await Task.WhenAll(tasks).ConfigureAwait(false);

            return results.Min();
        }

        public TimeSpan? Duration
        {
            get { return _segmentManagers.Max(sm => sm.Duration); }
        }

        #endregion

        #region Nested type: ManagerReaders

        class ManagerReaders : ISegmentManagerReaders
        {
            #region ISegmentManagerReaders Members

            public ISegmentManager Manager { get; set; }
            public IAsyncEnumerable<ISegmentReader> Readers { get; set; }

            #endregion
        }

        #endregion

        #region Nested type: SegmentReaderEnumerable

        class SegmentReaderEnumerable : IAsyncEnumerable<ISegmentReader>
        {
            readonly IPlatformServices _platformServices;
            readonly IRetryManager _retryManager;
            readonly ISegmentManager _segmentManager;
            readonly IWebMetadataFactory _webMetadataFactory;

            public SegmentReaderEnumerable(ISegmentManager segmentManager, IWebMetadataFactory webMetadataFactory, IRetryManager retryManager, IPlatformServices platformServices)
            {
                if (null == segmentManager)
                    throw new ArgumentNullException(nameof(segmentManager));
                if (null == webMetadataFactory)
                    throw new ArgumentNullException(nameof(webMetadataFactory));
                if (null == platformServices)
                    throw new ArgumentNullException(nameof(platformServices));
                if (null == retryManager)
                    throw new ArgumentNullException(nameof(retryManager));

                _segmentManager = segmentManager;
                _webMetadataFactory = webMetadataFactory;
                _platformServices = platformServices;
                _retryManager = retryManager;
            }

            #region IAsyncEnumerable<ISegmentReader> Members

            public IAsyncEnumerator<ISegmentReader> GetEnumerator()
            {
                return new SegmentReaderEnumerator(_segmentManager, _webMetadataFactory, _retryManager, _platformServices);
            }

            #endregion
        }

        #endregion

        #region Nested type: SegmentReaderEnumerator

        class SegmentReaderEnumerator : IAsyncEnumerator<ISegmentReader>
        {
            readonly IPlatformServices _platformServices;
            readonly IRetryManager _retryManager;
            readonly IAsyncEnumerator<ISegment> _segmentEnumerator;
            readonly IWebMetadataFactory _webMetadataFactory;
            readonly IWebReader _webReader;
            ISegmentReader _segmentReader;

            public SegmentReaderEnumerator(ISegmentManager segmentManager, IWebMetadataFactory webMetadataFactory, IRetryManager retryManager, IPlatformServices platformServices)
            {
                if (null == segmentManager)
                    throw new ArgumentNullException(nameof(segmentManager));
                if (null == webMetadataFactory)
                    throw new ArgumentNullException(nameof(webMetadataFactory));
                if (null == retryManager)
                    throw new ArgumentNullException(nameof(retryManager));
                if (null == platformServices)
                    throw new ArgumentNullException(nameof(platformServices));

                _segmentEnumerator = segmentManager.Playlist.GetEnumerator();
                _webReader = segmentManager.WebReader;
                _webMetadataFactory = webMetadataFactory;
                _retryManager = retryManager;
                _platformServices = platformServices;
            }

            #region IAsyncEnumerator<ISegmentReader> Members

            public void Dispose()
            {
                CloseReader();

                using (_segmentEnumerator)
                { }
            }

            public ISegmentReader Current
            {
                get { return _segmentReader; }
            }

            public async Task<bool> MoveNextAsync()
            {
                CloseReader();

                if (!await _segmentEnumerator.MoveNextAsync().ConfigureAwait(false))
                    return false;

                var segment = _segmentEnumerator.Current;

                _segmentReader = new SegmentReader(segment, _webReader, _webMetadataFactory, _retryManager, _platformServices);

                return true;
            }

            #endregion

            void CloseReader()
            {
                var segmentReader = _segmentReader;

                _segmentReader = null;

                using (segmentReader)
                { }
            }
        }

        #endregion
    }
}

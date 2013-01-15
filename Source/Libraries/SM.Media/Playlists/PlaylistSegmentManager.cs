// -----------------------------------------------------------------------
//  <copyright file="PlaylistSegmentManager.cs" company="Henric Jungheim">
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SM.Media.M3U8;
using SM.Media.Segments;
using SM.Media.Utility;

namespace SM.Media.Playlists
{
    public class PlaylistSegmentManager : ISegmentManager, IDisposable
    {
        readonly CancellationTokenSource _abortTokenSource;
        readonly object _segmentLock = new object();
        readonly ISubProgram _subProgram;
        readonly Func<Uri, ICachedWebRequest> _webRequestFactory;
        bool _isDynamicPlaylist;
        Task _reReader;
        Task _reader;
        SubStreamSegment[] _segments;
        int _segmentsExpiration;
        int _startSegmentIndex;
        ICachedWebRequest _subPlaylistRequest;

        public PlaylistSegmentManager(Func<Uri, ICachedWebRequest> webRequestFactory, ISubProgram program)
            : this(webRequestFactory, program, CancellationToken.None)
        { }

        public PlaylistSegmentManager(Func<Uri, ICachedWebRequest> webRequestFactory, ISubProgram program, CancellationToken cancellationToken)
        {
            if (null == webRequestFactory)
                throw new ArgumentNullException("webRequestFactory");

            if (null == program)
                throw new ArgumentNullException("program");

            _webRequestFactory = webRequestFactory;
            _subProgram = program;

            _abortTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _isDynamicPlaylist = true;

            Playlist = new PlaylistEnumerable(this);
        }

        CancellationToken CancellationToken
        {
            get { return _abortTokenSource.Token; }
        }

        #region IDisposable Members

        public void Dispose()
        {
            _abortTokenSource.Cancel();
        }

        #endregion

        #region ISegmentManager Members

        public Uri Url { get; private set; }
        public TimeSpan StartPosition { get; private set; }
        public TimeSpan? Duration { get; private set; }
        public IAsyncEnumerable<ISegment> Playlist { get; private set; }

        public async Task<TimeSpan> SeekAsync(TimeSpan timestamp)
        {
            if (_isDynamicPlaylist)
                await CheckReload(-1);

            TimeSpan actualPosition;

            lock (_segmentLock)
            {
                Seek(timestamp);

                actualPosition = StartPosition;
            }

            return actualPosition;
        }

        #endregion

        void Seek(TimeSpan timestamp)
        {
            StartPosition = TimeSpan.Zero;
            _startSegmentIndex = -1;

            if (null == _segments || _segments.Length < 1)
                return;

            if (timestamp <= TimeSpan.Zero)
                return;

            var seekTime = TimeSpan.Zero;

            for (var i = 0; i < _segments.Length; ++i)
            {
                var segment = _segments[i];

                if (!segment.Duration.HasValue)
                    break;

                if (seekTime + segment.Duration > timestamp)
                {
                    StartPosition = seekTime;
                    _startSegmentIndex = i - 1;

                    return;
                }

                seekTime += segment.Duration.Value;
            }
        }

        void InitializeSegmentIndex()
        {
            var segmentIndex = -1;

            if (_isDynamicPlaylist)
            {
                // We don't want to start with the first segment in case
                // we need to buffer.  We don't want to start too far into
                // the playlist since this would mean reloading the thing
                // too often.  With enough buffering, we will eventually 
                // miss segments.  To get that working properly, we must
                // adjust the sample timestamps since otherwise there will
                // be a discontinuity for MediaElement to choke on.
                segmentIndex = _segments.Length / 4 - 1;

                if (segmentIndex + 4 >= _segments.Length)
                    segmentIndex = _segments.Length - 5;

                if (segmentIndex < -1)
                    segmentIndex = -1;
            }

            _startSegmentIndex = segmentIndex;
        }

        Task CheckReload(int index)
        {
            lock (_segmentLock)
            {
                if (null != _segments && index + 2 < _segments.Length && (!_isDynamicPlaylist || _segmentsExpiration - Environment.TickCount > 0))
                    return TplTaskExtensions.CompletedTask;

                if (null != _reReader)
                    return _reReader;

                _reReader = Task.Factory.StartNew((Func<Task>)ReadSubList).Unwrap();

                if (null == _segments || index + 1 >= _segments.Length)
                    return _reReader;
            }

            return TplTaskExtensions.CompletedTask;
        }

        async Task<M3U8Parser> FetchPlaylist(IEnumerable<Uri> urls)
        {
            foreach (var playlist in urls)
            {
                if (null == _subPlaylistRequest || _subPlaylistRequest.Url != playlist)
                    _subPlaylistRequest = _webRequestFactory(playlist);

                var localPlaylist = playlist;

                var parsedPlaylist = await _subPlaylistRequest.ReadAsync(
                    bytes =>
                    {
                        if (bytes.Length < 1)
                            return null;

                        var parser = new M3U8Parser();

                        using (var ms = new MemoryStream(bytes))
                        {
                            parser.Parse(localPlaylist, ms);
                        }

                        return parser;
                    });

                if (null != parsedPlaylist)
                    return parsedPlaylist;
            }

            return null;
        }

        async Task ReadSubList()
        {
            var programStream = _subProgram.Video;

            var parser = await FetchPlaylist(programStream.Urls);

            if (null == parser)
            {
                lock (_segmentLock)
                {
                    _segments = null;
                    _isDynamicPlaylist = false;

                    _reReader = null;
                }

                return;
            }

            Url = parser.BaseUrl;

            var segments = PlaylistSubProgramBase.GetPlaylist(parser).ToArray();

            var isDynamicPlayist = null == parser.GlobalTags.Tag(M3U8Tags.ExtXEndList);

            Duration = isDynamicPlayist ? null : GetDuration(segments);

            lock (_segmentLock)
            {
                var oldSegments = _segments;
                var oldSegmentIndex = _startSegmentIndex;

                _segments = segments;
                _isDynamicPlaylist = isDynamicPlayist;

                InitializeSegmentIndex();

                var index = PlaylistEnumerator.FindNewIndex(oldSegments, segments, oldSegmentIndex);

                if (index >= 0)
                    _startSegmentIndex = index;

                if (isDynamicPlayist)
                {
                    // We use the system uptime rather than DateTime.UtcNow to
                    // avoid grief if there is a step in the system time.
                    // TODO: Make sure the TickCount doesn't get confused by steps in the system time.
                    var segmentsExpiration = Environment.TickCount;

                    if (Duration.HasValue)
                        segmentsExpiration += (int)(Duration.Value.TotalMilliseconds * 0.75);
                    else
                        segmentsExpiration += 5000 * segments.Length;

                    _segmentsExpiration = segmentsExpiration;
                }

                Debug.WriteLine("PlaylistSegmentManager.ReadSubList: playlist {0} loaded with {1} entries. index: {2} dynamic: {3} expires: {4}",
                                parser.BaseUrl, _segments.Length, _startSegmentIndex, isDynamicPlayist,
                                isDynamicPlayist ? TimeSpan.FromMilliseconds(_segmentsExpiration - Environment.TickCount) : TimeSpan.Zero);

                _reReader = null;
            }
        }

        static TimeSpan? GetDuration(IEnumerable<SubStreamSegment> segments)
        {
            var duration = TimeSpan.Zero;

            foreach (var segment in segments)
            {
                if (!segment.Duration.HasValue)
                    return null;

                duration += segment.Duration.Value;
            }

            return duration;
        }

        #region Nested type: PlaylistEnumerable

        class PlaylistEnumerable : IAsyncEnumerable<ISegment>
        {
            readonly PlaylistSegmentManager _segmentManager;

            public PlaylistEnumerable(PlaylistSegmentManager segmentManager)
            {
                if (null == segmentManager)
                    throw new ArgumentNullException("segmentManager");

                _segmentManager = segmentManager;
            }

            #region IAsyncEnumerable<ISegment> Members

            public IAsyncEnumerator<ISegment> GetEnumerator()
            {
                return new PlaylistEnumerator(_segmentManager);
            }

            #endregion
        }

        #endregion

        #region Nested type: PlaylistEnumerator

        class PlaylistEnumerator : IAsyncEnumerator<ISegment>
        {
            readonly PlaylistSegmentManager _segmentManager;
            int _index = -1;
            SubStreamSegment[] _segments;

            public PlaylistEnumerator(PlaylistSegmentManager segmentManager)
            {
                if (null == segmentManager)
                    throw new ArgumentNullException("segmentManager");

                _segmentManager = segmentManager;
            }

            #region IAsyncEnumerator<ISegment> Members

            public void Dispose()
            { }

            public ISegment Current { get; private set; }

            public async Task<bool> MoveNextAsync()
            {
                for (; ; )
                {
                    await _segmentManager.CheckReload(_index);

                    bool isDynamicPlaylist;
                    SubStreamSegment[] segments;
                    int startIndex;

                    lock (_segmentManager._segmentLock)
                    {
                        isDynamicPlaylist = _segmentManager._isDynamicPlaylist;
                        segments = _segmentManager._segments;
                        startIndex = _segmentManager._startSegmentIndex;
                    }

                    if (null == segments)
                        return false;

                    if (!ReferenceEquals(segments, _segments))
                    {
                        if (null != _segments)
                            _index = FindNewIndex(_segments, segments, _index);
                        else if (-1 == _index)
                            _index = startIndex;

                        _segments = segments;
                    }

                    if (_index + 1 < segments.Length)
                    {
                        Current = segments[++_index];

                        return true;
                    }

                    // We seem to have run out of playlist.  If this is not
                    // a dynamic playlist, then we are done.

                    if (!isDynamicPlaylist)
                        return false;

                    await TaskEx.Delay(5000);
                }
            }

            #endregion

            public static int FindNewIndex(IList<ISegment> oldSegments, IList<ISegment> newSegments, int oldIndex)
            {
                if (null == oldSegments || oldIndex < 0 || oldIndex >= oldSegments.Count)
                    return -1;

                var url = oldSegments[oldIndex].Url;

                var index = FindIndexByUrl(url, newSegments);

                if (index >= 0)
                    return index;

                Debug.WriteLine("PlaylistSegmentManager.ReadSubList: playlist discontinuity, does not contain {0}", url);

                return -1;
            }

            static int FindIndexByUrl(Uri url, IList<ISegment> segments)
            {
                for (var i = 0; i < segments.Count; ++i)
                {
                    if (url == segments[i].Url)
                        return i;
                }

                return -1;
            }
        }

        #endregion
    }
}

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
    public class PlaylistSegmentManager : ISegmentManager
    {
        static readonly TimeSpan NotDue = new TimeSpan(0, 0, 0, 0, -1);
        static readonly TimeSpan NotPeriodic = new TimeSpan(0, 0, 0, 0, -1);
        static readonly TimeSpan MinimumReload = new TimeSpan(0, 0, 0, 5);
        readonly CancellationTokenSource _abortTokenSource;
        readonly List<SubStreamSegment[]> _dynamicPlaylists = new List<SubStreamSegment[]>();
        readonly Timer _expirationTimer;
        readonly List<SubStreamSegment> _segmentList = new List<SubStreamSegment>();
        readonly object _segmentLock = new object();
        readonly ISubProgram _subProgram;
        readonly Func<Uri, ICachedWebRequest> _webRequestFactory;
        bool _isDynamicPlaylist;
        bool _isRunning;
        Task _reReader;
        Task _reader;
        SubStreamSegment[] _segments;
        int _segmentsExpiration;
        int _startSegmentIndex = -1;
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
            _expirationTimer = new Timer(PlaylistExpiration, null, NotDue, NotPeriodic);

            Playlist = new PlaylistEnumerable(this);
        }

        CancellationToken CancellationToken
        {
            get { return _abortTokenSource.Token; }
        }

        #region ISegmentManager Members

        public void Dispose()
        {
            _abortTokenSource.Cancel();

            using (_expirationTimer)
            { }
        }

        public Uri Url { get; private set; }
        public TimeSpan StartPosition { get; private set; }
        public TimeSpan? Duration { get; private set; }
        public IAsyncEnumerable<ISegment> Playlist { get; private set; }

        public Task StartAsync()
        {
            lock (_segmentLock)
            {
                _isRunning = true;
            }

            return TplTaskExtensions.CompletedTask;
        }

        public Task StopAsync()
        {
            _expirationTimer.Change(NotDue, NotPeriodic);

            lock (_segmentLock)
            {
                _isRunning = false;

                var reReader = _reReader;

                if (null != reReader)
                    return reReader;
            }

            return TplTaskExtensions.CompletedTask;
        }

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

        void PlaylistExpiration(object state)
        {
            Debug.WriteLine("PlaylistSegmentManager.PlaylistExpiration ({0})", DateTimeOffset.Now);

            lock (_segmentLock)
            {
                if (!_isDynamicPlaylist || !_isRunning)
                    return;

                if (null == _reReader)
                {
                    Debug.WriteLine("PlaylistSegmentManager.PlaylistExpiration is starting ReadSubList ({0})", DateTimeOffset.Now);

                    _reReader = Task.Factory.StartNew((Func<Task>)ReadSubList, CancellationToken).Unwrap();
                }
            }
        }

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

        Task CheckReload(int index)
        {
            Debug.WriteLine("PlaylistSegmentManager.CheckReload ({0})", DateTimeOffset.Now);

            lock (_segmentLock)
            {
                if (!_isDynamicPlaylist || !_isRunning || _segmentsExpiration - Environment.TickCount > 0)
                    return TplTaskExtensions.CompletedTask;

                if (null == _reReader)
                {
                    Debug.WriteLine("PlaylistSegmentManager.CheckReload is starting ReadSubList ({0})", DateTimeOffset.Now);

                    _reReader = Task.Factory.StartNew((Func<Task>)ReadSubList, CancellationToken).Unwrap();
                }

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
            Debug.WriteLine("PlaylistSegmentManager.ReadSubList ({0})", DateTimeOffset.Now);

            _expirationTimer.Change(NotDue, NotPeriodic);

            var programStream = _subProgram.Video;

            var start = DateTime.UtcNow;

            var parser = await FetchPlaylist(programStream.Urls);

            var fetchElapsed = DateTime.UtcNow - start;

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
            var segments0 = segments;

            var isDynamicPlayist = null == parser.GlobalTags.Tag(M3U8Tags.ExtXEndList);

            Duration = isDynamicPlayist ? null : GetDuration(segments);

            lock (_segmentLock)
            {
                if (!_isRunning)
                    return;

                var needReload = false;

                if (isDynamicPlayist || _dynamicPlaylists.Count > 0)
                {
                    var lastPlaylist = _dynamicPlaylists.LastOrDefault();

                    if (segments.Length > 0)
                    {
                        if (null != lastPlaylist && lastPlaylist[0].Url == segments[0].Url)
                        {
                            // We are running out of playlist, but the server just gave us the
                            // same list as last time.
                            Debug.WriteLine("PlaylistSegmentManager.ReadSubList: need reload ({0})", DateTimeOffset.Now);

                            needReload = true;
                        }
                        else
                        {
                            _dynamicPlaylists.Add(segments);

                            if (_dynamicPlaylists.Count > 4)
                                _dynamicPlaylists.RemoveAt(0);
                        }
                    }

                    if (!needReload)
                    {
                        var previousPlaylist = null as SubStreamSegment[];

                        foreach (var playlist in _dynamicPlaylists)
                        {
                            if (null == previousPlaylist)
                            {
                                _startSegmentIndex = -1;
                                _segmentList.AddRange(playlist);

                                previousPlaylist = playlist;
                                continue;
                            }

                            var found = false;

                            for (var i = 0; i < previousPlaylist.Length; ++i)
                            {
                                if (previousPlaylist[i].Url == playlist[0].Url)
                                {
                                    _startSegmentIndex = _segmentList.Count - 1;

                                    for (var j = 0; j < playlist.Length; ++j)
                                    {
                                        if (i + j < previousPlaylist.Length && previousPlaylist[i + j].Url == playlist[j].Url)
                                            continue;

                                        _segmentList.Add(playlist[j]);
                                    }

                                    found = true;
                                    break;
                                }
                            }

                            if (!found)
                            {
                                _startSegmentIndex = _segmentList.Count - 1;
                                _segmentList.AddRange(playlist);
                            }

                            previousPlaylist = playlist;
                        }

                        segments = _segmentList.ToArray();
                        _segmentList.Clear();
                    }
                }

                if (!needReload)
                    _segments = segments;

                _isDynamicPlaylist = isDynamicPlayist;

                if (isDynamicPlayist)
                {
                    var reloadDelay = GetDuration(segments0, Math.Max(1, segments0.Length - 4), segments0.Length);

                    if (!reloadDelay.HasValue)
                    {
                        var segmentCount = segments0.Length - 1;

                        if (segmentCount > 0)
                            reloadDelay = new TimeSpan(0, 0, 0, 5 * segmentCount);
                        else
                            reloadDelay = TimeSpan.Zero;
                    }

                    var expire = reloadDelay.Value;

                    if (expire < MinimumReload)
                        expire = MinimumReload;

                    _expirationTimer.Change(expire, NotPeriodic);

                    // We use the system uptime rather than DateTime.UtcNow to
                    // avoid grief if there is a step in the system time.
                    // TODO: Make sure the TickCount doesn't get confused by steps in the system time.
                    var segmentsExpiration = Environment.TickCount;

                    segmentsExpiration += (int)expire.TotalMilliseconds;

                    _segmentsExpiration = segmentsExpiration;
                }

                Debug.WriteLine("PlaylistSegmentManager.ReadSubList: playlist {0} loaded with {1} entries in {2}. index: {3} dynamic: {4} expires: {5} ({6})",
                                parser.BaseUrl, _segments.Length, fetchElapsed, _startSegmentIndex, isDynamicPlayist,
                                isDynamicPlayist ? TimeSpan.FromMilliseconds(_segmentsExpiration - Environment.TickCount) : TimeSpan.Zero,
                                DateTimeOffset.Now);

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

        static TimeSpan? GetDuration(SubStreamSegment[] segments, int start, int end)
        {
            var duration = TimeSpan.Zero;

            for (var i = start; i < end; ++i)
            {
                var segment = segments[i];

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

                    var delay = 5000;

                    if (null != segments && 0 < segments.Length)
                    {
                        var lastSegment = segments[segments.Length - 1];

                        if (lastSegment.Duration.HasValue)
                            delay = (int)(lastSegment.Duration.Value.TotalMilliseconds / 2);
                    }

                    await TaskEx.Delay(delay, _segmentManager.CancellationToken);
                }
            }

            #endregion

            static int FindNewIndex(ISegment[] oldSegments, ISegment[] newSegments, int oldIndex)
            {
                if (null == oldSegments || oldIndex < 0 || oldIndex >= oldSegments.Length)
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

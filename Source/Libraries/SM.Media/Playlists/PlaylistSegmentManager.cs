// -----------------------------------------------------------------------
//  <copyright file="PlaylistSegmentManager.cs" company="Henric Jungheim">
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
        readonly CancellationToken _cancellationToken;
        readonly List<ISegment[]> _dynamicPlaylists = new List<ISegment[]>();
        readonly TimeSpan _excessiveDuration;
        readonly TimeSpan _maximumReload;
        readonly TimeSpan _minimumReload;
        readonly TimeSpan _minimumRetry;
        readonly TaskTimer _refreshTimer = new TaskTimer();
        readonly List<ISegment> _segmentList = new List<ISegment>();
        readonly object _segmentLock = new object();
        readonly Func<M3U8Parser, IStreamSegments> _segmentsFactory;
        readonly ISubProgram _subProgram;
        readonly Func<Uri, ICachedWebRequest> _webRequestFactory;
        CancellationTokenSource _abortTokenSource;
        int _dynamicStartIndex;
        int _isDisposed;
        bool _isDynamicPlaylist;
        bool _isInitialized;
        bool _isRunning;
        int _readSubListFailureCount;
        SignalTask _readTask;
        ISegment[] _segments;
        int _segmentsExpiration;
        int _startSegmentIndex = -1;
        ICachedWebRequest _subPlaylistRequest;

        public PlaylistSegmentManager(Func<Uri, ICachedWebRequest> webRequestFactory, ISubProgram program, Func<M3U8Parser, IStreamSegments> segmentsFactory)
            : this(webRequestFactory, program, segmentsFactory, CancellationToken.None)
        { }

        public PlaylistSegmentManager(Func<Uri, ICachedWebRequest> webRequestFactory, ISubProgram program, Func<M3U8Parser, IStreamSegments> segmentsFactory, CancellationToken cancellationToken)
        {
            if (null == webRequestFactory)
                throw new ArgumentNullException("webRequestFactory");

            if (null == program)
                throw new ArgumentNullException("program");

            if (segmentsFactory == null)
                throw new ArgumentNullException("segmentsFactory");

            _webRequestFactory = webRequestFactory;
            _subProgram = program;
            _segmentsFactory = segmentsFactory;
            _cancellationToken = cancellationToken;

            var p = PlaylistSettings.Parameters;

            _minimumRetry = p.MinimumRetry;
            _minimumReload = p.MinimumReload;
            _maximumReload = p.MaximumReload;
            _excessiveDuration = p.ExcessiveDuration;

            // ReSharper disable once PossiblyMistakenUseOfParamsMethod
            _abortTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);

            _isDynamicPlaylist = true;
            _readTask = new SignalTask(ReadSubList, _abortTokenSource.Token);

            _segmentsExpiration = Environment.TickCount;

            Playlist = new PlaylistEnumerable(this);
        }

        CancellationToken CancellationToken
        {
            get { return _abortTokenSource.Token; }
        }

        #region ISegmentManager Members

        public void Dispose()
        {
            if (0 != Interlocked.Exchange(ref _isDisposed, 1))
                return;

            _refreshTimer.Cancel();

            try
            {
                CleanupReader()
                    .Wait();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("PlaylistSegmentManager.Dispose() failed: " + ex.Message);
            }

            _refreshTimer.Dispose();
        }

        public Uri Url { get; private set; }
        public TimeSpan StartPosition { get; private set; }
        public TimeSpan? Duration { get; private set; }
        public IAsyncEnumerable<ISegment> Playlist { get; private set; }

        public Task StartAsync()
        {
            ThrowIfDisposed();

            _cancellationToken.ThrowIfCancellationRequested();

            SignalTask oldReadTask = null;
            CancellationTokenSource cancellationTokenSource = null;

            lock (_segmentLock)
            {
                if (_abortTokenSource.IsCancellationRequested)
                {
                    cancellationTokenSource = _abortTokenSource;

                    _abortTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);

                    oldReadTask = _readTask;

                    _readTask = new SignalTask(ReadSubList, _abortTokenSource.Token);
                }

                _isRunning = true;
            }

            return CleanupReader(oldReadTask, cancellationTokenSource);
        }

        public Task StopAsync()
        {
            // We would generally want to throw an ObjectDisposedException,
            // but here we are dealing with something along the lines of
            // Close() or even Dispose() itself.  Perhaps there should
            // be a separate CloseAsync() call?
            if (0 != _isDisposed)
                return TplTaskExtensions.CompletedTask;

            _refreshTimer.Cancel();

            SignalTask readTask;
            CancellationTokenSource cancellationTokenSource;

            lock (_segmentLock)
            {
                _isRunning = false;

                cancellationTokenSource = _abortTokenSource;
                readTask = _readTask;
            }

            return CleanupReader(readTask, cancellationTokenSource);
        }

        public async Task<TimeSpan> SeekAsync(TimeSpan timestamp)
        {
            ThrowIfDisposed();

            CancellationToken.ThrowIfCancellationRequested();

            if (_isDynamicPlaylist)
                await CheckReload(-1).ConfigureAwait(false);

            TimeSpan actualPosition;

            lock (_segmentLock)
            {
                Seek(timestamp);

                actualPosition = StartPosition;
            }

            return actualPosition;
        }

        #endregion

        async Task CleanupReader()
        {
            SignalTask readTask;
            CancellationTokenSource cancellationTokenSource;

            lock (_segmentLock)
            {
                readTask = _readTask;
                cancellationTokenSource = _abortTokenSource;
            }

            await CleanupReader(readTask, cancellationTokenSource).ConfigureAwait(false);

            lock (_segmentLock)
            {
                _readTask = null;
                _abortTokenSource = null;
            }
        }

        static async Task CleanupReader(SignalTask readTask, CancellationTokenSource cancellationTokenSource)
        {
            using (cancellationTokenSource)
            using (readTask)
            {
                if (null != cancellationTokenSource)
                {
                    try
                    {
                        if (!cancellationTokenSource.IsCancellationRequested)
                            cancellationTokenSource.Cancel();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("PlaylistSegmentManager.CleanupReader() cancel failed: " + ex.Message);
                    }
                }

                if (null != readTask)
                    await readTask.WaitAsync().ConfigureAwait(false);
            }
        }

        void ThrowIfDisposed()
        {
            if (0 != _isDisposed)
                throw new ObjectDisposedException(GetType().FullName);
        }

        void Seek(TimeSpan timestamp)
        {
            StartPosition = TimeSpan.Zero;

            _startSegmentIndex = _isDynamicPlaylist ? _dynamicStartIndex : -1;

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
            //Debug.WriteLine("PlaylistSegmentManager.CheckReload ({0})", DateTimeOffset.Now);

            CancellationToken.ThrowIfCancellationRequested();

            lock (_segmentLock)
            {
                if (_isInitialized && (!_isDynamicPlaylist || !_isRunning || _segmentsExpiration - Environment.TickCount > 0))
                    return TplTaskExtensions.CompletedTask;

                _readTask.Fire();

                if (null == _segments || index + 1 >= _segments.Length)
                    return _readTask.WaitAsync();
            }

            return TplTaskExtensions.CompletedTask;
        }

        async Task<M3U8Parser> FetchPlaylist(IEnumerable<Uri> urls)
        {
            foreach (var playlist in urls)
            {
                if (null == _subPlaylistRequest || _subPlaylistRequest.Url != playlist)
                    _subPlaylistRequest = _webRequestFactory(playlist);

                CancellationToken.ThrowIfCancellationRequested();

                var parsedPlaylist = await _subPlaylistRequest.ReadAsync(
                    bytes =>
                    {
                        CancellationToken.ThrowIfCancellationRequested();

                        if (bytes.Length < 1)
                            return null;

                        var parser = new M3U8Parser();

                        using (var ms = new MemoryStream(bytes))
                        {
                            parser.Parse(_subPlaylistRequest.RequestUri, ms);
                        }

                        return parser;
                    }, CancellationToken)
                                                              .ConfigureAwait(false);

                if (null != parsedPlaylist)
                    return parsedPlaylist;
            }

            return null;
        }

        async Task ReadSubList()
        {
            Debug.WriteLine("PlaylistSegmentManager.ReadSubList ({0})", DateTimeOffset.Now);

            try
            {
                try
                {
                    _refreshTimer.Cancel();

                    if (await UpdatePlaylist().ConfigureAwait(false))
                    {
                        _readSubListFailureCount = 0;

                        if (_isDynamicPlaylist)
                        {
                            var remaining = TimeSpan.FromMilliseconds(_segmentsExpiration - Environment.TickCount);

                            if (remaining < _minimumRetry)
                            {
                                Debug.WriteLine("Expiration too short: " + remaining);

                                remaining = _minimumRetry;
                            }

                            CancellationToken.ThrowIfCancellationRequested();

                            _refreshTimer.SetTimer(() => _readTask.Fire(), remaining);
                        }

                        return;
                    }
                }
                catch (OperationCanceledException)
                {
                    // Normal, ignore it.
                    return;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("PlaylistSegmentManager.ReadSubList() failed: " + ex.Message);
                }

                if (++_readSubListFailureCount > 3)
                {
                    lock (_segmentLock)
                    {
                        _segments = null;
                        _isDynamicPlaylist = false;
                    }

                    return;
                }

                // Retry in a little while
                var delay = 1.0 + (1 << (2 * _readSubListFailureCount));

                delay += (delay / 2) * (GlobalPlatformServices.Default.GetRandomNumber() - 0.5);

                var timeSpan = TimeSpan.FromSeconds(delay);

                Debug.WriteLine("PlaylistSegmentManager.ReadSubList(): retrying update in " + timeSpan);

                CancellationToken.ThrowIfCancellationRequested();

                _refreshTimer.SetTimer(() => _readTask.Fire(), timeSpan);
            }
            finally
            {
                lock (_segmentLock)
                {
                    _isInitialized = true;
                    //_reader = null;
                }
            }
        }

        async Task<bool> UpdatePlaylist()
        {
            var programStream = _subProgram.Video;

            var start = DateTime.UtcNow;

            var parser = await FetchPlaylist(programStream.Urls).ConfigureAwait(false);

            var fetchElapsed = DateTime.UtcNow - start;

            if (null == parser)
            {
                Debug.WriteLine("PlaylistSegmentManager.UpdatePlaylist(): unable to fetch playlist");

                return false;
            }

            Url = parser.BaseUrl;

            var segments = _segmentsFactory(parser)
                .GetPlaylist()
                .ToArray();

            var segments0 = segments;

            var isDynamicPlayist = null == parser.GlobalTags.Tag(M3U8Tags.ExtXEndList);

            Duration = isDynamicPlayist ? null : GetDuration(segments);

            lock (_segmentLock)
            {
                if (!_isRunning)
                    return true;

                var needReload = false;

                if (isDynamicPlayist || _dynamicPlaylists.Count > 0)
                {
                    var lastPlaylist = _dynamicPlaylists.LastOrDefault();

                    if (segments.Length > 0)
                    {
                        if (null != lastPlaylist
                            && lastPlaylist[0].MediaSequence == segments[0].MediaSequence
                            && (segments[0].MediaSequence.HasValue || lastPlaylist[0].Url == segments[0].Url)
                            && lastPlaylist.Length == segments.Length)
                        {
                            // We are running out of playlist, but the server just gave us the
                            // same list as last time.
                            Debug.WriteLine("PlaylistSegmentManager.UpdatePlaylist(): need reload ({0})", DateTimeOffset.Now);

                            var expiration = Environment.TickCount + (int)(Math.Round(2 * _minimumRetry.TotalMilliseconds));

                            if (_segmentsExpiration < expiration)
                                _segmentsExpiration = expiration;

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
                        segments = ResyncSegments();

                        if (isDynamicPlayist)
                            UpdateDynamicPlaylistExpiration(segments0);
                    }
                }

                if (!needReload)
                    _segments = segments;

                _isDynamicPlaylist = isDynamicPlayist;
            }

            Debug.WriteLine("PlaylistSegmentManager.UpdatePlaylist: playlist {0} loaded with {1} entries in {2}. index: {3} dynamic: {4} expires: {5} ({6})",
                parser.BaseUrl, _segments.Length, fetchElapsed, _startSegmentIndex, isDynamicPlayist,
                isDynamicPlayist ? TimeSpan.FromMilliseconds(_segmentsExpiration - Environment.TickCount) : TimeSpan.Zero,
                DateTimeOffset.Now);

            // Is a race possible between our just-completed reload and the
            // reader's CheckReload?  (The expiration timer handles this...?)

            return true;
        }

        ISegment[] ResyncSegments()
        {
            var previousPlaylist = null as ISegment[];

            var lastLength = -1;

            foreach (var playlist in _dynamicPlaylists)
            {
                lastLength = playlist.Length;

                if (null == previousPlaylist)
                {
                    _segmentList.AddRange(playlist);

                    previousPlaylist = playlist;
                    continue;
                }

                var found = false;

                for (var i = 0; i < previousPlaylist.Length; ++i)
                {
                    if (previousPlaylist[i].MediaSequence.HasValue && previousPlaylist[i].MediaSequence == playlist[0].MediaSequence
                        || !previousPlaylist[i].MediaSequence.HasValue && previousPlaylist[i].Url == playlist[0].Url)
                    {
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
                    _segmentList.AddRange(playlist);

                previousPlaylist = playlist;
            }

            var segments = _segmentList.ToArray();
            _segmentList.Clear();

            // lastLength is the length of the most recent playlist.  We must
            // start inside this playlist.

            SetDynamicStartIndex(segments, segments.Length - lastLength - 1);

            return segments;
        }

        void SetDynamicStartIndex(IList<ISegment> segments, int notBefore)
        {
            if (notBefore < -1)
                notBefore = -1;

            _dynamicStartIndex = notBefore;

            // Don't start more than 60 seconds in the past.

            var duration = TimeSpan.FromSeconds(60);

            for (var i = segments.Count - 1; i > notBefore; --i)
            {
                if (!segments[i].Duration.HasValue)
                    break;

                duration -= segments[i].Duration.Value;

                if (duration <= TimeSpan.Zero)
                {
                    _dynamicStartIndex = i;

                    break;
                }
            }

            _startSegmentIndex = _dynamicStartIndex;
        }

        void UpdateDynamicPlaylistExpiration(IList<ISegment> segments)
        {
            var reloadDelay = GetDuration(segments, Math.Max(1, segments.Count - 4), segments.Count);

            if (!reloadDelay.HasValue)
            {
                var segmentCount = segments.Count - 1;

                if (segmentCount > 0)
                    reloadDelay = new TimeSpan(0, 0, 0, 3 * segmentCount);
                else
                    reloadDelay = TimeSpan.Zero;
            }

            var expire = reloadDelay.Value;

            if (expire < _minimumReload)
                expire = _minimumReload;
            else if (expire > _maximumReload)
                expire = _maximumReload;

            // We use the system uptime rather than DateTime.UtcNow to
            // avoid grief if there is a step in the system time.
            // TODO: Make sure the TickCount doesn't get confused by steps in the system time.
            var segmentsExpiration = Environment.TickCount;

            segmentsExpiration += (int)expire.TotalMilliseconds;

            _segmentsExpiration = segmentsExpiration;
        }

        TimeSpan? GetDuration(IEnumerable<ISegment> segments)
        {
            var duration = TimeSpan.Zero;

            foreach (var segment in segments)
            {
                if (!segment.Duration.HasValue)
                    return null;

                if (segment.Duration <= TimeSpan.Zero || segment.Duration > _excessiveDuration)
                    return null;

                duration += segment.Duration.Value;
            }

            return duration;
        }

        TimeSpan? GetDuration(IList<ISegment> segments, int start, int end)
        {
            var duration = TimeSpan.Zero;

            for (var i = start; i < end; ++i)
            {
                var segment = segments[i];

                if (!segment.Duration.HasValue)
                    return null;

                if (segment.Duration <= TimeSpan.Zero || segment.Duration > _excessiveDuration)
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
            ISegment[] _segments;

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
                    await _segmentManager.CheckReload(_index).ConfigureAwait(false);

                    bool isDynamicPlaylist;
                    ISegment[] segments;
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

                    await TaskEx.Delay(delay, _segmentManager.CancellationToken).ConfigureAwait(false);
                }
            }

            #endregion

            static int FindNewIndex(ISegment[] oldSegments, ISegment[] newSegments, int oldIndex)
            {
                // TODO: Use ISegment.MediaSequence where possible.
                if (null == oldSegments || oldIndex < 0 || oldIndex >= oldSegments.Length)
                    return -1;

                var url = oldSegments[oldIndex].Url;

                var index = FindIndexByUrl(url, newSegments);

                if (index >= 0)
                    return index;

                Debug.WriteLine("PlaylistSegmentManager.FindNewIndex(): playlist discontinuity, does not contain {0}", url);

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

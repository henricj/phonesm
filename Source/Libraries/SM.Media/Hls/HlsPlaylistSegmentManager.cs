// -----------------------------------------------------------------------
//  <copyright file="HlsPlaylistSegmentManager.cs" company="Henric Jungheim">
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
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SM.Media.Content;
using SM.Media.Playlists;
using SM.Media.Segments;
using SM.Media.Utility;
using SM.Media.Web;

namespace SM.Media.Hls
{
    public sealed class HlsPlaylistSegmentManager : ISegmentManager
    {
        readonly CancellationToken _cancellationToken;
        readonly List<ISegment[]> _dynamicPlaylists = new List<ISegment[]>();
        readonly TimeSpan _excessiveDuration;
        readonly TimeSpan _maximumReload;
        readonly TimeSpan _minimumReload;
        readonly TimeSpan _minimumRetry;
        readonly IPlatformServices _platformServices;
        readonly IProgramStream _programStream;
        readonly TaskTimer _refreshTimer = new TaskTimer();
        readonly List<ISegment> _segmentList = new List<ISegment>();
        readonly object _segmentLock = new object();
        CancellationTokenSource _abortTokenSource;
        int _dynamicStartIndex;
        int _isDisposed;
        bool _isDynamicPlaylist;
        bool _isInitialized;
        bool _isRunning;
        ContentType _playlistType;
        int _readSubListFailureCount;
        SignalTask _readTask;
        ISegment[] _segments;
        int _segmentsExpiration;
        int _startSegmentIndex = -1;

        public HlsPlaylistSegmentManager(IProgramStream programStream, ContentType playlistType, IPlatformServices platformServices, CancellationToken cancellationToken)
        {
            if (null == programStream)
                throw new ArgumentNullException("programStream");
            if (null == playlistType)
                throw new ArgumentNullException("playlistType");
            if (null == platformServices)
                throw new ArgumentNullException("platformServices");

            _programStream = programStream;
            _playlistType = playlistType;
            _platformServices = platformServices;
            _cancellationToken = cancellationToken;

            var p = HlsPlaylistSettings.Parameters;

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

        public IWebReader WebReader { get; private set; }
        public TimeSpan StartPosition { get; private set; }
        public TimeSpan? Duration { get; private set; }
        public ContentType ContentType { get; private set; }
        public IAsyncEnumerable<ISegment> Playlist { get; private set; }

        [SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "_readTask", Justification = "CleanupReader does dispose _readTask")]
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

        public async Task StartAsync()
        {
            ThrowIfDisposed();

            _cancellationToken.ThrowIfCancellationRequested();

            SignalTask oldReadTask = null;
            CancellationTokenSource cancellationTokenSource = null;

            lock (_segmentLock)
            {
                if (_isRunning)
                    return;

                if (_abortTokenSource.IsCancellationRequested)
                {
                    cancellationTokenSource = _abortTokenSource;

                    // ReSharper disable once PossiblyMistakenUseOfParamsMethod
                    _abortTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);

                    oldReadTask = _readTask;

                    _readTask = new SignalTask(ReadSubList, _abortTokenSource.Token);
                }

                _isRunning = true;
            }

            await CleanupReader(oldReadTask, cancellationTokenSource).ConfigureAwait(false);

            if (null != _programStream.Segments && _programStream.Segments.Count > 0)
                UpdatePlaylist();

            var segment = await Playlist.FirstOrDefaultAsync().ConfigureAwait(false);

            if (null == segment)
            {
                Debug.WriteLine("PlaylistSegmentManager.StartAsync() no segments found");

                throw new FileNotFoundException("Unable to find the first segment");
            }

            ContentType = await _programStream.GetContentTypeAsync(_cancellationToken).ConfigureAwait(false);

            WebReader = _programStream.WebReader.CreateChild(null, ContentKind.AnyMedia, ContentType);
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

        public Task CloseAsync()
        {
            return StopAsync();
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

                var duration = GetSaneDuration(segment.Duration);

                if (!duration.HasValue)
                    break;

                if (seekTime + duration.Value > timestamp)
                {
                    StartPosition = seekTime;
                    _startSegmentIndex = i - 1;

                    return;
                }

                seekTime += duration.Value;
            }

            StartPosition = seekTime;
            _startSegmentIndex = _segments.Length;
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

        async Task ReadSubList()
        {
            Debug.WriteLine("PlaylistSegmentManager.ReadSubList({0})", DateTimeOffset.Now);

            try
            {
                _refreshTimer.Cancel();

                var start = DateTime.UtcNow;

                await _programStream.RefreshPlaylistAsync(_cancellationToken).ConfigureAwait(false);

                var fetchElapsed = DateTime.UtcNow - start;

                Debug.WriteLine("PlaylistSegmentManager.ReadSubList() refreshed playlist in " + fetchElapsed);

                if (UpdatePlaylist())
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

            delay += (delay / 2) * (_platformServices.GetRandomNumber() - 0.5);

            var timeSpan = TimeSpan.FromSeconds(delay);

            Debug.WriteLine("PlaylistSegmentManager.ReadSubList(): retrying update in " + timeSpan);

            CancellationToken.ThrowIfCancellationRequested();

            _refreshTimer.SetTimer(() => _readTask.Fire(), timeSpan);
        }

        bool UpdatePlaylist()
        {
            var segments = _programStream.Segments.ToArray();

            var isDynamicPlaylist = _programStream.IsDynamicPlaylist;

            Duration = isDynamicPlaylist ? null : GetDuration(segments);

            lock (_segmentLock)
            {
                if (!_isRunning)
                    return true;

                UnlockedUpdatePlaylist(isDynamicPlaylist, segments);

                _isDynamicPlaylist = isDynamicPlaylist;

                _isInitialized = true;
            }

            Debug.WriteLine("PlaylistSegmentManager.UpdatePlaylist: playlist {0} loaded with {1} entries. index: {2} dynamic: {3} expires: {4} ({5})",
                _programStream.WebReader, _segments.Length, _startSegmentIndex, isDynamicPlaylist,
                isDynamicPlaylist ? TimeSpan.FromMilliseconds(_segmentsExpiration - Environment.TickCount) : TimeSpan.Zero,
                DateTimeOffset.Now);

            // Is a race possible between our just-completed reload and the
            // reader's CheckReload?  (The expiration timer handles this...?)

            return true;
        }

        void UnlockedUpdatePlaylist(bool isDynamicPlaylist, ISegment[] segments)
        {
            var segments0 = segments;

            var needReload = false;

            if (isDynamicPlaylist || _dynamicPlaylists.Count > 0)
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

                    if (isDynamicPlaylist)
                        UpdateDynamicPlaylistExpiration(segments0);
                }
            }

            if (!needReload)
                _segments = segments;
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
            if (notBefore < -1 || notBefore > segments.Count - 2)
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
                    _dynamicStartIndex = Math.Min(i, segments.Count - 2);

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
                var segmentDuration = GetSaneDuration(segment.Duration);

                if (!segmentDuration.HasValue)
                    return null;

                duration += segmentDuration.Value;
            }

            return duration;
        }

        TimeSpan? GetSaneDuration(TimeSpan? duration)
        {
            if (!duration.HasValue)
                return null;

            if (duration.Value <= TimeSpan.Zero || duration.Value >= _excessiveDuration)
                return null;

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
            readonly HlsPlaylistSegmentManager _segmentManager;

            public PlaylistEnumerable(HlsPlaylistSegmentManager segmentManager)
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
            readonly HlsPlaylistSegmentManager _segmentManager;
            int _index = -1;
            ISegment[] _segments;

            public PlaylistEnumerator(HlsPlaylistSegmentManager segmentManager)
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
                Debug.Assert(null != newSegments);

                if (null == oldSegments || oldIndex < 0 || oldIndex >= oldSegments.Length)
                    return -1;

                if (newSegments.Length < 1)
                    return -1;

                var oldSegment = oldSegments[oldIndex];

                var mediaSequence = oldSegment.MediaSequence;

                if (mediaSequence.HasValue)
                {
                    var index = FindIndexByMediaSequence(mediaSequence.Value, newSegments);

                    if (index >= 0)
                        return index;
                }

                var url = oldSegment.Url;

                var urlIndex = FindIndexByUrl(url, newSegments);

                if (urlIndex >= 0)
                    return urlIndex;

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

            static int FindIndexByMediaSequence(long mediaSequence, IList<ISegment> segments)
            {
                var firstMediaSequence = segments[0].MediaSequence;

                if (!firstMediaSequence.HasValue)
                    return -1;

                if (mediaSequence < firstMediaSequence.Value)
                    return -1;

                var offset = (int)(mediaSequence - firstMediaSequence.Value);

                if (offset >= segments.Count)
                    return -1;

                if (segments[offset].MediaSequence == mediaSequence)
                    return offset;

                return -1;
            }
        }

        #endregion
    }
}

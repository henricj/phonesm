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
        bool _isDynamicPlayist;
        Task _reReader;
        Task _reader;
        int _segmentIndex;
        SubStreamSegment[] _segments;
        ICachedWebRequest _subPlaylistRequest;
        int _segmentsExpiration;

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

            _isDynamicPlayist = true;
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

        public async Task<ISegment> NextAsync()
        {
            for (; ; )
            {
                if (_isDynamicPlayist)
                    await CheckReload().ConfigureAwait(false);

                lock (_segmentLock)
                {
                    if (null != _segments && _segmentIndex + 1 < _segments.Length)
                        return _segments[++_segmentIndex];
                }

                // We seem to have run out of playlist.  If this is not
                // a dynamic playlist, then we are done.

                if (!_isDynamicPlayist)
                    return null;

                await TaskEx.Delay(5000);
            }
        }

        public async Task<TimeSpan> SeekAsync(TimeSpan timestamp)
        {
            if (_isDynamicPlayist)
                await CheckReload().ConfigureAwait(false);

            var actualPosition = Seek(timestamp);

            StartPosition = actualPosition;

            return actualPosition;
        }

        #endregion

        TimeSpan Seek(TimeSpan timestamp)
        {
            if (null == _segments || _segments.Length < 1)
                return TimeSpan.Zero;

            InitializeSegmentIndex();

            if (TimeSpan.Zero < timestamp)
            {
                var seekTime = TimeSpan.Zero;

                for (var i = 0; i < _segments.Length; ++i)
                {
                    var segment = _segments[i];

                    if (!segment.Duration.HasValue)
                        break;

                    if (seekTime + segment.Duration > timestamp)
                    {
                        _segmentIndex = i - 1;
                        return seekTime;
                    }

                    seekTime += segment.Duration.Value;
                }
            }

            return TimeSpan.Zero;
        }

        void InitializeSegmentIndex()
        {
            var segmentIndex = -1;

            if (_isDynamicPlayist)
            {
                // We don't want to start with the first segment in case
                // we need to buffer.  We don't want to start too far into
                // the playlist since this would mean reloading the thing
                // too often.  With enough buffering, we will eventually 
                // miss segments.  To get that working properly, we must
                // adjust the sample timestamps since otherwise there will
                // be a discontinuity for MediaElement to choke on.
                segmentIndex = _segments.Length / 3 - 1;

                if (segmentIndex + 4 >= _segments.Length)
                    segmentIndex = _segments.Length - 5;

                if (segmentIndex < -1)
                    segmentIndex = -1;
            }

            _segmentIndex = segmentIndex;
        }

        Task CheckReload()
        {
            lock (_segmentLock)
            {
                if (null != _segments && _segmentIndex + 2 < _segments.Length && (!_isDynamicPlayist || _segmentsExpiration - Environment.TickCount > 0))
                    return TplTaskExtensions.CompletedTask;

                if (null != _reReader)
                    return _reReader;

                _reReader = Task.Factory.StartNew((Func<Task>)ReadSubList).Unwrap();

                if (null == _segments || _segmentIndex + 1 >= _segments.Length)
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
                    _isDynamicPlayist = false;

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
                var oldSegmentIndex = _segmentIndex;

                _segments = segments;
                _isDynamicPlayist = isDynamicPlayist;

                InitializeSegmentIndex();

                if (null != oldSegments && oldSegmentIndex >= 0 && oldSegmentIndex < oldSegments.Length)
                {
                    var url = oldSegments[oldSegmentIndex].Url;

                    var index = FindIndexByUrl(url);

                    if (index > 0)
                        _segmentIndex = index;
                    else
                        Debug.WriteLine("PlaylistSegmentManager.ReadSubList: playlist discontinuity {0} does not contain {1}", parser.BaseUrl, url);
                }

                if (isDynamicPlayist)
                {
                    var duration = GetDuration(_segmentIndex > 0 ? segments.Skip(_segmentIndex) : _segments);

                    // We use the system uptime rather than DateTime.UtcNow to
                    // avoid grief if there is a step in the system time.
                    // TODO: Make sure the TickCount doesn't get confused by steps in the system time.
                    var segmentsExpiration = Environment.TickCount;

                    if (duration.HasValue)
                        segmentsExpiration += (int)(duration.Value.TotalMilliseconds * 0.90);
                    else
                        segmentsExpiration += 5000 * segments.Length;

                    _segmentsExpiration = segmentsExpiration;
                }

                Debug.WriteLine("PlaylistSegmentManager.ReadSubList: playlist {0} loaded with {1} entries. index: {2} dynamic: {3} expires: {4}",
                    parser.BaseUrl, _segments.Length, _segmentIndex, isDynamicPlayist,
                    isDynamicPlayist ? TimeSpan.FromMilliseconds( _segmentsExpiration - Environment.TickCount) : TimeSpan.Zero);

                _reReader = null;
            }
        }

        int FindIndexByUrl(Uri url)
        {
            for (var i = 0; i < _segments.Length; ++i)
            {
                if (url == _segments[i].Url)
                    return i;
            }

            return -1;
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
    }
}

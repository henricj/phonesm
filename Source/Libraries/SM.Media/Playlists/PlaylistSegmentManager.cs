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
        readonly Uri[] _playlistUrls;
        readonly Func<IProgramManager> _programManagerFactory;
        readonly object _segmentLock = new object();
        readonly Func<Uri, IWebRequest> _webRequestFactory;
        bool _isDynamicPlayist;
        Task _reReader;
        Task _reader;
        int _segmentIndex;
        SubStreamSegment[] _segments;
        IWebRequest _subPlaylistRequest;
        PlaylistSubProgramBase _subProgram;

        public PlaylistSegmentManager(Uri playlist, Func<Uri, IWebRequest> webRequestFactory, Func<IProgramManager> programManagerFactory)
            : this(playlist, webRequestFactory, programManagerFactory, CancellationToken.None)
        { }

        public PlaylistSegmentManager(Uri playlist, Func<Uri, IWebRequest> webRequestFactory, Func<IProgramManager> programManagerFactory, CancellationToken cancellationToken)
            : this(new[] { playlist }, webRequestFactory, programManagerFactory, cancellationToken)
        { }

        public PlaylistSegmentManager(IEnumerable<Uri> playlistUrls, Func<Uri, IWebRequest> webRequestFactory, Func<IProgramManager> programManagerFactory, CancellationToken cancellationToken)
        {
            _playlistUrls = playlistUrls.ToArray();
            _webRequestFactory = webRequestFactory;
            _programManagerFactory = programManagerFactory;

            _abortTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        CancellationToken CancellationToken
        {
            get { return _abortTokenSource.Token; }
        }

        #region IDisposable Members

        public void Dispose()
        {
            _abortTokenSource.Cancel();

            try
            {
                WaitLoad().Wait();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("PlaylistSegmentManager: {0}", ex.Message);
            }
        }

        #endregion

        #region ISegmentManager Members

        public async Task<Segment> NextAsync()
        {
            if (_isDynamicPlayist)
                await CheckReload().ConfigureAwait(false);

            if (_segmentIndex + 1 >= _segments.Length)
            {
                _segmentIndex = _segments.Length;
                return null;
            }

            return _segments[++_segmentIndex];
        }

        public async Task<TimeSpan> SeekAsync(TimeSpan timestamp)
        {
            await WaitLoad();

            return Seek(timestamp);
        }

        #endregion

        Task WaitLoad()
        {
            lock (_segmentLock)
            {
                if (null == _reader)
                    _reader = Task.Factory
                                  .StartNew((Func<Task>)Reader)
                                  .Unwrap();

                return _reader;
            }
        }

        public TimeSpan Seek(TimeSpan timestamp)
        {
            if (null == _segments || _segments.Length < 1)
                return TimeSpan.Zero;

            _segmentIndex = -1;

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

        public Segment Next()
        {
            return NextAsync().Result;
        }

        public async Task<IEnumerable<IProgram>> LoadProgramsAsync()
        {
            await WaitLoad().ConfigureAwait(false);

            return null;
        }

        Task CheckReload()
        {
            if (_segmentIndex + 3 < _segments.Length)
                return TplTaskExtensions.CompletedTask;

            lock (_segmentLock)
            {
                if (null != _reReader)
                    return _reReader;

                return _reReader = Task.Factory.StartNew((Func<Task>)ReadSubList).Unwrap();
            }
        }

        async Task Reader()
        {
            var playlists = _playlistUrls;

            IDictionary<long, Program> programs;

            using (var pmb = _programManagerFactory())
            {
                programs = await pmb.LoadAsync(playlists, CancellationToken);
            }

            var program = programs.Values.FirstOrDefault();

            if (null == program)
                return;

            _subProgram = program.SubPrograms.OfType<PlaylistSubProgramBase>()
                                 .OrderByDescending(p => p.Bandwidth)
                                 .FirstOrDefault();

            if (null == _subProgram)
                return;

            await ReadSubList();
        }

        async Task ReadSubList()
        {
            var parser = new M3U8Parser();

            //await parser.ParseAsync(_subProgram.Playlist, _cancellationToken);

            if (null == _subPlaylistRequest)
                _subPlaylistRequest = _webRequestFactory(_subProgram.Playlist);

            var playlistString = default(string);

            await _subPlaylistRequest.ReadAsync(
                async s =>
                {
                    using (var sr = new StreamReader(s))
                    {
                        playlistString = await sr.ReadToEndAsync();
                    }
                });

            using (var tr = new StringReader(playlistString))
            {
                parser.Parse(tr);
            }

            var segments = _subProgram.GetPlaylist(parser).ToArray();

            var isDynamicPlayist = null == parser.GlobalTags.Tag(M3U8Tags.ExtXEndList);

            lock (_segmentLock)
            {
                if (null != _segments && _segmentIndex >= 0 && _segmentIndex < _segments.Length)
                {
                    var url = _segments[_segmentIndex].Url;

                    _segmentIndex = 0;

                    for (var i = 0; i < segments.Length; ++i)
                    {
                        if (url == segments[i].Url)
                        {
                            _segmentIndex = i;
                            break;
                        }
                    }
                }

                _segments = segments;
                _isDynamicPlayist = isDynamicPlayist;

                _reReader = null;
            }
        }
    }
}

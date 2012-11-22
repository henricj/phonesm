//-----------------------------------------------------------------------
// <copyright file="PlaylistSegmentManager.cs" company="Henric Jungheim">
// Copyright (c) 2012.
// <author>Henric Jungheim</author>
// </copyright>
//-----------------------------------------------------------------------
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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SM.Media.M3U8;
using SM.Media.Playlists;
using SM.Media.Segments;

namespace SM.Media.Playlists
{
    public class PlaylistSegmentManager : ISegmentManager, IAsyncLoadTask, IDisposable
    {
        readonly Uri _playlist;
        readonly CancellationToken _cancellationToken;
        Task _reader;
        Task _reReader;
        SubStreamSegment[] _segments;
        int _segmentIndex;
        readonly object _segmentLock = new object();
        bool _dynamicPlayist;
        PlaylistSubProgramBase _subProgram;
        CachedWebRequest _subPlaylistRequest;

        public PlaylistSegmentManager(Uri playlist)
            : this(playlist, CancellationToken.None)
        { }

        public PlaylistSegmentManager(Uri playlist, CancellationToken cancellationToken)
        {
            _playlist = playlist;
            _cancellationToken = cancellationToken;
        }

        public void Dispose()
        { }

        public Segment Next()
        {
            if (_segmentIndex + 1 >= _segments.Length)
            {
                _segmentIndex = _segments.Length;
                return null;
            }

            if (_dynamicPlayist)
                CheckReload();

            return _segments[++_segmentIndex];
        }

        public Segment Seek(TimeSpan timestamp)
        {
            if (null == _segments || _segments.Length < 1)
                return null;

            _segmentIndex = 0;

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
                        _segmentIndex = i;
                        break;
                    }
                }
            }

            return _segments[_segmentIndex];
        }

        void CheckReload()
        {
            if (_segmentIndex + 3 < _segments.Length)
                return;

            lock (_segmentLock)
            {
                if (null != _reReader)
                    return;

                _reReader = Task.Factory.StartNew((Func<Task>)ReadSubList).Unwrap();
            }
        }

        async Task Reader()
        {
            var playlist = _playlist;

            IDictionary<long, Program> programs;

            using (var pmb = new ProgramManager())
            {
                programs = await pmb.LoadAsync(playlist, _cancellationToken);
            }

            var program = programs.Values.FirstOrDefault();

            if (null == program)
                return;

            _subProgram = program.SubPrograms.OfType<PlaylistSubProgramBase>().FirstOrDefault();

            if (null == _subProgram)
                return;

            await ReadSubList();
        }

        async Task ReadSubList()
        {
            var parser = new M3U8Parser();

            //await parser.ParseAsync(_subProgram.Playlist, _cancellationToken);

            if (null == _subPlaylistRequest)
                _subPlaylistRequest = new CachedWebRequest(_subProgram.Playlist);

            await _subPlaylistRequest.Read(s =>
                                           {
                                               using (var sr = new StreamReader(s))
                                               {
                                                   parser.Parse(sr);
                                               }
                                           });

            var segments = _subProgram.GetPlaylist(parser).ToArray();

            var dynamicPlayist = null == parser.GlobalTags.Tag(M3U8Tags.ExtXEndList);

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
                _dynamicPlayist = dynamicPlayist;

                _reReader = null;
            }
        }

        public Task WaitLoad()
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
    }
}

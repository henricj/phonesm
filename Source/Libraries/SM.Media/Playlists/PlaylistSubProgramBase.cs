// -----------------------------------------------------------------------
//  <copyright file="PlaylistSubProgramBase.cs" company="Henric Jungheim">
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
using SM.Media.M3U8;

namespace SM.Media.Playlists
{
    public class ProgramStream : IProgramStream
    {
        #region IProgramStream Members

        public string StreamType { get; internal set; }
        public string Language { get; internal set; }
        public IEnumerable<Uri> Urls { get; internal set; }

        #endregion
    }

    public class PlaylistSubProgramBase : SubProgram
    {
        static readonly IEnumerable<SubStreamSegment> NoEntries = new SubStreamSegment[0];
        readonly IProgramStream _video;

        public PlaylistSubProgramBase(IProgramStream video)
        {
            _video = video;
        }

        public Uri Playlist { get; set; }

        //protected abstract M3U8Parser Parse(Uri playlist);

        public override IProgramStream Audio
        {
            get { throw new NotImplementedException(); }
        }

        public override IProgramStream Video
        {
            get { return _video; }
        }

        public override ICollection<IProgramStream> AlternateAudio
        {
            get { throw new NotImplementedException(); }
        }

        public override ICollection<IProgramStream> AlternateVideo
        {
            get { throw new NotImplementedException(); }
        }

        public override IEnumerable<SubStreamSegment> GetPlaylist(SubStream video = null, SubStream audio = null)
        {
            var playlist = Playlist;

            //if (null == playlist)
            return NoEntries;

            M3U8Parser parser = null; // Parse(playlist);

            return GetPlaylist(parser);
        }

        public static IEnumerable<SubStreamSegment> GetPlaylist(M3U8Parser parser)
        {
            Uri previous = null;

            foreach (var p in parser.Playlist)
            {
                var url = parser.ResolveUrl(p.Uri);

                if (null != previous && url == previous)
                    continue;

                var segment = new SubStreamSegment(url);

                var info = M3U8Tags.ExtXInf.Find(p.Tags);

                if (null != info)
                    segment.Duration = TimeSpan.FromSeconds((double)info.Duration);

                yield return segment;

                previous = url;
            }
        }
    }
}

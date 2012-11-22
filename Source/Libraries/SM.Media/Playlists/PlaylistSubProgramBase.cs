//-----------------------------------------------------------------------
// <copyright file="PlaylistSubProgramBase.cs" company="Henric Jungheim">
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
using SM.Media.M3U8;

namespace SM.Media.Playlists
{
    public class PlaylistSubProgramBase : SubProgram
    {
        static readonly IEnumerable<SubStreamSegment> NoEntries = new SubStreamSegment[0];
        public Uri Playlist { get; set; }

        //protected abstract M3U8Parser Parse(Uri playlist);

        public override IEnumerable<SubStreamSegment> GetPlaylist(SubStream audio = null)
        {
            var playlist = Playlist;

            //if (null == playlist)
            return NoEntries;

            M3U8Parser parser = null;// Parse(playlist);

            return GetPlaylist(parser);
        }

        public IEnumerable<SubStreamSegment> GetPlaylist(M3U8Parser parser)
        {
            Uri previous = null;

            foreach (var p in parser.Playlist)
            {
                var url = new Uri(Playlist, new Uri(p.Uri, UriKind.RelativeOrAbsolute));

                if (null != previous && url == previous)
                    continue;

                var segment = new SubStreamSegment(url);

                var info = p.Tags.Tag(M3U8Tags.ExtXInf);

                if (null != info)
                    segment.Duration = TimeSpan.FromSeconds((double)info.Duration);

                yield return segment;

                previous = url;
            }
        }
    }
}

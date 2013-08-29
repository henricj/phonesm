// -----------------------------------------------------------------------
//  <copyright file="PlaylistSubProgramBase.cs" company="Henric Jungheim">
//  Copyright (c) 2012, 2013.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012, 2013 Henric Jungheim <software@henric.org>
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
            var lastOffset = 0L;

            var mediaSequence = M3U8Tags.ExtXMediaSequence.GetValue<long>(parser.GlobalTags);
            var index = 0;

            foreach (var p in parser.Playlist)
            {
                var segment = StreamSegments.CreateStreamSegment(parser, p);

                if (mediaSequence.HasValue)
                    segment.MediaSequence = mediaSequence + index++;

                if (null != p.Tags && 0 != p.Tags.Length)
                {
                    var byteRange = M3U8Tags.ExtXByteRange.Find(p.Tags);

                    if (null != byteRange)
                    {
                        if (byteRange.Offset.HasValue)
                        {
                            segment.Offset = byteRange.Offset.Value;
                            lastOffset = byteRange.Offset.Value;
                        }
                        else
                            segment.Offset = lastOffset;

                        segment.Length = byteRange.Length;
                        lastOffset += byteRange.Length;
                    }
                }

                yield return segment;
            }
        }
    }
}

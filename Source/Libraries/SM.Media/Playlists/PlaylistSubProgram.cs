// -----------------------------------------------------------------------
//  <copyright file="PlaylistSubProgram.cs" company="Henric Jungheim">
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

namespace SM.Media.Playlists
{
    public class PlaylistSubProgram : SubProgram
    {
        readonly IProgramStream _video;

        public PlaylistSubProgram(IProgram program, IProgramStream video)
            : base(program)
        {
            _video = video;
        }

        public Uri Playlist { get; set; }

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

        public override string ToString()
        {
            return string.Format("{0} {1}", null == Playlist ? "<none>" : Playlist.ToString(), base.ToString());
        }
    }
}

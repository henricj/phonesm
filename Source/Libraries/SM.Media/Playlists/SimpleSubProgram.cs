// -----------------------------------------------------------------------
//  <copyright file="SimpleSubProgram.cs" company="Henric Jungheim">
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
using System.Globalization;
using SM.Media.Segments;

namespace SM.Media.Playlists
{
    class SimpleSubProgram : SubProgram, IProgramStream
    {
        static readonly IProgramStream[] NoStreams = new IProgramStream[0];
        readonly Uri[] _playlistUrl;
        readonly ICollection<ISegment> _segments = new List<ISegment>();

        public SimpleSubProgram(IProgram program, Uri playlistUrl)
            : base(program)
        {
            _playlistUrl = new[] { playlistUrl };
        }

        public ICollection<ISegment> Segments
        {
            get { return _segments; }
        }

        public override IProgramStream Audio
        {
            get { return this; }
        }

        public override IProgramStream Video
        {
            get { return this; }
        }

        public override ICollection<IProgramStream> AlternateAudio
        {
            get { return NoStreams; }
        }

        public override ICollection<IProgramStream> AlternateVideo
        {
            get { return NoStreams; }
        }

        #region IProgramStream Members

        public string StreamType
        {
            get { return "unknown"; }
        }

        public string Language
        {
            get { return CultureInfo.InvariantCulture.TwoLetterISOLanguageName; }
        }

        public ICollection<Uri> Urls
        {
            get { return _playlistUrl; }
        }

        #endregion
    }
}

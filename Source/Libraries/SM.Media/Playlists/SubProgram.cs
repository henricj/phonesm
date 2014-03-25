// -----------------------------------------------------------------------
//  <copyright file="SubProgram.cs" company="Henric Jungheim">
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
using SM.Media.Hls;

namespace SM.Media.Playlists
{
    public interface ISubProgram
    {
        IProgram Program { get; }

        int? Height { get; }
        int? Width { get; }

        TimeSpan? Duration { get; }

        long Bandwidth { get; }

        IProgramStream Audio { get; }
        IProgramStream Video { get; }

        ICollection<IProgramStream> AlternateAudio { get; }
        ICollection<IProgramStream> AlternateVideo { get; }
    }

    public abstract class SubProgram : ISubProgram
    {
        readonly IProgram _program;

        protected SubProgram(IProgram program)
        {
            if (null == program)
                throw new ArgumentNullException("program");

            _program = program;
        }

        public HlsProgramManager.MediaGroup AudioGroup { get; set; }

        #region ISubProgram Members

        public IProgram Program
        {
            get { return _program; }
        }

        public int? Height
        {
            get { return null; }
        }

        public int? Width
        {
            get { return null; }
        }

        public long Bandwidth { get; set; }

        public abstract IProgramStream Audio { get; }
        public abstract IProgramStream Video { get; }
        public abstract ICollection<IProgramStream> AlternateAudio { get; }
        public abstract ICollection<IProgramStream> AlternateVideo { get; }

        public TimeSpan? Duration
        {
            get { return null; }
        }

        #endregion

        public override string ToString()
        {
            return string.Format("{0:F3} Mbit/s from {1}", Bandwidth * (1.0 / (1000 * 1000)), _program.PlaylistUrl);
        }
    }
}

// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Henric Jungheim">
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
using System.Linq;

namespace SM.Media.Playlists
{
    public interface IProgramStream
    {
        string StreamType { get; }
        string Language { get; }
    }

    public interface ISubProgram
    {
        int? Height { get; }
        int? Width { get; }

        TimeSpan? Duration { get; }

        long Bandwidth { get; }

        IProgramStream Audio { get; }
        IProgramStream Video { get; }

        ICollection<IProgramStream> AlternateAudio { get; }
        ICollection<IProgramStream> AlternateVideo { get; }
    }

    public interface IProgram
    {
        ICollection<ISubProgram> SubPrograms { get; }
    }


    public class Program : IProgram
    {
        readonly ICollection<ISubProgram> _subPrograms = new List<ISubProgram>();

        public long ProgramId { get; set; }

        #region IProgram Members

        public ICollection<ISubProgram> SubPrograms
        {
            get { return _subPrograms; }
        }

        #endregion
    }
}

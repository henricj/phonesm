// -----------------------------------------------------------------------
//  <copyright file="ProgramStreams.cs" company="Henric Jungheim">
//  Copyright (c) 2012-2015.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012-2015 Henric Jungheim <software@henric.org>
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

using System.Collections.Generic;

namespace SM.Media.TransportStream.TsParser
{
    public interface IProgramStream
    {
        uint Pid { get; }
        TsStreamType StreamType { get; }
        bool BlockStream { get; set; }
        string Language { get; }
    }

    public interface IProgramStreams
    {
        int ProgramNumber { get; }
        string Language { get; }
        ICollection<IProgramStream> Streams { get; }
    }

    class ProgramStreams : IProgramStreams
    {
        #region IProgramStreams Members

        public string Language { get; set; }

        public int ProgramNumber { get; internal set; }

        public ICollection<IProgramStream> Streams { get; internal set; }

        #endregion

        #region Nested type: ProgramStream

        public class ProgramStream : IProgramStream
        {
            #region IProgramStream Members

            public string Language { get; set; }

            public uint Pid { get; internal set; }

            public TsStreamType StreamType { get; internal set; }

            public bool BlockStream { get; set; }

            #endregion
        }

        #endregion
    }
}

// -----------------------------------------------------------------------
//  <copyright file="WaveFormatEx.cs" company="Henric Jungheim">
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

using System.Collections.Generic;

namespace SM.Media.Mmreg
{
    // http://msdn.microsoft.com/en-us/library/windows/hardware/ff538799(v=vs.85).aspx
    // Also see WAVEFORMATEX in Microsoft's mmreg.h
    public class WaveFormatEx
    {
        #region WaveFormatTag enum

        public enum WaveFormatTag : ushort
        {
            RawAac1 = 0x00ff,
            Mpeg = 0x0050,
            MpegLayer3 = 0x0055,
            FraunhoferIisMpeg2Aac = 0x0180,
            AdtsAac = 0x1600,
            RawAac = 0x1601,
            HeAac = 0x1610,
            Mpeg4Aac = 0xa106
        }

        #endregion

        public uint nAvgBytesPerSec;
        public ushort nBlockAlign;
        public ushort nChannels;
        public uint nSamplesPerSec;
        public ushort wBitsPerSample;
        public ushort wFormatTag;

        /// <summary>
        ///     The number of bytes after WAVEFORMATEX.
        /// </summary>
        public virtual ushort cbSize
        {
            get { return 0; }
        }

        public virtual void ToBytes(IList<byte> buffer)
        {
            buffer.AddLe(wFormatTag);
            buffer.AddLe(nChannels);
            buffer.AddLe(nSamplesPerSec);
            buffer.AddLe(nAvgBytesPerSec);
            buffer.AddLe(nBlockAlign);
            buffer.AddLe(wBitsPerSample);
            buffer.AddLe(cbSize);
        }
    }
}

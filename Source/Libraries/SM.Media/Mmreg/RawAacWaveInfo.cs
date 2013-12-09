// -----------------------------------------------------------------------
//  <copyright file="RawAacWaveInfo.cs" company="Henric Jungheim">
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

namespace SM.Media.Mmreg
{
    /// <summary>
    ///     Extend WaveFormatEx by appending an AudioSpecificConfig
    /// </summary>
    // http://wiki.multimedia.cx/index.php?title=MPEG-4_Audio#Audio_Specific_Config
    public class RawAacWaveInfo : WaveFormatEx
    {
        public RawAacWaveInfo()
        {
            wFormatTag = (ushort)WaveFormatTag.RawAac1;
        }

        public override ushort cbSize
        {
            get { return (ushort)(base.cbSize + 2); }
        }

        // TODO: Range check these things...
        public int ObjectType { get; set; }
        public int FrequencyIndex { get; set; }
        public int ChannelConfiguration { get; set; }

        public override void ToBytes(IList<byte> buffer)
        {
            base.ToBytes(buffer);

            if (31 == ObjectType)
                throw new NotImplementedException("Extended object types are not supported");

            if (15 == FrequencyIndex)
                throw new NotImplementedException("Unsupported frequency");

            var v = (ushort)((ObjectType << 11) | (FrequencyIndex << 7) | (ChannelConfiguration << 3));

            buffer.Add((byte)(v >> 8));
            buffer.Add((byte)v);
        }
    }
}

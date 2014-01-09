// -----------------------------------------------------------------------
//  <copyright file="RawAacWaveInfo.cs" company="Henric Jungheim">
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

using System.Collections.Generic;

namespace SM.Media.Mmreg
{
    /// <summary>
    ///     Extend WaveFormatEx by appending an AudioSpecificConfig
    /// </summary>
    // http://wiki.multimedia.cx/index.php?title=MPEG-4_Audio#Audio_Specific_Config
    public class RawAacWaveInfo : WaveFormatEx
    {
        public ICollection<byte> pbAudioSpecificConfig;

        public RawAacWaveInfo()
        {
            wFormatTag = (ushort)WaveFormatTag.RawAac1;

            nBlockAlign = 4;
            wBitsPerSample = 16;
        }

        public override ushort cbSize
        {
            get
            {
                var size = base.cbSize;

                if (null != pbAudioSpecificConfig)
                    size += (ushort)pbAudioSpecificConfig.Count;

                return size;
            }
        }

        public override void ToBytes(IList<byte> buffer)
        {
            base.ToBytes(buffer);

            if (null == pbAudioSpecificConfig || pbAudioSpecificConfig.Count <= 0)
                return;

            foreach (var b in pbAudioSpecificConfig)
                buffer.Add(b);
        }
    }
}

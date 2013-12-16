//-----------------------------------------------------------------------
// <copyright file="HeAacWaveInfo.cs" company="Henric Jungheim">
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

using System.Collections.Generic;

namespace SM.Media.Mmreg
{
    // See the note in http://msdn.microsoft.com/en-us/library/ff426928(v=VS.96).aspx
    // We should use RawAac1 (0xff) instead of ADTS (0x1610).
    public class HeAacWaveInfo : WaveFormatEx
    {
        public enum PayloadType : ushort
        {
            Raw = 0,
            ADTS = 1,
            ADIF = 2,
            LOAS = 3
        }

        public override ushort cbSize
        {
            get
            {
                var size = base.cbSize + 12;

                if (null != pbAudioSpecificConfig)
                    size += pbAudioSpecificConfig.Count;

                return (ushort)size;
            }
        }

        public uint dwReserved2;
        public ushort wAudioProfileLevelIndication = 0xFE;
        public ushort wPayloadType = (ushort)PayloadType.ADTS;
        public ushort wReserved1;
        public ushort wStructType;
        public ICollection<byte> pbAudioSpecificConfig;

        public HeAacWaveInfo()
        {
            wFormatTag = (ushort)WaveFormatTag.HeAac;

            wBitsPerSample = 16;
            nBlockAlign = 1;
        }

        public override void ToBytes(IList<byte> buffer)
        {
            base.ToBytes(buffer);

            buffer.AddLe(wPayloadType);
            buffer.AddLe(wAudioProfileLevelIndication);
            buffer.AddLe(wStructType);
            buffer.AddLe(wReserved1);
            buffer.AddLe(dwReserved2);

            if (null == pbAudioSpecificConfig || pbAudioSpecificConfig.Count <= 0)
                return;

            foreach (var b in pbAudioSpecificConfig)
                buffer.Add(b);
        }
    }
}

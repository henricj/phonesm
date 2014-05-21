// -----------------------------------------------------------------------
//  <copyright file="Ac3FrameHeader.cs" company="Henric Jungheim">
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
using System.Diagnostics;
using System.Linq;
using SM.Media.Audio;

namespace SM.Media.Ac3
{
    sealed class Ac3FrameHeader : IAudioFrameHeader
    {
        internal static readonly TimeSpan FrameDuration = TimeSpan.FromMilliseconds(32);

        static readonly int[] SamplingFrequencyTable =
        {
            48000,
            44100,
            32000
        };

        static readonly Dictionary<byte, FrameCode> FrameCodes =
            new[]
            {
                new FrameCode(0, 32, 96, 69, 64),
                new FrameCode(1, 32, 96, 70, 64),
                new FrameCode(2, 40, 120, 87, 80),
                new FrameCode(3, 40, 120, 88, 80),
                new FrameCode(4, 48, 144, 104, 96),
                new FrameCode(5, 48, 144, 105, 96),
                new FrameCode(6, 56, 168, 121, 112),
                new FrameCode(7, 56, 168, 122, 112),
                new FrameCode(8, 64, 192, 139, 128),
                new FrameCode(9, 64, 192, 140, 128),
                new FrameCode(10, 80, 240, 174, 160),
                new FrameCode(11, 80, 240, 175, 160),
                new FrameCode(12, 96, 288, 208, 192),
                new FrameCode(13, 96, 288, 209, 192),
                new FrameCode(14, 112, 336, 243, 224),
                new FrameCode(15, 112, 336, 244, 224),
                new FrameCode(16, 128, 384, 278, 256),
                new FrameCode(17, 128, 384, 279, 256),
                new FrameCode(18, 160, 480, 348, 320),
                new FrameCode(19, 160, 480, 349, 320),
                new FrameCode(20, 192, 576, 417, 384),
                new FrameCode(21, 192, 576, 418, 384),
                new FrameCode(22, 224, 672, 487, 448),
                new FrameCode(23, 224, 672, 488, 448),
                new FrameCode(24, 256, 768, 557, 512),
                new FrameCode(25, 256, 768, 558, 512),
                new FrameCode(26, 320, 960, 696, 640),
                new FrameCode(27, 320, 960, 697, 640),
                new FrameCode(28, 384, 1152, 835, 768),
                new FrameCode(29, 384, 1152, 836, 768),
                new FrameCode(30, 448, 1344, 975, 896),
                new FrameCode(31, 448, 1344, 976, 896),
                new FrameCode(32, 512, 1536, 1114, 1024),
                new FrameCode(33, 512, 1536, 1115, 1024),
                new FrameCode(34, 576, 1728, 1253, 1152),
                new FrameCode(35, 576, 1728, 1254, 1152),
                new FrameCode(36, 640, 1920, 1393, 1280),
                new FrameCode(37, 640, 1920, 1394, 1280)
            }.ToDictionary(v => v.Code);

        public int Bitrate { get; private set; }

        #region IAudioFrameHeader Members

        public int SamplingFrequency { get; private set; }

        public int FrameLength { get; private set; }

        public int HeaderLength
        {
            get { return 5; }
        }

        public int HeaderOffset { get; private set; }

        public string Name { get; private set; }

        public TimeSpan Duration
        {
            get { return FrameDuration; }
        }

        public bool Parse(byte[] buffer, int index, int length, bool verbose = false)
        {
            // http://stnsoft.com/DVD/ac3hdr.html

            HeaderOffset = 0;

            var index0 = index;
            var lastIndex = index + length;

            if (length < 5)
                return false;

            for (; ; )
            {
                for (; ; )
                {
                    if (index + 5 > lastIndex)
                        return false;

                    var frameSync1 = buffer[index++];

                    if (0x0b == frameSync1)
                        break;
                }

                if (index + 4 > lastIndex)
                    return false;

                var frameSync2 = buffer[index++];

                if (0x77 == frameSync2)
                    break;
            }

            HeaderOffset = index - index0 - 2;

            if (HeaderOffset < 0)
                return false;

            var crc = buffer[index++] << 8;
            crc |= buffer[index++];

            var b4 = buffer[index++];

            var fscod = (b4 >> 6) & 0x03;

            SamplingFrequency = GetSamplingFrequency(fscod);

            if (SamplingFrequency <= 0)
                return false;

            var frmsizcod = (byte)(b4 & 0x3f);

            var frameCode = GetFrameCode(frmsizcod);

            if (null == frameCode)
                return false;

            Bitrate = 1000 * frameCode.Bitrate;
            FrameLength = frameCode.GetFrameSize(fscod);

            if (string.IsNullOrEmpty(Name))
                Name = string.Format("AC-3 {0}kHz", SamplingFrequency / 1000.0);

#if DEBUG
            if (verbose)
            {
                Debug.WriteLine("Configuration AC-3 sampling {0}kHz bitrate {1}kHz",
                    SamplingFrequency / 1000.0, Bitrate / 1000.0);
            }
#endif
            return true;
        }

        #endregion

        int GetSamplingFrequency(int samplingIndex)
        {
            if (samplingIndex < 0 || samplingIndex >= SamplingFrequencyTable.Length)
                return -1;

            return SamplingFrequencyTable[samplingIndex];
        }

        FrameCode GetFrameCode(byte frmsizcod)
        {
            FrameCode frameCode;

            if (!FrameCodes.TryGetValue(frmsizcod, out frameCode))
                return null;

            return frameCode;
        }

        #region Nested type: FrameCode

        class FrameCode
        {
            readonly short[] _frame;

            public FrameCode(byte code, short bitrate, short f32k, short f44k, short f48k)
            {
                Code = code;
                Bitrate = bitrate;

                _frame = new[] { f48k, f44k, f32k };
            }

            public byte Code { get; private set; }
            public short Bitrate { get; private set; }

            public int GetFrameSize(int fscod)
            {
                if (fscod < 0 || fscod >= _frame.Length)
                    throw new ArgumentOutOfRangeException("fscod");

                return 2 * _frame[fscod];
            }
        }

        #endregion
    }
}

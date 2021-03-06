// -----------------------------------------------------------------------
//  <copyright file="H264BitstreamExtensions.cs" company="Henric Jungheim">
//  Copyright (c) 2012-2016.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012-2016 Henric Jungheim <software@henric.org>
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

namespace SM.Media.H264
{
    static class H264BitstreamExtensions
    {
        public static uint ReadUe(this H264Bitstream h264Bitstream)
        {
            var zeros = 0;

            for (;;)
            {
                var b = h264Bitstream.ReadBits(1);

                if (0 != b)
                    break;

                ++zeros;
            }

            if (0 == zeros)
                return 0;

            var u = h264Bitstream.ReadBits(zeros);

            return (1u << zeros) - 1 + u;
        }

        public static int ReadSe(this H264Bitstream h264Bitstream)
        {
            var codeNum = h264Bitstream.ReadUe();

            if (codeNum < 2)
                return (int)codeNum;

            var n = (int)((codeNum + 1) >> 1);

            if (0 == (codeNum & 1))
                return -n;

            return n;
        }

        public static int ReadSignedBits(this H264Bitstream h264Bitstream, int count)
        {
            var n = h264Bitstream.ReadBits(count);

            var leadingBits = 32 - count;
            var sn = (int)(n << leadingBits);

            sn >>= leadingBits;

            return sn;
        }

        public static uint ReadFfSum(this H264Bitstream h264Bitstream)
        {
            var sum = 0u;

            for (;;)
            {
                var b = h264Bitstream.ReadBits(8);

                sum += b;

                if (b != 0xff)
                    return sum;
            }
        }
    }
}

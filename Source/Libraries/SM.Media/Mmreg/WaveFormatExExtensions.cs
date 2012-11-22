//-----------------------------------------------------------------------
// <copyright file="WaveFormatExExtensions.cs" company="Henric Jungheim">
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
using System.Text;

namespace SM.Media.Mmreg
{
    public static class WaveFormatExExtensions
    {
        /// <summary>
        ///   Add "value" in little endian order.
        /// </summary>
        /// <param name="buffer"> </param>
        /// <param name="value"> </param>
        public static void AddLe(this IList<byte> buffer, ushort value)
        {
            buffer.Add((byte)(value & 0xff));
            buffer.Add((byte)(value >> 8));
        }

        /// <summary>
        ///   Add "value" in little endian order.
        /// </summary>
        /// <param name="buffer"> </param>
        /// <param name="value"> </param>
        public static void AddLe(this IList<byte> buffer, uint value)
        {
            AddLe(buffer, (ushort)value);
            AddLe(buffer, (ushort)(value >> 16));
        }

        public static string ToCodecPrivateData(this WaveFormatEx waveFormatEx)
        {
            var b = new List<byte>(18 + waveFormatEx.cbSize);

            waveFormatEx.ToBytes(b);

            var sb = new StringBuilder(b.Count * 2);

            foreach (var x in b)
                sb.Append(x.ToString("X2"));

            return sb.ToString();
        }
    }
}

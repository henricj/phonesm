// -----------------------------------------------------------------------
//  <copyright file="Well512.cs" company="Henric Jungheim">
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

using System;

namespace SM.Media.Utility.RandomGenerators
{
    public class Well512 : IRandomGenerator<uint>
    {
        //  http://lomont.org/Math/Papers/2008/Lomont_PRNG_2008.pdf

        const float FloatScale = 1.0f / uint.MaxValue;
        const double DoubleScale = 1.0 / ulong.MaxValue;
        readonly IPlatformServices _platformServices;
        readonly uint[] _state = new uint[16];
        uint _index;

        public Well512(IPlatformServices platformServices)
        {
            if (null == platformServices)
                throw new ArgumentNullException("platformServices");

            _platformServices = platformServices;

            Reseed();
        }

        #region IRandomGenerator<uint> Members

        public void Reseed()
        {
            _index = 0;

            _platformServices.GetSecureRandom(_state);
        }

        public uint Next()
        {
            var a = _state[_index];
            var c = _state[(_index + 13) & 15];

            var b = a ^ c ^ (a << 16) ^ (c << 15);

            c = _state[(_index + 9) & 15];

            c ^= (c >> 11);

            a = _state[_index] = b ^ c;

            var d = a ^ ((a << 5) & 0xDA442D24U);

            _index = (_index + 15) & 15;

            a = _state[_index];

            _state[_index] = a ^ b ^ d ^ (a << 2) ^ (b << 18) ^ (c << 28);

            return _state[_index];
        }

        public void GetBytes(byte[] buffer, int offset, int count)
        {
            for (; ; )
            {
                var v = Next();

                for (var j = 0; j < sizeof(uint); ++j)
                {
                    if (count <= 0)
                        return;

                    buffer[offset++] = (byte)v;
                    --count;
                    v >>= 8;
                }
            }
        }

        public float NextFloat()
        {
            return Next() * FloatScale;
        }

        public double NextDouble()
        {
            return (((ulong)Next() << 32) | Next()) * DoubleScale;
        }

        #endregion
    }
}

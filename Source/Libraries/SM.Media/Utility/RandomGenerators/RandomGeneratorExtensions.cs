// -----------------------------------------------------------------------
//  <copyright file="RandomGeneratorExtensions.cs" company="Henric Jungheim">
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
    public static class RandomGeneratorExtensions
    {
        public static void GetBytes(this IRandomGenerator randomGenerator, byte[] buffer)
        {
            randomGenerator.GetBytes(buffer, 0, buffer.Length);
        }

        public static int Next(this IRandomGenerator<uint> randomGenerator, int lessThan)
        {
            if (lessThan <= 0)
                return 0;

            var mask = BitTwiddling.PowerOf2Mask((uint)lessThan);

            for (; ; )
            {
                var v = (int)(randomGenerator.Next() & mask);

                if (v < lessThan)
                    return v;
            }
        }

        public static long Next(this IRandomGenerator<ulong> randomGenerator, long lessThan)
        {
            if (lessThan <= 0)
                return 0;

            var mask = BitTwiddling.PowerOf2Mask((ulong)lessThan);

            for (; ; )
            {
                var v = (long)(randomGenerator.Next() & mask);

                if (v < lessThan)
                    return v;
            }
        }

        public static int NextInt(this IRandomGenerator randomGenerator)
        {
            var uintGen = randomGenerator as IRandomGenerator<uint>;

            if (null != uintGen)
                return unchecked((int)uintGen.Next());

            var ulongGen = randomGenerator as IRandomGenerator<ulong>;

            if (null != ulongGen)
                return unchecked((int)ulongGen.Next());

            return unchecked((int)(-randomGenerator.NextDouble() * int.MinValue));
        }

        public static double NextExponential(this IRandomGenerator randomGenerator, double lambda)
        {
            return -Math.Log(randomGenerator.NextDouble()) / lambda;
        }
    }
}

// -----------------------------------------------------------------------
//  <copyright file="RandomNumbers.cs" company="Henric Jungheim">
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
using System.Security.Cryptography;

namespace SM.Media.Utility
{
    public sealed class RandomNumbers
    {
        readonly object _lock = new object();
        readonly Func<Random> _randomFactory;
        Random _random;

        public RandomNumbers(Func<Random> randomFactory = null)
        {
            _randomFactory = randomFactory ?? DefaultFactory;

            Reseed();
        }

        public void Reseed()
        {
            var random = _randomFactory();

            lock (_lock)
            {
                _random = random;
            }
        }

        static Random DefaultFactory()
        {
            Random random;

            using (var rng = RandomNumberGenerator.Create())
            {
                var seed = new byte[4];

                rng.GetBytes(seed);

                random = new Random(BitConverter.ToInt32(seed, 0));
            }

            return random;
        }

        /// <summary>
        ///     Returns count random numbers greater than or equal to 0 and less than 1.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public double[] GetRandomNumbers(int count)
        {
            var random = new double[count];

            lock (_lock)
            {
                for (var i = 0; i < random.Length; ++i)
                    random[i] = _random.NextDouble();
            }

            return random;
        }

        /// <summary>
        ///     Returns a random number greater than or equal to 0 and less than 1.
        /// </summary>
        /// <returns></returns>
        public double GetRandomNumber()
        {
            lock (_lock)
            {
                return _random.NextDouble();
            }
        }

        /// <summary>
        ///     Fisher–Yates shuffle
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        public void Shuffle<T>(IList<T> list)
        {
            lock (_lock)
            {
                for (var i = list.Count - 1; i >= 1; --i)
                {
                    var j = _random.Next(i + 1);

                    var tmp = list[i];
                    list[i] = list[j];
                    list[j] = tmp;
                }
            }
        }
    }
}

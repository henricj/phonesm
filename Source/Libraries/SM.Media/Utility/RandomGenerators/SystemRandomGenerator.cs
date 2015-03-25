// -----------------------------------------------------------------------
//  <copyright file="SystemRandomGenerator.cs" company="Henric Jungheim">
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
    public class SystemRandomGenerator : IRandomGenerator
    {
        readonly IPlatformServices _platformServices;
        Random _random;

        public SystemRandomGenerator(IPlatformServices platformServices)
        {
            if (null == platformServices)
                throw new ArgumentNullException("platformServices");

            _platformServices = platformServices;

            Reseed();
        }

        #region IRandomGenerator Members

        public void GetBytes(byte[] buffer, int offset, int count)
        {
            if (null == buffer)
                throw new ArgumentNullException("buffer");
            if (offset < 0 || offset >= buffer.Length)
                throw new ArgumentOutOfRangeException("offset");
            if (count < 1 || count + offset > buffer.Length)
                throw new ArgumentOutOfRangeException("count");

            if (0 == offset && buffer.Length == offset)
            {
                _random.NextBytes(buffer);

                return;
            }

            var bytes = new byte[count];

            _random.NextBytes(bytes);

            Array.Copy(bytes, 0, buffer, offset, count);
        }

        public float NextFloat()
        {
            return (float)_random.NextDouble();
        }

        public double NextDouble()
        {
            return _random.NextDouble();
        }

        public void Reseed()
        {
            var seed = new byte[sizeof(int)];

            _platformServices.GetSecureRandom(seed);

            _random = new Random(BitConverter.ToInt32(seed, 0));
        }

        #endregion
    }
}

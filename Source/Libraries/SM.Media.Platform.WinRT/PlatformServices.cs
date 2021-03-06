// -----------------------------------------------------------------------
//  <copyright file="PlatformServices.cs" company="Henric Jungheim">
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
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Security.Cryptography;
using SM.Media.Utility;

namespace SM.Media
{
    public class PlatformServices : IPlatformServices
    {
        readonly Stack<Random> _generators = new Stack<Random>();

        #region IPlatformServices Members

        public double GetRandomNumber()
        {
            var random = GetRandom();

            var ret = random.NextDouble();

            FreeRandom(random);

            return ret;
        }

        public Stream Aes128DecryptionFilter(Stream stream, byte[] key, byte[] iv)
        {
            return new Aes128Pkcs7ReadStream(stream, key, iv);
        }

        public void GetSecureRandom(byte[] bytes)
        {
            var buffer = CryptographicBuffer.GenerateRandom((uint)bytes.Length);

            buffer.CopyTo(bytes);
        }

        #endregion

        Random GetRandom()
        {
            lock (_generators)
            {
                if (_generators.Count > 0)
                    return _generators.Pop();
            }

            var seed = CryptographicBuffer.GenerateRandomNumber();

            return new Random(unchecked((int)seed));
        }

        void FreeRandom(Random random)
        {
            lock (_generators)
            {
                _generators.Push(random);
            }
        }
    }
}

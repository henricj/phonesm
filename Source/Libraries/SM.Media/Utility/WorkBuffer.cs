// -----------------------------------------------------------------------
//  <copyright file="WorkBuffer.cs" company="Henric Jungheim">
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
using System.Threading;

namespace SM.Media.Utility
{
    public class WorkBuffer
    {
        const int DefaultBufferSize = 174 * 188; // Almost 32768 and saves some cycles having to rebuffer partial packets

        public readonly byte[] Buffer;
        public int Length;

#if DEBUG
        static int _sequenceCounter;

        public readonly int Sequence = Interlocked.Increment(ref _sequenceCounter);
        public int ReadCount;
#endif

        public WorkBuffer()
            : this(DefaultBufferSize)
        { }

        public WorkBuffer(int bufferSize)
        {
            if (bufferSize < 1)
                throw new ArgumentException("The buffer size must be positive", "bufferSize");

            Buffer = new byte[bufferSize];
        }

        public override string ToString()
        {
#if DEBUG
            return string.Format("WorkBuffer({0}) count {1} length {2}/{3}", Sequence, ReadCount, Length, Buffer.Length);
#else
            return string.Format("WorkBuffer length {0}/{1}", Length, Buffer.Length);
#endif
        }
    }
}

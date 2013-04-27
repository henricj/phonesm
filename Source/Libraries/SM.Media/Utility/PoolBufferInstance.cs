// -----------------------------------------------------------------------
//  <copyright file="PoolBufferInstance.cs" company="Henric Jungheim">
//  Copyright (c) 2012.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
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

using System.Diagnostics;
using System.Threading;
using SM.TsParser.Utility;

namespace SM.Media.Utility
{
    sealed class PoolBufferInstance : BufferInstance
    {
#if BUFFER_POOL_STATS
        static int _bufferEntryCount;
        readonly int _bufferEntryId = Interlocked.Increment(ref _bufferEntryCount);
#endif
        int _allocationCount;

        public PoolBufferInstance(int size)
            : base(new byte[size])
        { }

        public override void Reference()
        {
            Debug.Assert(_allocationCount >= 0);

            Interlocked.Increment(ref _allocationCount);
        }

        public override bool Dereference()
        {
            Debug.Assert(_allocationCount > 0);

            return 0 == Interlocked.Decrement(ref _allocationCount);
        }

        public override string ToString()
        {
#if BUFFER_POOL_STATS
            return string.Format("Buffer({0}) {1} bytes {2} refs", _bufferEntryId, Buffer.Length, _allocationCount);
#else
            return string.Format("Buffer {0} bytes {1} refs", Buffer.Length, _allocationCount);
#endif
        }
    }
}

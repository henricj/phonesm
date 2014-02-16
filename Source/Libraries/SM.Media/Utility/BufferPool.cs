// -----------------------------------------------------------------------
//  <copyright file="BufferPool.cs" company="Henric Jungheim">
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
using System.Threading;
using SM.TsParser.Utility;

namespace SM.Media.Utility
{
    public interface IBufferPoolParameters
    {
        int BaseSize { get; set; }
        int Pools { get; set; }
    }

    public class DefaultBufferPoolParameters : IBufferPoolParameters
    {
        public DefaultBufferPoolParameters()
        {
            BaseSize = 5 * 64 * 1024;
            Pools = 2;
        }

        #region IBufferPoolParameters Members

        public int BaseSize { get; set; }

        public int Pools { get; set; }

        #endregion
    }

    public sealed class BufferPool : IBufferPool
    {
        readonly BufferSubPool[] _pools;
        int _isDisposed;
#if BUFFER_POOL_STATS
        int _requestedAllocationBytes;
        int _actualAllocationBytes;
        int _actualFreeBytes;
        int _allocationCount;
        int _freeCount;
        int _nonPoolAllocationCount;
#endif

        public BufferPool(IBufferPoolParameters bufferPoolParameters)
        {
            if (null == bufferPoolParameters)
                throw new ArgumentNullException("bufferPoolParameters");

            _pools = new BufferSubPool[bufferPoolParameters.Pools];

            var size = bufferPoolParameters.BaseSize;
            for (var i = 0; i < _pools.Length; ++i)
            {
                _pools[i] = new BufferSubPool(size);

                size <<= 2;
            }
        }

        BufferSubPool FindPool(int size)
        {
            // Could we get Array's binary search to work without having to
            // instantiate a BufferSubPool object per search?
            var min = 0;
            var max = _pools.Length;

            while (min < max)
            {
                var i = (max + min) / 2;

                var pool = _pools[i];

                if (pool.Size == size)
                    return pool;

                if (pool.Size < size)
                    min = i + 1;
                else
                    max = i;
            }

            if (max >= _pools.Length)
                return null;

            return _pools[min];
        }

        public BufferInstance Allocate(int minSize)
        {
#if BUFFER_POOL_STATS
            Interlocked.Increment(ref _allocationCount);
            Interlocked.Add(ref _requestedAllocationBytes, minSize);
#endif
            var pool = FindPool(minSize);

            PoolBufferInstance bufferEntry;
            if (null != pool)
                bufferEntry = pool.Allocate(minSize);
            else
            {
                bufferEntry = new PoolBufferInstance(minSize);
#if BUFFER_POOL_STATS
                Interlocked.Increment(ref _nonPoolAllocationCount);
#endif
            }

#if BUFFER_POOL_STATS
            Interlocked.Add(ref _actualAllocationBytes, bufferEntry.Buffer.Length);
#endif

            bufferEntry.Reference();

#if DEBUG
            //Debug.WriteLine("Allocated Buffer {0}", bufferEntry);
#endif

            return bufferEntry;
        }

        public void Free(BufferInstance bufferInstance)
        {
            //Debug.WriteLine("Free Buffer {0}", bufferInstance);

            if (!bufferInstance.Dereference())
                return;

#if BUFFER_POOL_DEBUG
            for (var i = 0; i < bufferInstance.Buffer.Length; ++i)
                bufferInstance.Buffer[i] = 0xff;
#endif

#if BUFFER_POOL_STATS
            Interlocked.Increment(ref _freeCount);
            Interlocked.Add(ref _actualFreeBytes, bufferInstance.Buffer.Length);
#endif
            var pool = FindPool(bufferInstance.Buffer.Length);

            if (null == pool)
                return; // Oversize buffer relegated to the GC.

            if (pool.Size != bufferInstance.Buffer.Length)
                throw new ArgumentException("Invalid buffer size", "bufferInstance");

            pool.Free((PoolBufferInstance)bufferInstance);
        }

        public void Clear()
        {
#if BUFFER_POOL_STATS
            Debug.Assert(_allocationCount == _freeCount && _actualAllocationBytes == _actualFreeBytes,
                string.Format("BufferPool.Dispose(): _allocationCount {0} == _freeCount {1} && _actualAllocationBytes {2} == _actualFreeBytes {3}",
                    _allocationCount, _freeCount, _actualAllocationBytes, _actualFreeBytes));
#endif

            foreach (var pool in _pools)
                pool.Clear();

#if BUFFER_POOL_STATS
            Debug.WriteLine("Pool clear: alloc {0} free {1} req bytes {2}, alloc bytes {3} free bytes {4}", _allocationCount, _freeCount, _requestedAllocationBytes, _actualAllocationBytes, _actualFreeBytes);

            Interlocked.Exchange(ref _allocationCount, 0);
            Interlocked.Exchange(ref _freeCount, 0);
            Interlocked.Exchange(ref _requestedAllocationBytes, 0);
            Interlocked.Exchange(ref _actualAllocationBytes, 0);
            Interlocked.Exchange(ref _actualFreeBytes, 0);
#endif
        }

        #region Nested type: BufferSubPool

        sealed class BufferSubPool : IDisposable
        {
            public readonly int Size;

            readonly Stack<PoolBufferInstance> _pool = new Stack<PoolBufferInstance>();

#if BUFFER_POOL_STATS
            int _allocationActualSize;
            int _allocationCount;
            int _freeCount;
            int _newAllocationCount;
            readonly List<PoolBufferInstance> _allocationTracker = new List<PoolBufferInstance>();
#endif

            public BufferSubPool(int size)
            {
                Size = size;
            }

            public PoolBufferInstance Allocate(int actualSize)
            {
#if BUFFER_POOL_STATS
                Interlocked.Increment(ref _allocationCount);
                Interlocked.Add(ref _allocationActualSize, actualSize);
#endif
                Debug.Assert(actualSize <= Size);

                lock (_pool)
                {
                    if (_pool.Count > 0)
                        return _pool.Pop();
                }

                var bufferEntry = new PoolBufferInstance(Size);
#if BUFFER_POOL_STATS
                Interlocked.Increment(ref _newAllocationCount);
                lock (_allocationTracker)
                {
                    _allocationTracker.Add(bufferEntry);
                }
#endif

                return bufferEntry;
            }

            public void Free(PoolBufferInstance buffer)
            {
#if BUFFER_POOL_STATS
                Interlocked.Increment(ref _freeCount);
#endif

                if (Size != buffer.Buffer.Length)
                    throw new ArgumentException("Invalid buffer size", "buffer");

                lock (_pool)
                {
                    _pool.Push(buffer);
                }
            }

            public void Clear()
            {
                lock (_pool)
                {
#if BUFFER_POOL_STATS
                    Debug.Assert(_allocationCount == _freeCount && _newAllocationCount == _pool.Count,
                        string.Format("BufferSubPool.Dispose(): _allocationCount {0} == _freeCount {1} && _newAllocationCount {2} == _pool.Count {3}",
                            _allocationCount, _freeCount, _newAllocationCount, _pool.Count));

                    if (_pool.Count != _allocationTracker.Count)
                        Debug.WriteLine("SubPool {0}: Pool size {1} != Tracker {2}", Size, _pool.Count, _allocationTracker.Count);
#endif

                    _pool.Clear();

#if BUFFER_POOL_STATS
                    Debug.WriteLine("SubPool {0}: new {1} alloc {2} free {3} allocSize {4}",
                        Size, _newAllocationCount, _allocationCount, _freeCount, _allocationActualSize);

                    _allocationTracker.Clear();
                    _newAllocationCount = 0;
                    _freeCount = 0;
                    _allocationActualSize = 0;
                    _allocationCount = 0;
#endif
                }
            }

            public void Dispose()
            {
                Clear();
            }

            public override string ToString()
            {
#if BUFFER_POOL_STATS
                return string.Format("Pool {0}k ({1} free of {2})", Size * (1 / 1024.0), _freeCount, _newAllocationCount);
#else
                return string.Format("Pool {0}k", Size * (1 / 1024.0));
#endif
            }
        }

        #endregion

        public void Dispose()
        {
            if (0 != Interlocked.Exchange(ref _isDisposed, 1))
                return;

             Clear();

            if (null != _pools)
            {
                foreach (var pool in _pools)
                    pool.Dispose();
            }
        }
    }
}

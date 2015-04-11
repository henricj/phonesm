// -----------------------------------------------------------------------
//  <copyright file="WinRtBufferPool.cs" company="Henric Jungheim">
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
using System.Diagnostics;
using System.Linq;
using System.Text;
using Windows.Storage.Streams;
using Buffer = Windows.Storage.Streams.Buffer;

namespace SM.Media.Utility
{
    public class WinRtBufferPool
    {
        readonly Bucket[] _buckets;
        readonly object _lock = new object();
#if DEBUG
        ulong _newCount;
        uint _largest;
#endif

        public WinRtBufferPool(params uint[] bucketSizes)
        {
            _buckets = bucketSizes
                .Select(BitTwiddling.NextPowerOf2)
                .Distinct()
                .OrderBy(s => s)
                .Select(s => new Bucket(s))
                .ToArray();
        }

        public IBuffer Allocate(uint size)
        {
            lock (_lock)
            {
#if DEBUG
                if (size > _largest)
                    _largest = size;
#endif

                foreach (var bucket in _buckets)
                {
                    if (bucket.Size >= size)
                        return bucket.Allocate();
                }
            }

#if DEBUG
            ++_newCount;
#endif

            return new Buffer(size);
        }

        public void Free(IBuffer buffer)
        {
            var capacity = buffer.Capacity;

            lock (_lock)
            {
                foreach (var bucket in _buckets)
                {
                    if (capacity == bucket.Size)
                    {
                        bucket.Free(buffer);

                        return;
                    }
                }

                Debug.WriteLine("Hello");
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
#if DEBUG
                _newCount = 0;
                _largest = 0;
#endif

                foreach (var bucket in _buckets)
                {
                    bucket.Clear();
                }
            }
        }

        #region Nested type: Bucket

        class Bucket
        {
            readonly Stack<IBuffer> _buffers = new Stack<IBuffer>();
            readonly uint _size;
#if DEBUG
            ulong _allocationCount;
            ulong _freeCount;
            ulong _newCount;
#endif

            public Bucket(uint size)
            {
                if (size < 1)
                    throw new ArgumentOutOfRangeException("size");

                _size = size;
            }

            public uint Size
            {
                get { return _size; }
            }

            public void Free(IBuffer buffer)
            {
#if DEBUG
                ++_freeCount;
#endif

                Debug.Assert(buffer.Capacity == Size);

                buffer.Length = 0;

                _buffers.Push(buffer);
            }

            public IBuffer Allocate()
            {
#if DEBUG
                ++_allocationCount;
#endif

                if (_buffers.Count < 1)
                {
#if DEBUG
                    ++_newCount;
#endif

                    return new Buffer(Size);
                }

                var buffer = _buffers.Pop();

                Debug.Assert(buffer.Capacity == Size);

                return buffer;
            }

            public void Clear()
            {
#if DEBUG
                _newCount = 0;
                _allocationCount = 0;
                _freeCount = 0;
#endif

                _buffers.Clear();
            }

            public override string ToString()
            {
#if DEBUG
                return string.Format("Size {0} Count {1} new {2} alloc {3} free {4}", Size, _buffers.Count, _newCount, _allocationCount, _freeCount);
#else
                return string.Format("Size {0} Count {1}", Size, _buffers.Count);
#endif
            }
        }

        #endregion

        public override string ToString()
        {
            var sb = new StringBuilder();

#if DEBUG
            sb.Append("IBuffer new " + _newCount + " largest " + _largest);
#endif

            foreach (var bucket in _buckets)
            {
                sb.AppendLine();
                sb.Append("   ");
                sb.Append(bucket);
            }

            return sb.ToString();
        }
    }
}

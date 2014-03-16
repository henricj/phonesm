// -----------------------------------------------------------------------
//  <copyright file="BlockingPool.cs" company="Henric Jungheim">
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
using System.Threading.Tasks;

namespace SM.Media.Utility
{
    public sealed class BlockingPool<TItem> : IBlockingPool<TItem>
        where TItem : class, new()
    {
        readonly Queue<TItem> _pool = new Queue<TItem>();
        readonly int _poolSize;
        readonly Queue<CancellationTaskCompletionSource<TItem>> _waiters = new Queue<CancellationTaskCompletionSource<TItem>>();
        int _allocationCount;

#if DEBUG
        readonly List<TItem> _allocationTracker = new List<TItem>();
#endif // DEBUG

        public BlockingPool(int poolSize)
        {
            _poolSize = poolSize;
        }

        #region IBlockingPool<TItem> Members

        public Task<TItem> AllocateAsync(CancellationToken cancellationToken)
        {
            lock (_pool)
            {
                if (_pool.Count > 0)
                {
                    var item = _pool.Dequeue();

                    Debug.Assert(!EqualityComparer<TItem>.Default.Equals(default(TItem), item));

                    //Debug.WriteLine("BlockingPool.AllocateAsync() Returning pool item: " + item);

                    return TaskEx.FromResult(item);
                }

                if (_allocationCount >= _poolSize)
                {
                    var workHandle = new CancellationTaskCompletionSource<TItem>(wh => _waiters.Remove(wh), cancellationToken);

                    _waiters.Enqueue(workHandle);

#if DEBUG
                    //var sw = Stopwatch.StartNew();

                    //Debug.WriteLine("BlockingPool.AllocateAsync() Creating waiter");
                    //workHandle.Task.ContinueWith(t =>
                    //                             {
                    //                                 sw.Stop();

                    //                                 Debug.WriteLine("BlockingPool.AllocateAsync() Waiter completed: status {0} elapsed {1}",
                    //                                     t.Status, sw.Elapsed);
                    //                             });
#endif

                    return workHandle.Task;
                }

                ++_allocationCount;
            }

            var newItem = new TItem();

            //Debug.WriteLine("BlockingPool.AllocateAsync() Returning new item " + newItem);

#if DEBUG
            _allocationTracker.Add(newItem);
#endif

            return TaskEx.FromResult(newItem);
        }

        public void Free(TItem item)
        {
            //Debug.WriteLine("BlockingPool.Free() item: " + item);

            if (EqualityComparer<TItem>.Default.Equals(default(TItem), item))
                throw new ArgumentNullException("item");

            lock (_pool)
            {
#if DEBUG
                Debug.Assert(_allocationTracker.Contains(item), "Unknown item has been freed");
                Debug.Assert(!_pool.Contains(item), "Item is already in pool");
#endif

                while (_waiters.Count > 0)
                {
                    Debug.Assert(0 == _pool.Count, "The pool should be empty when there are waiters");

                    var waiter = _waiters.Dequeue();

                    //Debug.WriteLine("BlockingPool.Free() giving to waiter: " + item);

                    if (waiter.TrySetResult(item))
                        return;

                    //Debug.WriteLine("BlockingPool.Free() giving to waiter failed for: " + item);
                }

                _pool.Enqueue(item);
            }
        }

        public void Dispose()
        {
            Clear();
        }

        #endregion

        void Clear()
        {
            CancellationTaskCompletionSource<TItem>[] waiters;

            lock (_pool)
            {
                waiters = _waiters.ToArray();
                _waiters.Clear();

                _pool.Clear();
                _allocationCount = 0;

#if DEBUG
                _allocationTracker.Clear();
#endif
            }

            foreach (var waiter in waiters)
                waiter.Dispose();
        }
    }
}

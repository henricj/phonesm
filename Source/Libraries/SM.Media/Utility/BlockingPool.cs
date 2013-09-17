// -----------------------------------------------------------------------
//  <copyright file="BlockingPool.cs" company="Henric Jungheim">
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SM.Media.Utility
{
    public sealed class BlockingPool<TItem> : IBlockingPool<TItem>
        where TItem : new()
    {
        readonly AsyncManualResetEvent _bufferWait = new AsyncManualResetEvent(true);
        readonly Stack<LinkedListNode<TItem>> _freeNodes = new Stack<LinkedListNode<TItem>>();
        readonly LinkedList<TItem> _pool = new LinkedList<TItem>();
        readonly int _poolSize;
        int _allocationCount;

        public BlockingPool(int poolSize)
        {
            _poolSize = poolSize;
        }

        #region IBlockingPool<TItem> Members

        public async Task<TItem> AllocateAsync(CancellationToken cancellationToken)
        {
            for (; ; )
            {
                var allocateBuffer = false;

                lock (_pool)
                {
                    if (_pool.Count > 0)
                    {
                        var node = _pool.First;

                        _pool.RemoveFirst();

                        var item = node.Value;

                        _freeNodes.Push(node);

                        Debug.Assert(!EqualityComparer<TItem>.Default.Equals(default(TItem), item));

                        return item;
                    }

                    if (_allocationCount < _poolSize)
                    {
                        ++_allocationCount;

                        allocateBuffer = true;
                    }

                    _bufferWait.Reset();
                }

                if (allocateBuffer)
                    return new TItem();

                //Debug.WriteLine("Waiting for item");

                //var sw = Stopwatch.StartNew();

                await _bufferWait.WaitAsync()
                    .WithCancellation(cancellationToken)
                    .ConfigureAwait(false);

                //sw.Stop();

                //Debug.WriteLine("Done waiting for item ({0})", sw.Elapsed);
            }
        }

        public void Free(TItem value)
        {
            if (EqualityComparer<TItem>.Default.Equals(default(TItem), value))
                throw new ArgumentNullException("value");

            lock (_pool)
            {
                if (_freeNodes.Count > 0)
                {
                    var node = _freeNodes.Pop();

                    node.Value = value;

                    _pool.AddFirst(node);
                }
                else
                    _pool.AddFirst(value);

                if (1 == _pool.Count)
                    _bufferWait.Set();
            }
        }

        public void Dispose()
        {
            Clear();
        }

        #endregion

        void Clear()
        {
            lock (_pool)
            {
                _pool.Clear();
                _allocationCount = 0;
                _freeNodes.Clear();
            }
        }
    }
}

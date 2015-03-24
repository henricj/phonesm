// -----------------------------------------------------------------------
//  <copyright file="WorkBufferBlockingPool.cs" company="Henric Jungheim">
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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SM.Media.Utility
{
    public sealed class WorkBufferBlockingPool : IBlockingPool<WorkBuffer>
    {
        BlockingPool<WorkBuffer> _pool;

        public WorkBufferBlockingPool(int poolSize)
        {
            _pool = new BlockingPool<WorkBuffer>(poolSize);
        }

        #region IBlockingPool<WorkBuffer> Members

        public void Dispose()
        {
            var pool = _pool;

            _pool = null;

            pool.Dispose();
        }

#if DEBUG
        public async Task<WorkBuffer> AllocateAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            var item = await _pool.AllocateAsync(cancellationToken).ConfigureAwait(false);

            Debug.Assert(null == item.Metadata, "Pending metadata");

            return item;
        }
#else
        public Task<WorkBuffer> AllocateAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            return _pool.AllocateAsync(cancellationToken);
        }
#endif

        public void Free(WorkBuffer item)
        {
            ThrowIfDisposed();

            item.Metadata = null;

            _pool.Free(item);
        }

        #endregion

        void ThrowIfDisposed()
        {
            if (null == _pool)
                throw new ObjectDisposedException(GetType().Name);
        }
    }
}

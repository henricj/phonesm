// -----------------------------------------------------------------------
//  <copyright file="AsyncLock.cs" company="Henric Jungheim">
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
    public sealed class AsyncLock : IDisposable
    {
        readonly object _lock = new object();
        bool _isLocked;
        Queue<TaskCompletionSource<IDisposable>> _pending = new Queue<TaskCompletionSource<IDisposable>>();

        #region IDisposable Members

        public void Dispose()
        {
            TaskCompletionSource<IDisposable>[] pending;

            lock (_lock)
            {
                if (null == _pending)
                    return;

                CheckInvariant();

                _isLocked = true;

                if (0 == _pending.Count)
                {
                    _pending = null;
                    return;
                }

                pending = _pending.ToArray();
                _pending.Clear();

                _pending = null;
            }

            foreach (var tcs in pending)
                tcs.TrySetCanceled();
        }

        #endregion

        [Conditional("DEBUG")]
        void CheckInvariant()
        {
            Debug.Assert(null != _pending && !(_pending.Count > 0 && !_isLocked), "If there are pending, then we should be locked");
        }

        public IDisposable TryLock()
        {
            ThrowIfDisposed();

            lock (_lock)
            {
                CheckInvariant();

                if (_isLocked)
                    return null;

                _isLocked = true;

                return new Releaser(this);
            }
        }

        public Task<IDisposable> LockAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            cancellationToken.ThrowIfCancellationRequested();

            TaskCompletionSource<IDisposable> tcs;

            lock (_lock)
            {
                CheckInvariant();

                if (!_isLocked)
                {
                    _isLocked = true;

                    return TaskEx.FromResult<IDisposable>(new Releaser(this));
                }

                tcs = new TaskCompletionSource<IDisposable>();

                _pending.Enqueue(tcs);
            }

            if (!cancellationToken.CanBeCanceled)
                return tcs.Task;

            return CancellableTaskAsync(tcs, cancellationToken);
        }

        async Task<IDisposable> CancellableTaskAsync(TaskCompletionSource<IDisposable> tcs, CancellationToken cancellationToken)
        {
            using (cancellationToken.Register(
                () =>
                {
                    bool wasPending;

                    lock (_lock)
                    {
                        CheckInvariant();

                        wasPending = _pending.Remove(tcs);
                    }

                    if (wasPending)
                        tcs.TrySetCanceled();
                }))
            {
                return await tcs.Task.ConfigureAwait(false);
            }
        }

        void Release()
        {
            for (; ; )
            {
                TaskCompletionSource<IDisposable> tcs;

                lock (_lock)
                {
                    CheckInvariant();

                    Debug.Assert(_isLocked, "AsyncLock.Release() was unlocked");

                    if (0 == _pending.Count)
                    {
                        _isLocked = false;
                        return;
                    }

                    tcs = _pending.Dequeue();
                }

                if (tcs.TrySetResult(new Releaser(this)))
                    return;
            }
        }

        void ThrowIfDisposed()
        {
            if (null != _pending)
                return;

            throw new ObjectDisposedException(GetType().Name);
        }

        #region Nested type: Releaser

        sealed class Releaser : IDisposable
        {
            AsyncLock _asyncLock;

            public Releaser(AsyncLock asynclock)
            {
                _asyncLock = asynclock;
            }

            #region IDisposable Members

            public void Dispose()
            {
                var asyncLock = Interlocked.Exchange(ref _asyncLock, null);

                if (null != asyncLock)
                    asyncLock.Release();
            }

            #endregion
        }

        #endregion
    }
}

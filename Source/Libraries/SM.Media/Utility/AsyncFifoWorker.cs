// -----------------------------------------------------------------------
//  <copyright file="AsyncFifoWorker.cs" company="Henric Jungheim">
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
    public sealed class AsyncFifoWorker : IDisposable
    {
        readonly object _lock = new object();
        readonly SignalTask _signalTask;
        readonly Queue<Func<Task>> _workQueue = new Queue<Func<Task>>();
        bool _isClosed;

        public AsyncFifoWorker(CancellationToken cancellationToken)
        {
            _signalTask = new SignalTask(Worker, cancellationToken);
        }

        #region IDisposable Members

        public void Dispose()
        {
            using (_signalTask)
            { }
        }

        #endregion

        async Task Worker()
        {
            for (; ; )
            {
                Func<Task> work;

                lock (_lock)
                {
                    if (_workQueue.Count < 1)
                        return;

                    work = _workQueue.Dequeue();
                }

                try
                {
                    await work().ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                { }
                catch (Exception ex)
                {
                    Debug.WriteLine("AsyncFifoWorker.Worker() work should not throw exceptions: " + ex.Message);
                }
            }
        }

        public void Post(Func<Task> workFunc)
        {
            if (workFunc == null)
                throw new ArgumentNullException("workFunc");

            lock (_lock)
            {
                if (_isClosed)
                    throw new InvalidOperationException("AsyncFifoWorker is closed");

                _workQueue.Enqueue(workFunc);
            }

            _signalTask.Fire();
        }

        public Task CloseAsync()
        {
            lock (_lock)
            {
                _isClosed = true;
            }

            return _signalTask.WaitAsync();
        }
    }

    public static class AsyncFifoWorkerExtensions
    {
        public static void Post(this AsyncFifoWorker worker, Action action)
        {
            worker.Post(() =>
                        {
                            action();
                            return TplTaskExtensions.CompletedTask;
                        });
        }

        public static void Post(this AsyncFifoWorker worker, Task work)
        {
            worker.Post(() => work);
        }

        public static Task PostAsync(this AsyncFifoWorker worker, Func<Task> workFunc)
        {
            var tcs = new TaskCompletionSource<bool>();

            worker.Post(() => workFunc().ContinueWith(t => tcs.TrySetResult(true)));

            return tcs.Task;
        }
    }
}

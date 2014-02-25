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
        readonly Queue<WorkHandle> _workQueue = new Queue<WorkHandle>();
        bool _isClosed;

        public AsyncFifoWorker(CancellationToken cancellationToken)
        {
            _signalTask = new SignalTask(Worker, cancellationToken);
        }

        public AsyncFifoWorker()
        {
            _signalTask = new SignalTask(Worker);
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (!Close())
                return;

            using (_signalTask)
            { }

            WorkHandle[] workQueue;

            lock (_lock)
            {
                workQueue = _workQueue.ToArray();
                _workQueue.Clear();
            }

            foreach (var work in workQueue)
            {
                work.CancellationTokenRegistration.Dispose();

                var tcs = work.TaskCompletionSource;

                if (null != tcs)
                    tcs.TrySetCanceled();
            }
        }

        #endregion

        async Task Worker()
        {
            for (; ; )
            {
                WorkHandle work;

                lock (_lock)
                {
                    if (_workQueue.Count < 1)
                        return;

                    work = _workQueue.Dequeue();
                }

                work.CancellationTokenRegistration.Dispose();

                var tcs = work.TaskCompletionSource;

                try
                {
                    await work.Work().ConfigureAwait(false);

                    if (null != tcs)
                        tcs.TrySetResult(true);
                }
                catch (OperationCanceledException)
                {
                    if (null != tcs)
                        tcs.TrySetCanceled();
                }
                catch (Exception ex)
                {
                    if (null != tcs)
                        tcs.TrySetException(ex);
                    else
                        Debug.WriteLine("AsyncFifoWorker.Worker() work should not throw exceptions: " + ex.ExtendedMessage());
                }

                work.Dispose();
            }
        }

        void RemoveWork(object workObject)
        {
            var work = (WorkHandle)workObject;

            lock (_lock)
            {
                if (!_workQueue.Remove(work))
                    return;
            }

            work.Dispose();
        }

        public void Post(Func<Task> workFunc, CancellationToken cancellationToken)
        {
            PostWork(workFunc, false, cancellationToken);
        }

        public Task PostAsync(Func<Task> workFunc, CancellationToken cancellationToken)
        {
            var work = PostWork(workFunc, true, cancellationToken);

            return work.TaskCompletionSource.Task;
        }

        WorkHandle PostWork(Func<Task> workFunc, bool createTcs, CancellationToken cancellationToken)
        {
            if (workFunc == null)
                throw new ArgumentNullException("workFunc");

            cancellationToken.ThrowIfCancellationRequested();

            WorkHandle work;

            lock (_lock)
            {
                if (_isClosed)
                    throw new InvalidOperationException("AsyncFifoWorker is closed");

                work = new WorkHandle
                       {
                           Work = workFunc,
                           TaskCompletionSource = createTcs ? new TaskCompletionSource<bool>() : null
                       };

                _workQueue.Enqueue(work);
            }

            work.CancellationTokenRegistration = cancellationToken.Register(RemoveWork, work);

            try
            {
                _signalTask.Fire();
            }
            catch (ObjectDisposedException)
            {
                RemoveWork(work);

                if (_workQueue.Count > 0)
                    Debug.WriteLine("AsyncFifoWorker.Post() object disposed but there are still {0} pending", _workQueue.Count);

                throw;
            }

            return work;
        }

        public Task CloseAsync()
        {
            Close();

            return _signalTask.WaitAsync();
        }

        bool Close()
        {
            lock (_lock)
            {
                if (_isClosed)
                    return false;

                _isClosed = true;
            }

            return true;
        }

        #region Nested type: WorkHandle

        sealed class WorkHandle : IDisposable
        {
            public CancellationTokenRegistration CancellationTokenRegistration;
            public TaskCompletionSource<bool> TaskCompletionSource;
            public Func<Task> Work;

            #region IDisposable Members

            public void Dispose()
            {
                CancellationTokenRegistration.Dispose();

                if (null != TaskCompletionSource)
                    TaskCompletionSource.TrySetCanceled();
            }

            #endregion
        }

        #endregion
    }

    public static class AsyncFifoWorkerExtensions
    {
        public static void Post(this AsyncFifoWorker worker, Action action, CancellationToken cancellationToken)
        {
            worker.Post(() =>
                        {
                            action();
                            return TplTaskExtensions.CompletedTask;
                        }, cancellationToken);
        }

        public static void Post(this AsyncFifoWorker worker, Task work, CancellationToken cancellationToken)
        {
            worker.Post(() => work, cancellationToken);
        }
    }
}

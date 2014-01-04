// -----------------------------------------------------------------------
//  <copyright file="QueueWorker.cs" company="Henric Jungheim">
//  Copyright (c) 2012, 2013.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012, 2013 Henric Jungheim <software@henric.org>
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
    public sealed class QueueWorker<TWorkItem> : IQueueThrottling, IDisposable
        where TWorkItem : class
    {
        readonly CancellationTokenSource _abortTokenSource = new CancellationTokenSource();
        readonly Action<TWorkItem> _callback;
        readonly Action<TWorkItem> _cleanup;
        readonly TaskCompletionSource<bool> _closeTaskCompletionSource = new TaskCompletionSource<bool>();
        readonly LinkedList<TWorkItem> _processBuffers = new LinkedList<TWorkItem>();
        readonly object _processLock = new object();
        bool _isClosed;
        int _isDisposed;
        bool _isEnabled;
        bool _isPaused;
        Task _queueWorker;
        bool _workerRunning;

        public QueueWorker(Action<TWorkItem> callback, Action<TWorkItem> cleanup)
        {
            _callback = callback;
            _cleanup = cleanup;
        }

        public bool IsEnabled
        {
            get { return _isEnabled; }
            set
            {
                lock (_processLock)
                {
                    if (_isClosed)
                        return;

                    if (_isEnabled == value)
                        return;

                    _isEnabled = value;

                    if (_isEnabled && _processBuffers.Count > 0)
                        UnlockedWakeWorker();
                }

                if (!value)
                    Clear();
            }
        }

        public bool IsPaused
        {
            get { return _isPaused; }
            set
            {
                ThrowIfDisposed();

                if (_isClosed)
                    return;

                if (_isPaused == value)
                    return;

                lock (_processLock)
                {
                    _isPaused = value;

                    if (!_isPaused)
                        UnlockedWakeWorker();
                }
            }
        }

        #region IDisposable Members

        public void Dispose()
        {
            var wasDisposed = Interlocked.Exchange(ref _isDisposed, 1);

            if (0 != wasDisposed)
                return;

            Clear();

            Task queueWorker;
            lock (_processLock)
            {
                queueWorker = _queueWorker;
            }

            if (null != queueWorker)
                queueWorker.Wait();
        }

        #endregion

        #region IQueueThrottling Members

        public void Pause()
        {
            IsPaused = true;
        }

        public void Resume()
        {
            IsPaused = false;
        }

        #endregion

        public void Enqueue(TWorkItem value)
        {
            lock (_processLock)
            {
                if (_isClosed)
                    return;

                ThrowIfDisposed();

                _processBuffers.AddLast(value);

                if (!_isPaused)
                    UnlockedWakeWorker();
            }
        }

        void UnlockedWakeWorker()
        {
            if (_workerRunning)
                return;

            _workerRunning = true;

            //_queueWorker = Task.Run((Func<Task>)QueueWorker);
            _queueWorker = Task.Factory.StartNew((Func<Task>)WorkerAsync, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default)
                               .Unwrap();
        }

        void ThrowIfDisposed()
        {
            if (0 != _isDisposed)
                throw new ObjectDisposedException(GetType().Name);
        }

        public void Clear()
        {
            ClearImpl(false);
        }

        void ClearImpl(bool closeQueue)
        {
            TWorkItem[] workItems = null;

            lock (_processLock)
            {
                if (_processBuffers.Count > 0)
                {
                    workItems = new TWorkItem[_processBuffers.Count];

                    _processBuffers.CopyTo(workItems, 0);

                    _processBuffers.Clear();
                }

                if (closeQueue)
                {
                    if (!_isClosed)
                    {
                        _isClosed = true;

                        UnlockedWakeWorker();
                    }
                }
                else
                {
                    if (!_isPaused)
                        UnlockedWakeWorker();
                }
            }

            if (null != workItems)
            {
                foreach (var workItem in workItems)
                {
                    if (null != workItem)
                        _cleanup(workItem);
                }
            }
        }

        public Task FlushAsync()
        {
            IsPaused = false;

            Task queueWorker;

            lock (_processLock)
            {
                queueWorker = _queueWorker;
            }

            return queueWorker ?? TplTaskExtensions.CompletedTask;
        }

        public Task ClearAsync()
        {
            Clear();

            Task queueWorker;

            lock (_processLock)
            {
                queueWorker = _queueWorker;
            }

            return queueWorker ?? TplTaskExtensions.CompletedTask;
        }

        public Task CloseAsync()
        {
            ClearImpl(true);

            return _closeTaskCompletionSource.Task;
        }

        async Task WorkerAsync()
        {
            var closeTask = false;
            var normalExit = false;

            try
            {
                for (; ; )
                {
                    TWorkItem workItem = null;

                    try
                    {
                        lock (_processLock)
                        {
                            var isDisposed = 0 != _isDisposed;
                            var isEnabled = _isEnabled;
                            var isClosed = _isClosed;
                            var isPaused = _isPaused;

                            if (isDisposed || !isEnabled || isClosed || isPaused)
                            {
                                normalExit = true;
                                _workerRunning = false;

                                if (!isClosed)
                                    return;

                                if (_closeTaskCompletionSource.Task.IsCompleted)
                                    return;

                                _workerRunning = true;
                                closeTask = true;
                            }

                            if (!closeTask)
                            {
                                if (0 == _processBuffers.Count)
                                {
                                    normalExit = true;
                                    _workerRunning = false;

                                    return;
                                }

                                var item = _processBuffers.First;

                                _processBuffers.RemoveFirst();

                                workItem = item.Value;
                            }
                        }

                        _callback(workItem);

                        if (null == workItem)
                            Clear();

                        if (closeTask)
                            return;

                        continue;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Callback failed: " + ex.Message);
                    }
                    finally
                    {
                        if (null != workItem)
                            _cleanup(workItem);
                    }

                    await TaskEx.Delay(250, _abortTokenSource.Token).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("WorkAsync failed: " + ex.Message);
                throw;
            }
            finally
            {
                lock (_processLock)
                {
                    Debug.Assert(normalExit, "QueueWorker.WorkerAsync() exited unexpectedly");

                    _workerRunning = false;
                }

                if (closeTask)
                    _closeTaskCompletionSource.TrySetResult(true);
            }
        }
    }
}

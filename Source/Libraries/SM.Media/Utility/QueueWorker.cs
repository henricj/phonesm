// -----------------------------------------------------------------------
//  <copyright file="QueueWorker.cs" company="Henric Jungheim">
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
    public sealed class QueueWorker<TWorkItem> : IQueueThrottling, IDisposable
        where TWorkItem : class
    {
        readonly CancellationTokenSource _abortTokenSource = new CancellationTokenSource();
        readonly Action<TWorkItem> _callback;
        readonly Action<TWorkItem> _cleanup;
        readonly TaskCompletionSource<bool> _closeTaskCompletionSource = new TaskCompletionSource<bool>();
        readonly LinkedList<TWorkItem> _processBuffers = new LinkedList<TWorkItem>();
        readonly object _processLock = new object();
        readonly SignalTask _workerTask;
        Exception _exception;
        bool _isClosed;
        int _isDisposed;
        bool _isEnabled;
        bool _isPaused;

        public QueueWorker(Action<TWorkItem> callback, Action<TWorkItem> cleanup)
        {
            _callback = callback;
            _cleanup = cleanup;

            _workerTask = SignalTask.Create(Worker);
        }

        public Exception Exception
        {
            get { return _exception; }
        }

        public bool IsEnabled
        {
            get { return !_isClosed && _isEnabled; }
            set
            {
                lock (_processLock)
                {
                    if (_isClosed)
                        return;

                    if (value == _isEnabled)
                        return;

                    _isEnabled = value;

                    if (_isEnabled && !_isPaused && _processBuffers.Count > 0)
                        UnlockedWakeWorker();
                }

                if (!value)
                    Clear(false);
            }
        }

        bool IsPaused
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

                    if (_isEnabled && !_isPaused && _processBuffers.Count > 0)
                        UnlockedWakeWorker();
                }
            }
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (0 != Interlocked.Exchange(ref _isDisposed, 1))
                return;

            _abortTokenSource.Cancel();

            Clear(true);

            _workerTask.Dispose();

            _abortTokenSource.Dispose();
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
            //Debug.WriteLine("QueueWorker.Enqueue() " + value);

            lock (_processLock)
            {
                if (_isClosed)
                    throw _exception ?? new NotSupportedException("The worker is closed");

                ThrowIfDisposed();

                _processBuffers.AddLast(value);

                if (!_isPaused)
                    UnlockedWakeWorker();
            }
        }

        void UnlockedWakeWorker()
        {
            _workerTask.Fire();
        }

        void ThrowIfDisposed()
        {
            if (0 != _isDisposed)
                throw new ObjectDisposedException(GetType().Name);
        }

        void Clear(bool closeQueue)
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
                        _isClosed = true;
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

            return _workerTask.WaitAsync();
        }

        public Task ClearAsync()
        {
            Clear(false);

            return _workerTask.WaitAsync();
        }

        public Task CloseAsync()
        {
            Clear(true);

            return _workerTask.WaitAsync();
        }

        void Worker()
        {
            var normalExit = false;

            try
            {
                for (; ; )
                {
                    ThrowIfDisposed();

                    TWorkItem workItem = null;

                    try
                    {
                        lock (_processLock)
                        {
                            if (!_isEnabled || _isClosed || _isPaused || _closeTaskCompletionSource.Task.IsCompleted)
                            {
                                normalExit = true;

                                return;
                            }

                            if (0 == _processBuffers.Count)
                            {
                                normalExit = true;

                                return;
                            }

                            var item = _processBuffers.First;

                            _processBuffers.RemoveFirst();

                            workItem = item.Value;
                        }

                        _callback(workItem);
                    }
                    catch (OperationCanceledException)
                    {
                        normalExit = true;

                        return;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Callback failed: " + ex.Message);

                        lock (_processLock)
                        {
                            _exception = ex;
                        }

                        Clear(true);

                        normalExit = true;

                        return;
                    }
                    finally
                    {
                        if (null != workItem)
                            _cleanup(workItem);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("WorkAsync failed: " + ex.Message);
            }
            finally
            {
                Debug.Assert(normalExit, "QueueWorker.WorkerAsync() exited unexpectedly");
            }
        }
    }
}

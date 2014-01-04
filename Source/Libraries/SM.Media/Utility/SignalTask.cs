// -----------------------------------------------------------------------
//  <copyright file="SignalTask.cs" company="Henric Jungheim">
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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SM.Media.Utility
{
    public sealed class SignalTask : IDisposable
    {
        readonly Func<Task> _handler;
        readonly object _lock = new object();
        readonly CancellationTokenSource _token;
        bool _isDisposed;
        bool _isPending;
        Task _task;
#if DEBUG
        int _callCounter;
#endif

        public SignalTask(Func<Task> handler, CancellationToken token)
        {
            if (handler == null)
                throw new ArgumentNullException("handler");

            _handler = handler;
            _token = CancellationTokenSource.CreateLinkedTokenSource(token);
        }

        public bool IsActive
        {
            get
            {
                lock (_lock)
                {
                    return _isPending || null != _task;
                }
            }
        }

        #region IDisposable Members

        public void Dispose()
        {
            Task task;

            lock (_lock)
            {
                _isDisposed = true;
                task = _task;
            }

            _token.Cancel();

            if (null == task)
                return;

            try
            {
                task.Wait();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SignalTask.Dispose(): " + ex.Message);
            }
        }

        #endregion

        public void Fire()
        {
            Task<Task> task;
            Task unwrapTask;

            lock (_lock)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                if (_isPending)
                    return;

                _isPending = true;

                if (null != _task)
                    return;

                task = new Task<Task>(CallHandlerAsync, _token.Token
#if SM_MEDIA_LEGACY
                    );
#else
, TaskCreationOptions.DenyChildAttach);
#endif

                _task = unwrapTask = task.Unwrap();
            }

            try
            {
                // We start the outer task *after* leaving the monitor.
                if (TaskStatus.Created == task.Status)
                    task.Start(TaskScheduler.Default);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SignalTask.Fire() task start failed: " + ex.Message);

                lock (_lock)
                {
                    if (ReferenceEquals(unwrapTask, _task))
                    {
                        _task = null;
                        _isPending = false;
                    }
                }

                throw;
            }

#if DEBUG
            if (_isDisposed)
                throw new ObjectDisposedException(GetType().FullName);
#endif
        }

        async Task CallHandlerAsync()
        {
            try
            {
                for (; ; )
                {
                    lock (_lock)
                    {
                        if (!_isPending || _isDisposed || _token.IsCancellationRequested)
                        {
                            _task = null;

                            return;
                        }

                        _isPending = false;
                    }

#if DEBUG
                    var count = Interlocked.Increment(ref _callCounter);
                    Debug.Assert(1 == count, "SignalTask.CallHandlerAsync(): concurrent call detected");
#endif
                    try
                    {
                        await _handler().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("SignalTask.CallHandlerAsync() handler failed: " + ex.Message);
                    }
                    finally
                    {
#if DEBUG
                        count = Interlocked.Decrement(ref _callCounter);
                        Debug.Assert(0 == count, "SignalTask.CallHandlerAsync(): concurrent call detected after return");
#endif
                    }
                }
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    _task = null;
                    _isPending = false;
                }

                Debug.WriteLine("SignalTask.CallHandlerAsync() failed: " + ex.Message);
            }
        }

        public Task WaitAsync()
        {
            Task task;

            lock (_lock)
            {
                task = _task;
            }

            return task ?? TplTaskExtensions.CompletedTask;
        }
    }
}

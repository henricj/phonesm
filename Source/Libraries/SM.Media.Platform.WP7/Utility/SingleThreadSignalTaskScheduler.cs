// -----------------------------------------------------------------------
//  <copyright file="SingleThreadSignalTaskScheduler.cs" company="Henric Jungheim">
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
    public sealed class SingleThreadSignalTaskScheduler : TaskScheduler, IDisposable
    {
        readonly object _lock = new object();
        readonly Queue<Task> _tasks = new Queue<Task>();
        readonly Thread _thread;
        bool _isDone;
        bool _isSignaled;
        Action _signalHandler;

        public SingleThreadSignalTaskScheduler(string name, Action signalHandler)
        {
            if (name == null)
                throw new ArgumentNullException("name");
            if (signalHandler == null)
                throw new ArgumentNullException("signalHandler");

            _signalHandler = signalHandler;

            _thread = new Thread(Run)
                      {
                          Name = name
                      };

            _thread.Start();
        }

        public override int MaximumConcurrencyLevel
        {
            get { return 1; }
        }

        #region IDisposable Members

        public void Dispose()
        {
            lock (_lock)
            {
                _isDone = true;
                Monitor.PulseAll(_lock);
                _signalHandler = null;
            }

            if (null != _thread)
                _thread.Join();

            if (null != _tasks)
            {
                // Could we cancel of fail them somehow?
                _tasks.Clear();
            }
        }

        #endregion

        public void Signal()
        {
            lock (_lock)
            {
                if (_isSignaled)
                    return;

                _isSignaled = true;

                Monitor.Pulse(_lock);
            }
        }

        void Run()
        {
            try
            {
                for (; ; )
                {
                    Action signalHandler;
                    Task task;
                    var wasSignaled = false;

                    lock (_lock)
                    {
                        for (; ; )
                        {
                            if (_isDone || null == _signalHandler)
                                return;

                            signalHandler = _signalHandler;

                            var haveWork = false;
                            task = null;

                            if (_tasks.Count > 0)
                            {
                                task = _tasks.Dequeue();
                                haveWork = true;
                            }

                            if (_isSignaled)
                            {
                                _isSignaled = false;
                                wasSignaled = true;
                                haveWork = true;
                            }

                            if (haveWork)
                                break;

                            Monitor.Wait(_lock);
                        }
                    }

                    if (wasSignaled)
                    {
                        var signalTask = new Task(signalHandler);

                        signalTask.RunSynchronously(this);
                    }

                    if (null != task)
                        TryExecuteTask(task);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SingleThreadSignalTaskScheduler.Run() failed: " + ex.Message);

                // Kill the app...?
            }
        }

        protected override void QueueTask(Task task)
        {
            lock (_lock)
            {
                _tasks.Enqueue(task);

                Monitor.Pulse(_lock);
            }
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            lock (_lock)
            {
                return _tasks.ToArray();
            }
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            if (Thread.CurrentThread != _thread)
                return false;

            if (taskWasPreviouslyQueued)
                return false;

            return TryExecuteTask(task);
        }

        [Conditional("DEBUG")]
        public void ThrowIfNotOnThread()
        {
            if (Thread.CurrentThread != _thread)
                throw new InvalidOperationException("Not running on worker thread");
        }
    }
}

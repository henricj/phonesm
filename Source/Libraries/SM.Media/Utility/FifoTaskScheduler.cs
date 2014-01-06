// -----------------------------------------------------------------------
//  <copyright file="FifoTaskScheduler.cs" company="Henric Jungheim">
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SM.Media.Utility
{
    public sealed class FifoTaskScheduler : TaskScheduler, IDisposable
    {
        // See LimitedConcurrencyLevelTaskScheduler in ParallelExtensionsExtras.  We don't
        // have ThreadPool in this PCL, so we use SignalTask (which uses the default scheduler).
        readonly LinkedList<Task> _tasks = new LinkedList<Task>();
        readonly SignalTask _workerTask;

        public FifoTaskScheduler(CancellationToken cancellationToken)
        {
            _workerTask = new SignalTask(Worker, cancellationToken);
        }

        public override int MaximumConcurrencyLevel
        {
            get { return 1; }
        }

        Task Worker()
        {
            try
            {
                for (; ; )
                {
                    Task task;

                    lock (_tasks)
                    {
                        if (0 == _tasks.Count)
                            return TplTaskExtensions.CompletedTask;

                        task = _tasks.First.Value;
                        _tasks.RemoveFirst();
                    }

                    TryExecuteTask(task);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("FifoTaskScheduler.Worker() failed " + ex.Message);
            }

            return TplTaskExtensions.CompletedTask;
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            lock (_tasks)
            {
                return _tasks.ToArray();
            }
        }

        protected override void QueueTask(Task task)
        {
            lock (_tasks)
            {
                _tasks.AddLast(task);
            }

            _workerTask.Fire();
        }

        protected override bool TryDequeue(Task task)
        {
            lock (_tasks)
            {
                return _tasks.Remove(task);
            }
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            return false;
        }

        public void Dispose()
        {
            using (_workerTask)
            { }

            Task[] tasks;

            lock (_tasks)
            {
                tasks = _tasks.ToArray();
                _tasks.Clear();
            }

            Debug.Assert(0 == tasks.Length, "FifoTaskScheduler: Pending tasks abandoned");
        }
    }
}

// -----------------------------------------------------------------------
//  <copyright file="TaskCommandWorker.cs" company="Henric Jungheim">
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
using System.Threading;
using System.Threading.Tasks;

namespace SM.Media.Utility
{
    public sealed class TaskCommandWorker : ICommandWorker
    {
        readonly Queue<WorkCommand> _commandQueue = new Queue<WorkCommand>();
        readonly TaskCompletionSource<bool> _workerClosedTaskCompletionSource = new TaskCompletionSource<bool>();
        bool _isClosed;
        bool _managerRunning;
        Task _managerTask;

        #region ICommandWorker Members

        public void Dispose()
        {
            Task waitTask = null;

            lock (_commandQueue)
            {
                _isClosed = true;

                if (null != _managerTask && !_managerTask.IsCompleted && _managerRunning)
                    waitTask = _managerTask;
            }

            if (null != waitTask)
                waitTask.Wait();
        }

        public void SendCommand(WorkCommand command)
        {
            lock (_commandQueue)
            {
                if (_isClosed)
                    throw new ObjectDisposedException("TaskCommandWorker");

                _commandQueue.Enqueue(command);

                UnlockedPokeWorker();
            }
        }

        public Task CloseAsync()
        {
            lock (_commandQueue)
            {
                var wasClosed = _isClosed;

                _isClosed = true;

                if (!wasClosed)
                    UnlockedPokeWorker();

                return _workerClosedTaskCompletionSource.Task;
            }
        }

        #endregion

        void UnlockedPokeWorker()
        {
            if (null == _managerTask || _managerTask.IsCompleted || !_managerRunning)
            {
                _managerRunning = true;
                _managerTask = Task.Factory.StartNew((Func<Task>)ManageAsync, CancellationToken.None, TaskCreationOptions.PreferFairness, TaskScheduler.Default)
                                   .Unwrap();
            }
        }

        async Task ManageAsync()
        {
            var commands = new List<WorkCommand>();

            for (; ; )
            {
                commands.Clear();

                lock (_commandQueue)
                {
                    while (_commandQueue.Count > 0)
                        commands.Add(_commandQueue.Dequeue());

                    if (commands.Count < 1)
                    {
                        if (_isClosed && !_workerClosedTaskCompletionSource.Task.IsCompleted)
                            _workerClosedTaskCompletionSource.SetResult(true);

                        _managerRunning = false;
                        return;
                    }
                }

                await CommandWorkerBase.RunCommands(commands).ConfigureAwait(false);
            }
        }
    }
}

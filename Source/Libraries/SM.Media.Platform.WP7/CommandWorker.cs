//-----------------------------------------------------------------------
// <copyright file="CommandWorker.cs" company="Henric Jungheim">
// Copyright (c) 2012.
// <author>Henric Jungheim</author>
// </copyright>
//-----------------------------------------------------------------------
// Copyright (c) 2012 Henric Jungheim <software@henric.org> 
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
using System.Threading.Tasks;

namespace SM.Media
{
    sealed class CommandWorker : IDisposable
    {
        readonly Queue<Command> _commandQueue = new Queue<Command>();
        bool _isClosed;
        bool _managerRunning;
        Task _managerTask;

        public void Dispose()
        {
            lock (_commandQueue)
            {
                _isClosed = true;

                if (null != _managerTask && !_managerTask.IsCompleted && _managerRunning)
                {
                    _managerTask.Wait();
                }
            }
        }

        public void SendCommand(Command command)
        {
            lock (_commandQueue)
            {
                if (_isClosed)
                    throw new ObjectDisposedException("CommandWorker");

                _commandQueue.Enqueue(command);

                if (null == _managerTask || _managerTask.IsCompleted || !_managerRunning)
                {
                    _managerRunning = true;
                    _managerTask = Task.Factory.StartNew((Func<Task>)ManageAsync).Unwrap();
                }
            }
        }

        public Task CloseAsync()
        {
            lock (_commandQueue)
            {
                _isClosed = true;

                if (null == _managerTask)
                    return TplTaskExtensions.CompletedTask;

                return _managerTask;
            }
        }

        async Task ManageAsync()
        {
            var commands = new List<Command>();

            for (; ; )
            {
                commands.Clear();

                lock (_commandQueue)
                {
                    while (_commandQueue.Count > 0)
                    {
                        commands.Add(_commandQueue.Dequeue());
                    }

                    if (commands.Count < 1)
                    {
                        _managerRunning = false;
                        return;
                    }
                }

                foreach (var command in commands)
                {
                    var run = command.RunAsync;

                    var failed = false;

                    if (null != run)
                    {
                        try
                        {
                            var task = run();

                            if (null != task)
                                await task;
                        }
                        catch (OperationCanceledException)
                        {
                            failed = true;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("Command failed: " + ex.Message);
                            failed = true;
                        }
                    }

                    var callback = command.Complete;

                    if (null != callback)
                        callback(!failed);
                }
            }
        }

        public class Command
        {
            public readonly Action<bool> Complete;
            public readonly Func<Task> RunAsync;

            public Command(Func<Task> run, Action<bool> complete = null)
            {
                RunAsync = run;
                Complete = complete;
            }
        }
    }
}

// -----------------------------------------------------------------------
//  <copyright file="AsyncManualResetEvent.cs" company="Henric Jungheim">
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

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SM.Media.Utility
{
    /// <summary>
    ///     http://blogs.msdn.com/b/pfxteam/archive/2012/02/11/10266920.aspx
    /// </summary>
    public sealed class AsyncManualResetEvent
    {
        volatile TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>();

        public AsyncManualResetEvent(bool initialState = false)
        {
            if (!initialState)
                return;

            Set();
        }

        public Task WaitAsync()
        {
            return _tcs.Task;
        }

        public void Set() { _tcs.TrySetResult(true); }
        //public void Set()
        //{
        //    var tcs = _tcs;

        //    if (tcs.Task.IsCompleted)
        //        return;

        //    var t = Task.Factory.StartNew(s => ((TaskCompletionSource<bool>)s).TrySetResult(true),
        //        tcs, CancellationToken.None, TaskCreationOptions.PreferFairness, TaskScheduler.Default);

        //    TaskCollector.Default.Add(t, "AsyncManualResetEvent Set");

        //    tcs.Task.Wait();
        //}

        public void Reset()
        {
            var tcs = _tcs;

            if (!tcs.Task.IsCompleted)
                return;

            var newTcs = new TaskCompletionSource<bool>();

            while (true)
            {
#pragma warning disable 0420
                var currentTcs = Interlocked.CompareExchange(ref _tcs, newTcs, tcs);
#pragma warning restore 0420

                if (tcs == currentTcs)
                {
                    if (!tcs.Task.IsCompleted)
                    {
                        Debug.WriteLine("*** AsyncManualResetEvent.Reset(): task completion source was not completed");

                        tcs.TrySetResult(true);
                    }
                }

                tcs = currentTcs;

                if (!tcs.Task.IsCompleted)
                    return;
            }
        }
    }
}

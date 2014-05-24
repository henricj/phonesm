// -----------------------------------------------------------------------
//  <copyright file="DispatcherExtensions.cs" company="Henric Jungheim">
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
using System.Threading.Tasks;
using System.Windows.Threading;

namespace SM.Media.Utility
{
    public static class DispatcherExtensions
    {
        public static Task DispatchAsync(this Dispatcher dispatcher, Action action)
        {
            if (dispatcher.CheckAccess())
            {
                action();

                return TplTaskExtensions.CompletedTask;
            }

            var tcs = new TaskCompletionSource<bool>();

            var dispatcherObject = dispatcher.BeginInvoke(
                () =>
                {
                    try
                    {
                        action();

                        tcs.TrySetResult(true);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                });

            // Where is dispatcherObject.GetAwaiter()?

            return tcs.Task;
        }

        public static Task<T> DispatchAsync<T>(this Dispatcher dispatcher, Func<T> action)
        {
            if (dispatcher.CheckAccess())
                return TaskEx.FromResult(action());

            var tcs = new TaskCompletionSource<T>();

            var dispatcherObject = dispatcher.BeginInvoke(
                () =>
                {
                    try
                    {
                        var result = action();

                        tcs.TrySetResult(result);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                });

            // Where is dispatcherObject.GetAwaiter()?

            return tcs.Task;
        }

        public static Task DispatchAsync(this Dispatcher dispatcher, Func<Task> action)
        {
            if (dispatcher.CheckAccess())
                return action();

            var tcs = new TaskCompletionSource<bool>();

            var dispatcherObject = dispatcher.BeginInvoke(
                () => action()
                    .ContinueWith(t =>
                                  {
                                      if (t.IsCanceled)
                                          tcs.TrySetCanceled();

                                      if (t.IsFaulted)
                                          tcs.TrySetException(t.Exception);

                                      tcs.TrySetResult(true);
                                  }));

            // Where is dispatcherObject.GetAwaiter()?

            return tcs.Task;
        }

        public static Task<T> DispatchAsync<T>(this Dispatcher dispatcher, Func<Task<T>> action)
        {
            if (dispatcher.CheckAccess())
                return action();

            var tcs = new TaskCompletionSource<T>();

            var dispatcherObject = dispatcher.BeginInvoke(
                () => action()
                    .ContinueWith(t =>
                                  {
                                      if (t.IsCanceled)
                                          tcs.TrySetCanceled();

                                      if (t.IsFaulted)
                                          tcs.TrySetException(t.Exception);

                                      tcs.TrySetResult(t.Result);
                                  }));

            // Where is dispatcherObject.GetAwaiter()?

            return tcs.Task;
        }
    }
}

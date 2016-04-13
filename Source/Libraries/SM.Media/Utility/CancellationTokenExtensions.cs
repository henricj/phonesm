// -----------------------------------------------------------------------
//  <copyright file="CancellationTokenExtensions.cs" company="Henric Jungheim">
//  Copyright (c) 2012-2016.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012-2016 Henric Jungheim <software@henric.org>
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
    public static class CancellationTokenExtensions
    {
        static readonly Task CancelledTask;
        static readonly Task PendingTask = new TaskCompletionSource<object>().Task;

        static CancellationTokenExtensions()
        {
            var tcs = new TaskCompletionSource<object>();

            tcs.TrySetCanceled();

            CancelledTask = tcs.Task;
        }

        static async Task WaitAsync(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<object>();

            using (cancellationToken.Register(() => Task.Run(() => tcs.TrySetCanceled())))
            {
                await tcs.Task.ConfigureAwait(false);
            }
        }

        public static Task AsTask(this CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled)
                return PendingTask;

            if (cancellationToken.IsCancellationRequested)
                return CancelledTask;

            return WaitAsync(cancellationToken);
        }

        /// <summary>
        ///     Cancel then dispose without throwing any exceptions.
        /// </summary>
        /// <param name="cancellationTokenSource"></param>
        /// <returns></returns>
        public static void CancelDisposeSafe(this CancellationTokenSource cancellationTokenSource)
        {
            if (null == cancellationTokenSource)
                return;

            CancelSafe(cancellationTokenSource);

            cancellationTokenSource.DisposeSafe();
        }

        /// <summary>
        ///     Cancel without throwing any exceptions.
        /// </summary>
        public static void CancelSafe(this CancellationTokenSource cancellationTokenSource)
        {
            if (null == cancellationTokenSource)
                return;

            try
            {
                if (!cancellationTokenSource.IsCancellationRequested)
                    cancellationTokenSource.Cancel();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("CancellationTokenExtensions.CancelSafe() failed: " + ex.Message);
            }
        }

        /// <summary>
        ///     Cancel without throwing any exceptions on the default task scheduler.
        /// </summary>
        public static void BackgroundCancelSafe(this CancellationTokenSource cancellationTokenSource)
        {
            if (null == cancellationTokenSource)
                return;

            try
            {
                if (!cancellationTokenSource.IsCancellationRequested)
                {
                    var t = Task.Run(() =>
                    {
                        try
                        {
                            cancellationTokenSource.Cancel();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("CancellationTokenExtensions.BackgroundCancelSafe() cancel failed: " + ex.Message);
                        }
                    });

                    TaskCollector.Default.Add(t, "CancellationTokenExtensions BackgroundCancelSafe");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("CancellationTokenExtensions.BackgroundCancelSafe() failed: " + ex.Message);
            }
        }
    }
}

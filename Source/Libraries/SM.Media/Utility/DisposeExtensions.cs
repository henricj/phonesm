// -----------------------------------------------------------------------
//  <copyright file="DisposeExtensions.cs" company="Henric Jungheim">
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
using System.Diagnostics;
using System.Threading.Tasks;

namespace SM.Media.Utility
{
    public static class DisposeExtensions
    {
        /// <summary>
        ///     IDisposable.Dispose() should not throw exceptions; this will catch them if they do.
        /// </summary>
        /// <param name="disposable"></param>
        public static void DisposeSafe(this IDisposable disposable)
        {
            if (null == disposable)
                return;

            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("BackgroundDisposer.DisposeAsync() for {0} failed: {1}", disposable.GetType().FullName, ex.Message);
            }
        }

        /// <summary>
        ///     "Async over sync" wrapper for DisposeSafe().
        ///     See http://blogs.msdn.com/b/pfxteam/archive/2012/04/13/10293638.aspx
        /// </summary>
        /// <param name="disposable"></param>
        /// <returns></returns>
        public static Task DisposeAsync(this IDisposable disposable)
        {
            if (null == disposable)
                return TplTaskExtensions.CompletedTask;

            return TaskEx.Run(() => disposable.DisposeSafe());
        }

        /// <summary>
        ///     DisposeSafe() the object in the background ("async over sync").
        /// </summary>
        /// <param name="disposable"></param>
        /// <param name="description"></param>
        public static void DisposeBackground(this IDisposable disposable, string description)
        {
            TaskCollector.Default.Add(disposable.DisposeAsync(), description);
        }
    }
}

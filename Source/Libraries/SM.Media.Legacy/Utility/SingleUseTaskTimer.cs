// -----------------------------------------------------------------------
//  <copyright file="SingleUseTaskTimer.cs" company="Henric Jungheim">
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
using System.Threading;
using System.Threading.Tasks;

namespace SM.Media.Utility
{
    sealed class SingleUseTaskTimer : IDisposable
    {
        readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        // General idea from: http://stackoverflow.com/a/12790048
        // CancellationTokenSource is sealed on WP7...
        public SingleUseTaskTimer(Action callback, TimeSpan expiration)
        {
            TaskEx.Delay(expiration, _cancellationTokenSource.Token)
                  .ContinueWith(
                      t => callback(),
                      TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        #region IDisposable Members

        public void Dispose()
        {
            _cancellationTokenSource.Dispose();
        }

        #endregion

        public void Cancel()
        {
            _cancellationTokenSource.Cancel();
        }
    }
}

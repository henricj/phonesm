// -----------------------------------------------------------------------
//  <copyright file="TaskTimer.cs" company="Henric Jungheim">
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

namespace SM.Media.Utility
{
    public sealed class TaskTimer : IDisposable
    {
        SingleUseTaskTimer _timer;

        #region IDisposable Members

        public void Dispose()
        {
            Cancel();
        }

        #endregion

        public void SetTimer(Action callback, TimeSpan expiration)
        {
            var timer = new SingleUseTaskTimer(callback, expiration);

            SetTimer(timer);
        }

        public void Cancel()
        {
            SetTimer(null);
        }

        void SetTimer(SingleUseTaskTimer timer)
        {
            timer = Interlocked.Exchange(ref _timer, timer);

            if (null != timer)
                CleanupTimer(timer);
        }

        static void CleanupTimer(SingleUseTaskTimer timer)
        {
            try
            {
                timer.Cancel();

                timer.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Timer.Dispose(): " + ex.Message);
            }
        }
    }
}

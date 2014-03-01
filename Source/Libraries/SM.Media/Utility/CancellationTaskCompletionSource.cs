// -----------------------------------------------------------------------
//  <copyright file="CancellationTaskCompletionSource.cs" company="Henric Jungheim">
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
using System.Threading;
using System.Threading.Tasks;

namespace SM.Media.Utility
{
    public sealed class CancellationTaskCompletionSource<TItem> : IDisposable
    {
        readonly Action<CancellationTaskCompletionSource<TItem>> _cancellationAction;
        readonly TaskCompletionSource<TItem> _taskCompletionSource;
        CancellationTokenRegistration _cancellationTokenRegistration;

        public CancellationTaskCompletionSource(Action<CancellationTaskCompletionSource<TItem>> cancellationAction, CancellationToken cancellationToken)
        {
            if (null == cancellationAction)
                throw new ArgumentNullException("cancellationAction");

            _taskCompletionSource = new TaskCompletionSource<TItem>();

            _cancellationAction = cancellationAction;
            _cancellationTokenRegistration = cancellationToken.Register(obj => ((CancellationTaskCompletionSource<TItem>)obj).Cancel(), this);
        }

        public Task<TItem> Task
        {
            get { return _taskCompletionSource.Task; }
        }

        #region IDisposable Members

        public void Dispose()
        {
            _cancellationTokenRegistration.Dispose();

            if (null != _taskCompletionSource)
                _taskCompletionSource.TrySetCanceled();
        }

        #endregion

        public bool TrySetResult(TItem item)
        {
            return _taskCompletionSource.TrySetResult(item);
        }

        public bool TrySetException(Exception exception)
        {
            return _taskCompletionSource.TrySetException(exception);
        }

        public bool TrySetCanceled()
        {
            return _taskCompletionSource.TrySetCanceled();
        }

        void Cancel()
        {
            _cancellationAction(this);

            Dispose();
        }
    }
}

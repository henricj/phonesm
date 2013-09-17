// -----------------------------------------------------------------------
//  <copyright file="Retry.cs" company="Henric Jungheim">
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
using System.Threading.Tasks;

namespace SM.Media.Utility
{
    public class Retry
    {
        readonly int _delayMilliseconds;
        readonly int _maxRetries;
        readonly Func<Exception, bool> _retryableException;
        int _delay;
        int _retry;

        public Retry(int maxRetries, int delayMilliseconds, Func<Exception, bool> retryableException)
        {
            _maxRetries = maxRetries;
            _delayMilliseconds = delayMilliseconds;
            _retryableException = retryableException;
            _retry = 0;
            _delay = 0;
        }

        public async Task<TResult> CallAsync<TResult>(Func<Task<TResult>> operation, CancellationToken cancellationToken)
        {
            _retry = 0;
            _delay = _delayMilliseconds;

            for (; ; )
            {
                try
                {
                    return await operation().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (_retry >= _maxRetries || !_retryableException(ex))
                        throw;

                    ++_retry;

                    Debug.WriteLine("Retry {0} after: {1}", _retry, ex.Message);
                }

                await Delay(cancellationToken).ConfigureAwait(false);
            }
        }

        async Task Delay(CancellationToken cancellationToken)
        {
            var actualDelay = (int)(_delay * (0.5 + GlobalPlatformServices.Default.GetRandomNumber()));

            _delay += _delay;

#if WINDOWS_PHONE8
                await Task.Delay(actualDelay, cancellationToken).ConfigureAwait(false);
#else
            await TaskEx.Delay(actualDelay, cancellationToken).ConfigureAwait(false);
#endif
        }

        public async Task<bool> CanRetryAfterDelay(CancellationToken cancellationToken)
        {
            if (_retry >= _maxRetries)
                return false;

            ++_retry;

            await Delay(cancellationToken).ConfigureAwait(false);

            return true;
        }
    }

    public static class RetryExtensions
    {
        public static Task CallAsync(this Retry retry, Func<Task> operation, CancellationToken cancellationToken)
        {
            return retry.CallAsync(async () =>
                                         {
                                             await operation().ConfigureAwait(false);
                                             return 0;
                                         }, cancellationToken);
        }
    }
}

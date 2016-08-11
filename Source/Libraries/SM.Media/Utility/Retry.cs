// -----------------------------------------------------------------------
//  <copyright file="Retry.cs" company="Henric Jungheim">
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SM.Media.Utility
{
    public interface IRetry
    {
        Task<TResult> CallAsync<TResult>(Func<Task<TResult>> operation, CancellationToken cancellationToken);
        Task<bool> CanRetryAfterDelayAsync(CancellationToken cancellationToken);
    }

    public class Retry : IRetry
    {
        static readonly IEnumerable<Exception> NoExceptions = new Exception[0];
        readonly int _delayMilliseconds;
        readonly int _maxRetries;
        readonly IPlatformServices _platformServices;
        readonly Func<Exception, bool> _retryableException;
        int _delay;
        List<Exception> _exceptions;
        int _retry;

        public Retry(int maxRetries, int delayMilliseconds, Func<Exception, bool> retryableException, IPlatformServices platformServices)
        {
            if (maxRetries < 1)
                throw new ArgumentOutOfRangeException(nameof(maxRetries), "The number of retries must be positive.");
            if (delayMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(delayMilliseconds), "The delay cannot be negative");
            if (null == retryableException)
                throw new ArgumentNullException(nameof(retryableException));
            if (null == platformServices)
                throw new ArgumentNullException(nameof(platformServices));

            _maxRetries = maxRetries;
            _delayMilliseconds = delayMilliseconds;
            _retryableException = retryableException;
            _platformServices = platformServices;
            _retry = 0;
            _delay = 0;
        }

        #region IRetry Members

        public async Task<TResult> CallAsync<TResult>(Func<Task<TResult>> operation, CancellationToken cancellationToken)
        {
            _retry = 0;
            _delay = _delayMilliseconds;

            if (null != _exceptions)
                _exceptions.Clear();

            for (; ; )
            {
                try
                {
                    return await operation().ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    if (!_retryableException(ex))
                        throw;

                    if (null == _exceptions)
                        _exceptions = new List<Exception>();

                    _exceptions.Add(ex);

                    if (++_retry >= _maxRetries)
                        throw new RetryException("Giving up after " + _retry + " retries", _exceptions);

                    Debug.WriteLine("Retry {0} after: {1}", _retry, ex.Message);
                }

                await DelayAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task<bool> CanRetryAfterDelayAsync(CancellationToken cancellationToken)
        {
            if (_retry >= _maxRetries)
                return false;

            ++_retry;

            await DelayAsync(cancellationToken).ConfigureAwait(false);

            return true;
        }

        #endregion

        async Task DelayAsync(CancellationToken cancellationToken)
        {
            var actualDelay = (int)(_delay * (0.5 + _platformServices.GetRandomNumber()));

            _delay += _delay;

            await Task.Delay(actualDelay, cancellationToken).ConfigureAwait(false);
        }
    }

    public static class RetryExtensions
    {
        public static Task CallAsync(this IRetry retry, Func<Task> operation, CancellationToken cancellationToken)
        {
            return retry.CallAsync(async () =>
            {
                await operation().ConfigureAwait(false);
                return 0;
            }, cancellationToken);
        }
    }
}

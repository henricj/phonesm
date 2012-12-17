// -----------------------------------------------------------------------
//  <copyright file="Retry.cs" company="Henric Jungheim">
//  Copyright (c) 2012.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
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
using System.Diagnostics;
using System.Threading.Tasks;

namespace SM.Media.Utility
{
    public struct Retry
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

        public async Task<TResult> CallAsync<TResult>(Func<Task<TResult>> operation)
        {
            _retry = 0;
            _delay = _delayMilliseconds;

            for (; ; )
            {
                try
                {
                    return await operation();
                }
                catch (Exception ex)
                {
                    if (++_retry > _maxRetries || !_retryableException(ex))
                        throw;

                    Debug.WriteLine("Retry {0} after: {1}", _retry, ex.Message);
                }

                var actualDelay = (int)(_delay * (0.5 + GlobalPlatformServices.Default.GetRandomNumber()));

                _delay += _delay;

#if WINDOWS_PHONE8
                await Task.Delay(actualDelay);
#else
                await TaskEx.Delay(actualDelay);
#endif
            }
        }

        public async Task<TResult> Call<TResult>(Func<TResult> operation)
        {
            _retry = 0;
            _delay = _delayMilliseconds;

            for (; ; )
            {
                try
                {
                    return operation();
                }
                catch (Exception ex)
                {
                    if (++_retry > _maxRetries || !_retryableException(ex))
                        throw;

                    Debug.WriteLine("Retry {0} after: {1}", _retry, ex.Message);
                }

                var actualDelay = (int)(_delay * (0.5 + GlobalPlatformServices.Default.GetRandomNumber()));

                _delay += _delay;

#if WINDOWS_PHONE8
                await Task.Delay(actualDelay);
#else
                await TaskEx.Delay(actualDelay);
#endif
            }
        }
    }

    public static class RetryExtensions
    {
        public static Task CallAsync(this Retry retry, Func<Task> operation)
        {
            return retry.CallAsync(async () =>
                                    {
                                        await operation();
                                        return 0;
                                    });
        }

        public static Task Call(this Retry retry, Action operation)
        {
            return retry.Call(() =>
                              {
                                  operation();
                                  return 0;
                              });
        }
    }
}

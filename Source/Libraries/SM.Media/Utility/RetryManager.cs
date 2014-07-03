// -----------------------------------------------------------------------
//  <copyright file="RetryManager.cs" company="Henric Jungheim">
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

namespace SM.Media.Utility
{
    public interface IRetryManager
    {
        IRetry CreateRetry(int maxRetries, int delayMilliseconds, Func<Exception, bool> retryableException);
    }

    public class RetryManager : IRetryManager
    {
        readonly IPlatformServices _platformServices;

        public RetryManager(IPlatformServices platformServices)
        {
            if (null == platformServices)
                throw new ArgumentNullException("platformServices");

            _platformServices = platformServices;
        }

        #region IRetryManager Members

        public IRetry CreateRetry(int maxRetries, int delayMilliseconds, Func<Exception, bool> retryableException)
        {
            return new Retry(maxRetries, delayMilliseconds, retryableException, _platformServices);
        }

        #endregion
    }

    public static class RetryManagerExtensions
    {
        public static IRetry CreateWebRetry(this IRetryManager retryManager, int maxRetries, int delayMilliseconds)
        {
            return retryManager.CreateRetry(maxRetries, delayMilliseconds, RetryPolicy.IsWebExceptionRetryable);
        }
    }
}

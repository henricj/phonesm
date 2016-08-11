// -----------------------------------------------------------------------
//  <copyright file="HttpClientExceptionTranslator.cs" company="Henric Jungheim">
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
using System.Net;
using System.Threading;
using Windows.Web.Http;

namespace SM.Media.WinRtHttpClientReader
{
    public static class HttpClientExceptionTranslator
    {
        /// <summary>
        ///     WinRT's <see cref="HttpClient" /> often throws exceptions of type <see cref="Exception" /> that
        ///     are distinguishable only by <see cref="Exception.HResult" />.  We try to translate these into
        /// </summary>
        /// <param name="exception">An exception thrown by <see cref="HttpClient" /></param>
        /// <param name="cancellationToken">is the <see cref="CancellationToken" /> used for the failed operation</param>
        /// <returns>null or a more suitable exception</returns>
        public static Exception FindBetterHttpClientException(Exception exception, CancellationToken cancellationToken)
        {
            if (null == exception)
                throw new ArgumentNullException(nameof(exception));

            if (exception is OperationCanceledException || exception is ObjectDisposedException
                || exception is OutOfMemoryException || exception is ArgumentException)
            {
                return null;
            }

            switch ((uint)exception.HResult)
            {
                // http://msdn.microsoft.com/en-us/library/windows/apps/dn298645
                case 123: // ERROR_INVALID_NAME
                    return new ArgumentException(exception.Message, exception);
                case 0x80070057: // E_INVALIDARG
                    return new ArgumentNullException(exception.Message, exception);
                case 0x80072EFD: // WININET_E_CANNOT_CONNECT
                    if (cancellationToken.IsCancellationRequested)
                        return new OperationCanceledException(exception.Message, exception, cancellationToken);

                    return new WebException("Cannot connect", exception, WebExceptionStatus.ConnectFailure, null);
            }

            return new WebException(exception.Message, exception);
        }

        /// <summary>
        ///     WinRT's <see cref="HttpClient" /> often throws exceptions of type <see cref="Exception" /> that
        ///     are distinguishable only by <see cref="Exception.HResult" />.  We try to translate these into
        /// </summary>
        /// <param name="exception">An exception thrown by <see cref="HttpClient" /></param>
        /// <param name="cancellationToken">is the <see cref="CancellationToken" /> used for the failed operation</param>
        public static void ThrowBetterHttpClientException(Exception exception, CancellationToken cancellationToken)
        {
            var better = FindBetterHttpClientException(exception, cancellationToken);

            if (null != better)
                throw better;
        }
    }
}

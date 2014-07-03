// -----------------------------------------------------------------------
//  <copyright file="RetryPolicy.cs" company="Henric Jungheim">
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
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;

namespace SM.Media.Utility
{
    public static class RetryPolicy
    {
        static HttpStatusCode[] RetryCodes = new[]
                                             {
                                                 HttpStatusCode.GatewayTimeout,
                                                 HttpStatusCode.RequestTimeout,
                                                 HttpStatusCode.InternalServerError
                                             }.OrderBy(v => v).ToArray();

        static readonly WebExceptionStatus[] WebRetryCodes = new[]
                                                             {
                                                                 WebExceptionStatus.ConnectFailure,
                                                                 WebExceptionStatus.SendFailure
                                                             }.OrderBy(v => v).ToArray();

        public static bool IsRetryable(HttpStatusCode code)
        {
            return Array.BinarySearch(RetryCodes, code) >= 0;
        }

        public static bool IsRetryable(WebExceptionStatus code)
        {
            return Array.BinarySearch(WebRetryCodes, code) >= 0;
        }

        public static bool IsWebExceptionRetryable(Exception ex)
        {
            var webException = ex as WebException;
            if (null == webException)
                return false;

            if (IsRetryable(webException.Status))
                return true;

            var httpResponse = webException.Response as HttpWebResponse;
            if (null == httpResponse)
                return false;

            return IsRetryable(httpResponse.StatusCode);
        }

        public static void ChangeRetryableStatusCodes(IEnumerable<HttpStatusCode> addCodes, IEnumerable<HttpStatusCode> removeCodes)
        {
            // No HashSet<> in PCLs...
            var hs = new Dictionary<HttpStatusCode, bool>();

            for (; ; )
            {
                var retryCodes = RetryCodes;

                foreach (var code in retryCodes)
                    hs[code] = true;

                if (null != addCodes)
                {
                    foreach (var code in addCodes)
                        hs[code] = true;
                }

                if (null != removeCodes)
                {
                    foreach (var code in removeCodes)
                        hs.Remove(code);
                }

                var newRetryCodes = hs.Keys.OrderBy(v => v).ToArray();

                if (retryCodes == Interlocked.CompareExchange(ref RetryCodes, newRetryCodes, retryCodes))
                    return;

                hs.Clear();
            }
        }
    }
}

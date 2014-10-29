// -----------------------------------------------------------------------
//  <copyright file="WinRtHttpClientExtensions.cs" company="Henric Jungheim">
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
using System.Net.Http.Headers;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Web.Http;

namespace SM.Media.WinRtHttpClientReader
{
    public static class WinRtHttpClientExtensions
    {
        public static async Task<HttpResponseMessage> SendAsync(this HttpClient httpClient, HttpRequestMessage request,
            HttpCompletionOption completionOption, CancellationToken cancellationToken,
            Uri referrer, long? fromBytes, long? toBytes)
        {
            if (null != referrer)
                request.Headers.Referer = referrer;

            if (null != fromBytes || null != toBytes)
                request.Headers["Range"] = new RangeHeaderValue(fromBytes, toBytes).ToString();

            return await SendRequestAsync(httpClient, request, completionOption, cancellationToken);
        }

        public static async Task<HttpResponseMessage> SendRequestAsync(this HttpClient httpClient, HttpRequestMessage request,
            HttpCompletionOption completionOption, CancellationToken cancellationToken)
        {
            try
            {
                return await httpClient.SendRequestAsync(request, completionOption).AsTask(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                HttpClientExceptionTranslator.ThrowBetterHttpClientException(ex, cancellationToken);

                throw;
            }
        }

        public static async Task<HttpResponseMessage> GetAsync(this HttpClient httpClient, Uri uri,
            HttpCompletionOption completionOption, CancellationToken cancellationToken)
        {
            try
            {
                return await httpClient.GetAsync(uri, completionOption).AsTask(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                HttpClientExceptionTranslator.ThrowBetterHttpClientException(ex, cancellationToken);

                throw;
            }
        }

        public static async Task<byte[]> ReadAsByteArray(this IHttpContent httpContent, CancellationToken cancellationToken)
        {
            try
            {
                var buffer = await httpContent.ReadAsBufferAsync().AsTask(cancellationToken).ConfigureAwait(false);

                return buffer.ToArray();
            }
            catch (Exception ex)
            {
                HttpClientExceptionTranslator.ThrowBetterHttpClientException(ex, cancellationToken);

                throw;
            }
        }
    }
}

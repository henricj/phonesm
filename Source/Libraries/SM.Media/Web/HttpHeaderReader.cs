// -----------------------------------------------------------------------
//  <copyright file="HttpHeaderReader.cs" company="Henric Jungheim">
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
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using SM.Media.Utility;

namespace SM.Media.Web
{
    public class HttpHeaderReaderResults
    {
        public Uri Url { get; internal set; }

        public HttpResponseHeaders ResponseHeaders { get; internal set; }

        public HttpContentHeaders ContentHeaders { get; internal set; }
    }

    public interface IHttpHeaderReader
    {
        Task<HttpHeaderReaderResults> GetHeadersAsync(Uri source, bool tryHead, CancellationToken cancellationToken);
    }

    public class HttpHeaderReader : IHttpHeaderReader
    {
        readonly IHttpClients _httpClients;

        public HttpHeaderReader(IHttpClients httpClients)
        {
            _httpClients = httpClients;
        }

        #region IHttpHeaderReader Members

        public virtual async Task<HttpHeaderReaderResults> GetHeadersAsync(Uri source, bool tryHead, CancellationToken cancellationToken)
        {
            using (var httpClient = _httpClients.CreateSegmentClient(source))
            {
                httpClient.DefaultRequestHeaders.Accept.Clear();
                httpClient.DefaultRequestHeaders.Referrer = null;

                if (tryHead)
                {
                    try
                    {
                        var headers = await GetHeadersAsync(httpClient, HttpMethod.Head, source, cancellationToken).ConfigureAwait(false);

                        if (null != headers)
                            return headers;
                    }
                    catch (HttpRequestException ex)
                    {
                        Debug.WriteLine("SegmentManagerFactory.CreateAsync() HEAD request failed: " + ex.Message);
                    }
                }

                try
                {
                    return await GetHeadersAsync(httpClient, HttpMethod.Get, source, cancellationToken).ConfigureAwait(false);
                }
                catch (HttpRequestException ex)
                {
                    Debug.WriteLine("SegmentManagerFactory.CreateAsync() GET request failed: " + ex.Message);
                }
            }

            return null;
        }

        #endregion

        protected virtual Task<HttpHeaderReaderResults> GetHeadersAsync(HttpClient httpClient, HttpMethod method, Uri source, CancellationToken cancellationToken)
        {
            return new Retry(2, 200, RetryPolicy.IsWebExceptionRetryable)
                .CallAsync(
                    async () =>
                    {
                        using (var request = new HttpRequestMessage(method, source))
                        {
                            var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

                            if (response.IsSuccessStatusCode)
                            {
                                return new HttpHeaderReaderResults
                                       {
                                           Url = request.RequestUri,
                                           ResponseHeaders = response.Headers,
                                           ContentHeaders = response.Content.Headers
                                       };
                            }
                        }

                        return null;
                    }, cancellationToken);
        }
    }
}

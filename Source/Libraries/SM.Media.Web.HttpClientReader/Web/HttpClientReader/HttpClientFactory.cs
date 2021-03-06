// -----------------------------------------------------------------------
//  <copyright file="HttpClientFactory.cs" company="Henric Jungheim">
//  Copyright (c) 2012-2015.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012-2015 Henric Jungheim <software@henric.org>
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
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using SM.Media.Content;
using SM.Media.Utility;

namespace SM.Media.Web.HttpClientReader
{
    public class HttpClientFactory : IHttpClientFactory
    {
        readonly CookieContainer _cookieContainer;
        readonly ICredentials _credentials;
        readonly Func<HttpClientHandler> _httpClientHandlerFactory;
        readonly Uri _referrer;
        readonly ProductInfoHeaderValue _userAgent;
        readonly IWebReaderManagerParameters _webReaderManagerParameters;
        int _disposed;

        public HttpClientFactory(IHttpClientFactoryParameters parameters, IWebReaderManagerParameters webReaderManagerParameters, IProductInfoHeaderValueFactory userAgentFactory, Func<HttpClientHandler> httpClientHandlerFactory)
        {
            if (null == parameters)
                throw new ArgumentNullException(nameof(parameters));
            if (null == webReaderManagerParameters)
                throw new ArgumentNullException(nameof(webReaderManagerParameters));
            if (null == userAgentFactory)
                throw new ArgumentNullException(nameof(userAgentFactory));
            if (null == httpClientHandlerFactory)
                throw new ArgumentNullException(nameof(httpClientHandlerFactory));

            _referrer = parameters.Referrer;
            _userAgent = userAgentFactory.Create();
            _credentials = parameters.Credentials;
            _cookieContainer = parameters.CookieContainer;

            _webReaderManagerParameters = webReaderManagerParameters;
            _httpClientHandlerFactory = httpClientHandlerFactory;
        }

        #region IHttpClientFactory Members

        public void Dispose()
        {
            if (0 != Interlocked.Exchange(ref _disposed, 1))
                return;

            Dispose(true);

            GC.SuppressFinalize(this);
        }

        public virtual HttpClient CreateClient(Uri baseAddress, Uri referrer = null, ContentKind contentKind = ContentKind.Unknown, ContentType contentType = null)
        {
            if (null == referrer && baseAddress != _referrer)
                referrer = _referrer;

            var httpClient = CreateHttpClient(baseAddress, referrer);

            if (null != contentType)
            {
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType.MimeType));

                if (null != contentType.AlternateMimeTypes)
                {
                    foreach (var mimeType in contentType.AlternateMimeTypes)
                        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(mimeType));
                }

                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*", 0.1));
            }

            return httpClient;
        }

        #endregion

        protected virtual HttpMessageHandler CreateClientHandler()
        {
            var httpClientHandler = _httpClientHandlerFactory();

            if (httpClientHandler.SupportsAutomaticDecompression)
                httpClientHandler.AutomaticDecompression = DecompressionMethods.GZip;

            if (null != _credentials)
                httpClientHandler.Credentials = _credentials;

            if (null != _cookieContainer)
                httpClientHandler.CookieContainer = _cookieContainer;
            else
                httpClientHandler.UseCookies = false;

            return httpClientHandler;
        }

        protected virtual HttpClient CreateHttpClient(Uri baseAddress, Uri referrer)
        {
            var httpClient = new HttpClient(CreateClientHandler());

            var headers = httpClient.DefaultRequestHeaders;

            if (null != baseAddress)
                httpClient.BaseAddress = baseAddress;

            if (null != referrer)
            {
                if (null == baseAddress)
                    httpClient.BaseAddress = referrer;

                headers.Referrer = referrer;
            }

            if (null != _userAgent)
                headers.UserAgent.Add(_userAgent);

            if (null != _webReaderManagerParameters.DefaultHeaders)
            {
                foreach (var header in _webReaderManagerParameters.DefaultHeaders)
                {
                    try
                    {
                        headers.Add(header.Key, header.Value);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("HttpClientFactory.CreateHttpClient({0}) header {1}={2} failed: {3}",
                            baseAddress, header.Key, header.Value, ex.ExtendedMessage());
                    }
                }
            }

            return httpClient;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;
        }
    }
}

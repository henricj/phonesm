// -----------------------------------------------------------------------
//  <copyright file="HttpClients.cs" company="Henric Jungheim">
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
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using SM.Media.Content;

namespace SM.Media.Web.HttpClientReader
{
    public class HttpClients : IHttpClients
    {
        readonly CookieContainer _cookieContainer;
        readonly ICredentials _credentials;
        readonly Uri _referrer;
        readonly ProductInfoHeaderValue _userAgent;
        int _disposed;
        HttpClient _rootPlaylistClient;
        readonly Func<HttpClientHandler> _httpClientHandlerFactory; 

        public HttpClients(IHttpClientsParameters parameters, Func<HttpClientHandler> httpClientHandlerFactory)
        {
            if (null == parameters)
                throw new ArgumentNullException("parameters");
            if (null == httpClientHandlerFactory)
                throw new ArgumentNullException("httpClientHandlerFactory");

            _referrer = parameters.Referrer;
            _userAgent = parameters.UserAgent;
            _credentials = parameters.Credentials;
            _cookieContainer = parameters.CookieContainer;

            _httpClientHandlerFactory = httpClientHandlerFactory;
        }

        #region IHttpClients Members

        public void Dispose()
        {
            if (0 != Interlocked.Exchange(ref _disposed, 1))
                return;

            Dispose(true);

            GC.SuppressFinalize(this);
        }

        public virtual HttpClient RootPlaylistClient
        {
            get
            {
                if (null == _rootPlaylistClient)
                    _rootPlaylistClient = CreateClient(_referrer);

                return _rootPlaylistClient;
            }
        }

        public virtual HttpClient CreateClient(Uri baseAddress, Uri referrer = null, ContentType contentType = null)
        {
            var httpClient = CreateHttpClient(baseAddress, referrer);

            if (null != contentType)
            {
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType.MimeType));

                if (null != contentType.AlternateMimeTypes)
                {
                    foreach (var mimeType in contentType.AlternateMimeTypes)
                        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(mimeType));
                }
            }

            return httpClient;
        }

        #endregion

        protected virtual HttpMessageHandler CreateClientHandler()
        {
            var httpClientHandler = _httpClientHandlerFactory();

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

            return httpClient;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            var rootClient = _rootPlaylistClient;

            if (null != rootClient)
            {
                _rootPlaylistClient = null;

                rootClient.Dispose();
            }
        }
    }
}

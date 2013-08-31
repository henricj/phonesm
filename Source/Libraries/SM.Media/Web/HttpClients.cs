// -----------------------------------------------------------------------
//  <copyright file="HttpClients.cs" company="Henric Jungheim">
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
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;

namespace SM.Media.Web
{
    public class HttpClients : IHttpClients, IDisposable
    {
        public static readonly MediaTypeWithQualityHeaderValue AcceptMpegurlHeader = new MediaTypeWithQualityHeaderValue("application/vnd.apple.mpegurl");
        public static readonly MediaTypeWithQualityHeaderValue AcceptMp2tHeader = new MediaTypeWithQualityHeaderValue("video/MP2T");
        public static readonly MediaTypeWithQualityHeaderValue AcceptMp3Header = new MediaTypeWithQualityHeaderValue("audio/mpeg");
        public static readonly MediaTypeWithQualityHeaderValue AcceptOctetHeader = new MediaTypeWithQualityHeaderValue("application/octet-stream");
        public static readonly MediaTypeWithQualityHeaderValue AcceptAnyHeader = new MediaTypeWithQualityHeaderValue("*/*");
        readonly CookieContainer _cookieContainer;
        readonly ICredentials _credentials;

        readonly Uri _referrer;
        readonly ProductInfoHeaderValue _userAgent;
        int _disposed;
        HttpClient _rootPlaylistClient;

        public HttpClients(Uri referrer = null, ProductInfoHeaderValue userAgent = null, ICredentials credentials = null, CookieContainer cookieContainer = null)
        {
            _referrer = referrer;
            _userAgent = userAgent;
            _credentials = credentials;
            _cookieContainer = cookieContainer;
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (0 != Interlocked.Exchange(ref _disposed, 1))
                return;

            Dispose(true);

            GC.SuppressFinalize(this);
        }

        #endregion

        #region IHttpClients Members

        public virtual HttpClient RootPlaylistClient
        {
            get
            {
                if (null == _rootPlaylistClient)
                    _rootPlaylistClient = CreatePlaylistHttpClient(_referrer);

                return _rootPlaylistClient;
            }
        }

        public virtual HttpClient CreatePlaylistClient(Uri referrer)
        {
            return CreatePlaylistHttpClient(referrer);
        }

        public virtual HttpClient CreateSegmentClient(Uri segmentPlaylist /*, MediaTypeWithQualityHeaderValue mediaType = null*/)
        {
            var httpClient = CreateHttpClient(segmentPlaylist);

            //if (null == mediaType)
            //    return httpClient;

            //var headers = httpClient.DefaultRequestHeaders;

            //headers.Accept.Add(mediaType);
            //headers.Accept.Add(AcceptAnyHeader);

            return httpClient;
        }

        public virtual HttpClient CreateBinaryClient(Uri referrer)
        {
            var httpClient = CreateHttpClient(referrer);

            var headers = httpClient.DefaultRequestHeaders;

            headers.Accept.Add(AcceptOctetHeader);

            return httpClient;
        }

        #endregion

        protected virtual HttpClientHandler CreateClientHandler()
        {
            var httpClientHandler = new HttpClientHandler();

            if (null != _credentials)
                httpClientHandler.Credentials = _credentials;

            if (null != _cookieContainer)
                httpClientHandler.CookieContainer = _cookieContainer;
            else
                httpClientHandler.UseCookies = false;

            return httpClientHandler;
        }

        protected virtual HttpClient CreatePlaylistHttpClient(Uri referrer)
        {
            var httpClient = CreateHttpClient(referrer);

            var headers = httpClient.DefaultRequestHeaders;

            headers.Accept.Add(AcceptMpegurlHeader);
            headers.Accept.Add(AcceptAnyHeader);

            return httpClient;
        }

        protected virtual HttpClient CreateHttpClient(Uri referrer)
        {
            var httpClient = new HttpClient(CreateClientHandler());

            var headers = httpClient.DefaultRequestHeaders;

            if (null != referrer)
            {
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

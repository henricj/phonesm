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
        public static readonly MediaTypeWithQualityHeaderValue AcceptAnyHeader = new MediaTypeWithQualityHeaderValue("*/*");

        readonly HttpClientHandler _httpClientHandler;
        readonly ProductInfoHeaderValue _userAgent;
        int _disposed;

        public HttpClients(Uri referrer = null, ProductInfoHeaderValue userAgent = null, ICredentials credentials = null, CookieContainer cookieContainer = null)
        {
            _userAgent = userAgent;

            _httpClientHandler = new HttpClientHandler
                                 {
                                     UseCookies = false
                                 };

            if (null != credentials)
                _httpClientHandler.Credentials = credentials;

            if (null != cookieContainer)
            {
                _httpClientHandler.CookieContainer = cookieContainer;
                _httpClientHandler.UseCookies = true;
            }

            RootPlaylistClient = CreatePlaylistHttpClient(referrer);
        }

        #region IDisposable Members

        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        #endregion

        #region IHttpClients Members

        public virtual HttpClient RootPlaylistClient { get; private set; }

        public virtual HttpClient GetPlaylistClient(Uri referrer)
        {
            return CreatePlaylistHttpClient(referrer);
        }

        public virtual HttpClient GetSegmentClient(Uri segmentPlaylist)
        {
            var httpClient = new HttpClient(_httpClientHandler)
                             {
                                 BaseAddress = segmentPlaylist
                             };

            var headers = httpClient.DefaultRequestHeaders;

            if (null != segmentPlaylist)
                headers.Referrer = segmentPlaylist;

            if (null != _userAgent)
                headers.UserAgent.Add(_userAgent);

            return httpClient;
        }

        #endregion

        protected virtual HttpClient CreatePlaylistHttpClient(Uri referrer)
        {
            var httpClient = new HttpClient(_httpClientHandler);

            var headers = httpClient.DefaultRequestHeaders;

            if (null != referrer)
            {
                httpClient.BaseAddress = referrer;
                headers.Referrer = referrer;
            }

            if (null != _userAgent)
                headers.UserAgent.Add(_userAgent);

            headers.Accept.Add(AcceptMpegurlHeader);
            headers.Accept.Add(AcceptAnyHeader);

            return httpClient;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            if (0 != Interlocked.Exchange(ref _disposed, 1))
                return;

            var rootClient = RootPlaylistClient;

            if (null != rootClient)
            {
                RootPlaylistClient = null;

                rootClient.Dispose();
            }
        }
    }
}

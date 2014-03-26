// -----------------------------------------------------------------------
//  <copyright file="SilverlightHttpClients.cs" company="Henric Jungheim">
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
using SM.Media.Content;
using SM.Media.Web.HttpClientReader;

namespace SM.Media.Web
{
    public sealed class SilverlightHttpClients : IHttpClients, IDisposable
    {
        HttpClient _rootHttpClient;

        #region IDisposable Members

        public void Dispose()
        {
            var client = _rootHttpClient;

            if (null != client)
            {
                _rootHttpClient = null;
                client.Dispose();
            }
        }

        #endregion

        #region IHttpClients Members

        public HttpClient RootPlaylistClient
        {
            get
            {
                if (null == _rootHttpClient)
                    _rootHttpClient = CreateHttpClient(null);

                return _rootHttpClient;
            }
        }

        public HttpClient CreateClient(Uri url, Uri referrer = null, ContentType contentType = null)
        {
            var httpClient = CreateHttpClient(referrer);

            //if (null != referrer)
            //    httpClient.DefaultRequestHeaders.Referrer = referrer;

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

        static HttpClient CreateHttpClient(Uri baseAddress)
        {
            var httpClientHandler = new HttpClientHandler();

            if (httpClientHandler.SupportsAutomaticDecompression)
                httpClientHandler.AutomaticDecompression = DecompressionMethods.GZip;

            var httpClient = new HttpClient(httpClientHandler)
                             {
                                 BaseAddress = baseAddress
                             };

            return httpClient;
        }
    }
}

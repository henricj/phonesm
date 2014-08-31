// -----------------------------------------------------------------------
//  <copyright file="HttpClientWebReaderManager.cs" company="Henric Jungheim">
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
using System.Threading;
using System.Threading.Tasks;
using SM.Media.Content;
using SM.Media.Utility;

namespace SM.Media.Web.HttpClientReader
{
    public class HttpClientWebReaderManager : IWebReaderManager, IDisposable
    {
        readonly IContentTypeDetector _contentTypeDetector;
        readonly IHttpClients _httpClients;
        readonly IRetryManager _retryManager;
        int _disposed;
        IWebReader _rootWebReader;

        public HttpClientWebReaderManager(IHttpClients httpClients, IContentTypeDetector contentTypeDetector, IRetryManager retryManager)
        {
            if (null == httpClients)
                throw new ArgumentNullException("httpClients");
            if (null == contentTypeDetector)
                throw new ArgumentNullException("contentTypeDetector");
            if (null == retryManager)
                throw new ArgumentNullException("retryManager");

            _httpClients = httpClients;
            _contentTypeDetector = contentTypeDetector;
            _retryManager = retryManager;
            _rootWebReader = new HttpClientWebReader(this, httpClients.RootPlaylistClient, null, _contentTypeDetector);
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

        #region IWebReaderManager Members

        public IWebReader RootWebReader
        {
            get { return _rootWebReader; }
        }

        public virtual IWebReader CreateReader(Uri url, ContentKind contentKind, IWebReader parent = null, ContentType contentType = null)
        {
            return CreateHttpClientWebReader(url, parent, contentType);
        }

        public virtual IWebCache CreateWebCache(Uri url, ContentKind contentKind, IWebReader parent = null, ContentType contentType = null)
        {
            var webReader = CreateHttpClientWebReader(url, parent, contentType);

            return new HttpClientWebCache(webReader, _retryManager);
        }

        public virtual async Task<ContentType> DetectContentTypeAsync(Uri url, ContentKind contentKind, CancellationToken cancellationToken, IWebReader parent = null)
        {
            var contentType = _contentTypeDetector.GetContentType(url).SingleOrDefaultSafe();

            if (null != contentType)
            {
                Debug.WriteLine("HttpClientWebReaderManager.DetectContentTypeAsync() url ext \"{0}\" type {1}", url, contentType);
                return contentType;
            }

            var referrer = GetReferrer(parent);

            using (var httpClient = _httpClients.CreateClient(url, referrer))
            {
                try
                {
                    using (var request = new HttpRequestMessage(HttpMethod.Head, url))
                    using (var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken, referrer, null, null).ConfigureAwait(false))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            contentType = _contentTypeDetector.GetContentType(request.RequestUri, response.Content.Headers, response.Content.FileName()).SingleOrDefaultSafe();

                            if (null != contentType)
                            {
                                Debug.WriteLine("HttpClientWebReaderManager.DetectContentTypeAsync() url HEAD \"{0}\" type {1}", url, contentType);
                                return contentType;
                            }
                        }
                    }
                }
                catch (HttpRequestException)
                {
                    // Well, HEAD didn't work...
                }

                try
                {
                    using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                    using (var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken, referrer, 0, 0).ConfigureAwait(false))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            contentType = _contentTypeDetector.GetContentType(request.RequestUri, response.Content.Headers, response.Content.FileName()).SingleOrDefaultSafe();

                            if (null != contentType)
                            {
                                Debug.WriteLine("HttpClientWebReaderManager.DetectContentTypeAsync() url range GET \"{0}\" type {1}", url, contentType);
                                return contentType;
                            }
                        }
                    }
                }
                catch (HttpRequestException)
                {
                    // Well, a ranged GET didn't work either.
                }

                try
                {
                    using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                    using (var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken, referrer, null, null).ConfigureAwait(false))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            contentType = _contentTypeDetector.GetContentType(request.RequestUri, response.Content.Headers, response.Content.FileName()).SingleOrDefaultSafe();

                            if (null != contentType)
                            {
                                Debug.WriteLine("HttpClientWebReaderManager.DetectContentTypeAsync() url GET \"{0}\" type {1}", url, contentType);
                                return contentType;
                            }
                        }
                    }
                }
                catch (HttpRequestException)
                {
                    // This just isn't going to work.
                }
            }

            Debug.WriteLine("HttpClientWebReaderManager.DetectContentTypeAsync() url header \"{0}\" unknown type", url);

            return null;
        }

        #endregion

        protected virtual HttpClientWebReader CreateHttpClientWebReader(Uri url, IWebReader parent = null, ContentType contentType = null)
        {
            url = GetUrl(url, parent);

            if (null == contentType)
                contentType = _contentTypeDetector.GetContentType(url).SingleOrDefaultSafe();

            var httpClient = CreateHttpClient(url, parent, contentType);

            return new HttpClientWebReader(this, httpClient, contentType, _contentTypeDetector);
        }

        protected virtual HttpClient CreateHttpClient(Uri url, IWebReader parent, ContentType contentType)
        {
            url = GetUrl(url, parent);

            var referrer = GetReferrer(parent);

            if (null != referrer)
                url = new Uri(referrer, url);

            var httpClient = _httpClients.CreateClient(url, referrer, contentType);

            return httpClient;
        }

        protected static Uri GetUrl(Uri url, IWebReader parent)
        {
            if (null == url && null != parent)
                url = parent.RequestUri ?? parent.BaseAddress;

            return url;
        }

        protected static Uri GetReferrer(IWebReader parent)
        {
            return null == parent ? null : parent.RequestUri ?? parent.BaseAddress;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            var rootClient = _rootWebReader;

            if (null != rootClient)
            {
                _rootWebReader = null;

                rootClient.Dispose();
            }
        }
    }
}

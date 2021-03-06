﻿// -----------------------------------------------------------------------
//  <copyright file="HttpConnectionWebReaderManager.cs" company="Henric Jungheim">
//  Copyright (c) 2012-2016.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012-2016 Henric Jungheim <software@henric.org>
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
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using SM.Media.Content;
using SM.Media.Utility;
using SM.Media.Web.HttpConnection;

namespace SM.Media.Web.HttpConnectionReader
{
    public class HttpConnectionWebReaderManager : IWebReaderManager, IDisposable
    {
        readonly IContentTypeDetector _contentTypeDetector;
        readonly IHttpConnectionFactory _httpConnectionFactory;
        readonly IHttpConnectionRequestFactory _httpConnectionRequestFactory;
        readonly IRetryManager _retryManager;
        readonly IWebReaderManagerParameters _webReaderManagerParameters;
        int _disposed;

        public HttpConnectionWebReaderManager(IHttpConnectionFactory httpConnectionFactory, IHttpConnectionRequestFactory httpConnectionRequestFactory, IWebReaderManagerParameters webReaderManagerParameters, IContentTypeDetector contentTypeDetector, IRetryManager retryManager)
        {
            if (null == httpConnectionFactory)
                throw new ArgumentNullException(nameof(httpConnectionFactory));
            if (null == httpConnectionRequestFactory)
                throw new ArgumentNullException(nameof(httpConnectionRequestFactory));
            if (null == webReaderManagerParameters)
                throw new ArgumentNullException(nameof(webReaderManagerParameters));
            if (null == contentTypeDetector)
                throw new ArgumentNullException(nameof(contentTypeDetector));
            if (null == retryManager)
                throw new ArgumentNullException(nameof(retryManager));

            _httpConnectionFactory = httpConnectionFactory;
            _httpConnectionRequestFactory = httpConnectionRequestFactory;
            _webReaderManagerParameters = webReaderManagerParameters;
            _contentTypeDetector = contentTypeDetector;
            _retryManager = retryManager;
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

        internal Task<IHttpConnectionResponse> SendAsync(Uri url, IWebReader parent, CancellationToken cancellationToken, string method = null, ContentType contentType = null, bool allowBuffering = true, Uri referrer = null, long? fromBytes = null, long? toBytes = null)
        {
            var request = CreateRequest(url, referrer, parent, contentType, method, allowBuffering, fromBytes, toBytes);

            return GetAsync(request, cancellationToken);
        }

        protected virtual HttpConnectionWebReader CreateHttpConnectionWebReader(Uri url, ContentKind requiredKind, IWebReader parent = null, ContentType contentType = null)
        {
            if (null == contentType && null != url)
                contentType = _contentTypeDetector.GetContentType(url, requiredKind).SingleOrDefaultSafe();

            return new HttpConnectionWebReader(this, url, parent?.BaseAddress, contentType, _contentTypeDetector);
        }

        internal virtual async Task<IHttpConnectionResponse> GetAsync(HttpConnectionRequest request, CancellationToken cancellationToken)
        {
            var connection = _httpConnectionFactory.CreateHttpConnection();

            var requestUrl = request.Url;
            var url = requestUrl;

            var retry = 0;

            for (;;)
            {
                await connection.ConnectAsync(request.Proxy ?? url, cancellationToken).ConfigureAwait(false);

                request.Url = url; // TODO: Unhack this...
                var response = await connection.GetAsync(request, true, cancellationToken).ConfigureAwait(false);
                request.Url = requestUrl;

                var status = response.Status;
                if (HttpStatusCode.Moved != status.StatusCode && HttpStatusCode.Redirect != status.StatusCode)
                    return response;

                if (++retry >= 8)
                    return response;

                connection.Close();

                var location = response.Headers["Location"].FirstOrDefault();

                if (!Uri.TryCreate(request.Url, location, out url))
                    return response;
            }
        }

        internal virtual HttpConnectionRequest CreateRequest(Uri url, Uri referrer, IWebReader parent, ContentType contentType, string method = null, bool allowBuffering = false, long? fromBytes = null, long? toBytes = null)
        {
            referrer = referrer ?? GetReferrer(url, parent);

            if (null == url && null != parent)
                url = parent.RequestUri ?? parent.BaseAddress;

            if (null != referrer && (null == url || !url.IsAbsoluteUri))
                url = new Uri(referrer, url);

            var request = _httpConnectionRequestFactory.CreateRequest(url, referrer, contentType, fromBytes, toBytes, _webReaderManagerParameters.DefaultHeaders);

            return request;
        }

        protected static Uri GetReferrer(Uri url, IWebReader parent)
        {
            if (null == parent || null == url)
                return null;

            var parentUrl = parent.RequestUri ?? parent.BaseAddress;

            if (parentUrl == url)
                return null;

            return parentUrl;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;
        }

        #region IWebReaderManager Members

        public virtual IWebReader CreateReader(Uri url, ContentKind requiredKind, IWebReader parent = null, ContentType contentType = null)
        {
            return CreateHttpConnectionWebReader(url, requiredKind, parent, contentType);
        }

        public virtual IWebCache CreateWebCache(Uri url, ContentKind requiredKind, IWebReader parent = null, ContentType contentType = null)
        {
            var webReader = CreateHttpConnectionWebReader(url, requiredKind, parent, contentType);

            return new HttpConnectionWebCache(webReader, _retryManager);
        }

        public virtual async Task<ContentType> DetectContentTypeAsync(Uri url, ContentKind requiredKind, CancellationToken cancellationToken, IWebReader parent = null)
        {
            var contentType = _contentTypeDetector.GetContentType(url, requiredKind).SingleOrDefaultSafe();

            if (null != contentType)
            {
                Debug.WriteLine("HttpConnectionWebReaderManager.DetectContentTypeAsync() url ext \"{0}\" type {1}", url, contentType);
                return contentType;
            }

            try
            {
                using (var response = await SendAsync(url, parent, cancellationToken, "HEAD", allowBuffering: false).ConfigureAwait(false))
                {
                    if (response.IsSuccessStatusCode)
                        contentType = _contentTypeDetector.GetContentType(response.ResponseUri, requiredKind, response.Headers["Content-Type"].FirstOrDefault()).SingleOrDefaultSafe();

                    if (null != contentType)
                    {
                        Debug.WriteLine("HttpConnectionWebReaderManager.DetectContentTypeAsync() url HEAD \"{0}\" type {1}", url, contentType);
                        return contentType;
                    }
                }
            }
            catch (StatusCodeWebException)
            {
                // Well, HEAD didn't work...
            }

            try
            {
                using (var response = await SendAsync(url, parent, cancellationToken, allowBuffering: false, fromBytes: 0, toBytes: 0).ConfigureAwait(false))
                {
                    if (response.IsSuccessStatusCode)
                        contentType = _contentTypeDetector.GetContentType(response.ResponseUri, requiredKind, response.Headers["Content-Type"].FirstOrDefault()).SingleOrDefaultSafe();

                    if (null != contentType)
                    {
                        Debug.WriteLine("HttpConnectionWebReaderManager.DetectContentTypeAsync() url range GET \"{0}\" type {1}", url, contentType);
                        return contentType;
                    }
                }
            }
            catch (StatusCodeWebException)
            {
                // Well, a ranged GET didn't work either.
            }

            try
            {
                using (var response = await SendAsync(url, parent, cancellationToken, allowBuffering: false).ConfigureAwait(false))
                {
                    if (response.IsSuccessStatusCode)
                        contentType = _contentTypeDetector.GetContentType(response.ResponseUri, requiredKind, response.Headers["Content-Type"].FirstOrDefault()).SingleOrDefaultSafe();

                    if (null != contentType)
                    {
                        Debug.WriteLine("HttpConnectionWebReaderManager.DetectContentTypeAsync() url GET \"{0}\" type {1}", url, contentType);
                        return contentType;
                    }
                }
            }
            catch (StatusCodeWebException)
            {
                // This just isn't going to work.
            }

            Debug.WriteLine("HttpConnectionWebReaderManager.DetectContentTypeAsync() url header \"{0}\" unknown type", url);

            return null;
        }

        #endregion
    }
}

﻿// -----------------------------------------------------------------------
//  <copyright file="HttpConnectionWebReaderManager.cs" company="Henric Jungheim">
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
        readonly Func<IHttpConnection> _httpConnectionFactory;
        readonly IRetryManager _retryManager;
        int _disposed;
        IWebReader _rootWebReader;

        public HttpConnectionWebReaderManager(Func<IHttpConnection> httpConnectionFactory, IContentTypeDetector contentTypeDetector, IRetryManager retryManager)
        {
            if (null == httpConnectionFactory)
                throw new ArgumentNullException("httpConnectionFactory");
            if (null == contentTypeDetector)
                throw new ArgumentNullException("contentTypeDetector");
            if (null == retryManager)
                throw new ArgumentNullException("retryManager");

            _httpConnectionFactory = httpConnectionFactory;
            _contentTypeDetector = contentTypeDetector;
            _retryManager = retryManager;
            _rootWebReader = new HttpConnectionWebReader(this, null, null, null, _contentTypeDetector);
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

        public virtual IWebReader CreateReader(Uri url, ContentKind contentKind, IWebReader parent, ContentType contentType)
        {
            return CreateHttpConnectionWebReader(url, parent, contentType);
        }

        public virtual IWebCache CreateWebCache(Uri url, ContentKind contentKind, IWebReader parent = null, ContentType contentType = null)
        {
            var webReader = CreateHttpConnectionWebReader(url, parent, contentType);

            return new HttpConnectionWebCache(webReader, _retryManager);
        }

        public virtual async Task<ContentType> DetectContentTypeAsync(Uri url, ContentKind contentKind, CancellationToken cancellationToken, IWebReader parent = null)
        {
            var contentType = _contentTypeDetector.GetContentType(url).SingleOrDefaultSafe();

            if (null != contentType)
            {
                Debug.WriteLine("HttpWebRequestWebReaderManager.DetectContentTypeAsync() url ext \"{0}\" type {1}", url, contentType);
                return contentType;
            }

            try
            {
                using (var response = await SendAsync(url, parent, cancellationToken, "HEAD", allowBuffering: false).ConfigureAwait(false))
                {
                    contentType = _contentTypeDetector.GetContentType(response.ResponseUri, response.Headers["Content-Type"].FirstOrDefault()).SingleOrDefaultSafe();

                    if (null != contentType)
                    {
                        Debug.WriteLine("HttpWebRequestWebReaderManager.DetectContentTypeAsync() url HEAD \"{0}\" type {1}", url, contentType);
                        return contentType;
                    }
                }
            }
            catch (WebException)
            {
                // Well, HEAD didn't work...
            }

            try
            {
                using (var response = await SendAsync(url, parent, cancellationToken, allowBuffering: false, fromBytes: 0, toBytes: 0).ConfigureAwait(false))
                {
                    contentType = _contentTypeDetector.GetContentType(response.ResponseUri, response.Headers["Content-Type"].FirstOrDefault()).SingleOrDefaultSafe();

                    if (null != contentType)
                    {
                        Debug.WriteLine("HttpWebRequestWebReaderManager.DetectContentTypeAsync() url range GET \"{0}\" type {1}", url, contentType);
                        return contentType;
                    }
                }
            }
            catch (WebException)
            {
                // Well, a ranged GET didn't work either.
            }

            try
            {
                using (var response = await SendAsync(url, parent, cancellationToken, allowBuffering: false).ConfigureAwait(false))
                {
                    contentType = _contentTypeDetector.GetContentType(response.ResponseUri, response.Headers["Content-Type"].FirstOrDefault()).SingleOrDefaultSafe();

                    if (null != contentType)
                    {
                        Debug.WriteLine("HttpWebRequestWebReaderManager.DetectContentTypeAsync() url GET \"{0}\" type {1}", url, contentType);
                        return contentType;
                    }
                }
            }
            catch (WebException)
            {
                // This just isn't going to work.
            }

            Debug.WriteLine("HttpWebRequestWebReaderManager.DetectContentTypeAsync() url header \"{0}\" unknown type", url);

            return null;
        }

        #endregion

        internal Task<IHttpConnectionResponse> SendAsync(Uri url, IWebReader parent, CancellationToken cancellationToken, string method = null, ContentType contentType = null, bool allowBuffering = true, Uri referrer = null, long? fromBytes = null, long? toBytes = null)
        {
            var request = CreateRequest(url, referrer, parent, contentType, method, allowBuffering, fromBytes, toBytes);

            return GetAsync(request, cancellationToken);
        }

        protected virtual HttpConnectionWebReader CreateHttpConnectionWebReader(Uri url, IWebReader parent = null, ContentType contentType = null)
        {
            if (null == contentType)
                contentType = _contentTypeDetector.GetContentType(url).SingleOrDefaultSafe();

            return new HttpConnectionWebReader(this, url, null == parent ? null : parent.BaseAddress, contentType, _contentTypeDetector);
        }

        internal async virtual Task<IHttpConnectionResponse> GetAsync(HttpConnectionRequest request, CancellationToken cancellationToken)
        {
            var connection = _httpConnectionFactory();

            await connection.ConnectAsync(request.Url, cancellationToken).ConfigureAwait(false);

            return await connection.GetAsync(request, true, cancellationToken).ConfigureAwait(false);
        }

        internal virtual HttpConnectionRequest CreateRequest(Uri url, Uri referrer, IWebReader parent, ContentType contentType, string method = null, bool allowBuffering = false, long? fromBytes = null, long? toBytes = null)
        {
            referrer = referrer ?? GetReferrer(parent);

            if (null == url && null != parent)
                url = parent.RequestUri ?? parent.BaseAddress;

            if (null != referrer && (null == url || !url.IsAbsoluteUri))
                url = new Uri(referrer, url);

            var request = new HttpConnectionRequest
            {
                Url = url,
                Referrer = referrer,
                RangeFrom = fromBytes,
                RangeTo = toBytes
            };

            return request;
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
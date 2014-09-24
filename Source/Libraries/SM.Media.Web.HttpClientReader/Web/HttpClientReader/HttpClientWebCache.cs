// -----------------------------------------------------------------------
//  <copyright file="HttpClientWebCache.cs" company="Henric Jungheim">
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
using System.Threading.Tasks;
using SM.Media.Utility;

namespace SM.Media.Web.HttpClientReader
{
    public class HttpClientWebCache : IWebCache
    {
        static readonly CacheControlHeaderValue NoCacheHeader = new CacheControlHeaderValue
                                                                {
                                                                    NoCache = true
                                                                };

        readonly IRetryManager _retryManager;
        readonly HttpClientWebReader _webReader;
        CacheControlHeaderValue _cacheControl;
        object _cachedObject;
        EntityTagHeaderValue _etag;
        bool _firstRequestCompleted;
        DateTimeOffset? _lastModified;
        string _noCache;

        public HttpClientWebCache(HttpClientWebReader webReader, IRetryManager retryManager)
        {
            if (webReader == null)
                throw new ArgumentNullException("webReader");
            if (null == retryManager)
                throw new ArgumentNullException("retryManager");

            _webReader = webReader;
            _retryManager = retryManager;
        }

        #region IWebCache Members

        public IWebReader WebReader
        {
            get { return _webReader; }
        }

        public async Task<TCached> ReadAsync<TCached>(Func<Uri, byte[], TCached> factory, CancellationToken cancellationToken, WebResponse webResponse = null)
            where TCached : class
        {
            if (null == _cachedObject as TCached)
                _cachedObject = null;

            var retry = _retryManager.CreateWebRetry(2, 250);

            await retry
                .CallAsync(() => Fetch(retry, factory, webResponse, cancellationToken), cancellationToken)
                .ConfigureAwait(false);

            return _cachedObject as TCached;
        }

        #endregion

        async Task Fetch<TCached>(IRetry retry, Func<Uri, byte[], TCached> factory, WebResponse webResponse, CancellationToken cancellationToken)
            where TCached : class
        {
            for (; ; )
            {
                using (var request = CreateRequest())
                using (var response = await _webReader.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken, webResponse)
                                                      .ConfigureAwait(false))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        _firstRequestCompleted = true;
                        _cachedObject = factory(response.RequestMessage.RequestUri, await FetchObject(response).ConfigureAwait(false));
                        return;
                    }

                    if (HttpStatusCode.NotModified == response.StatusCode)
                        return;

                    if (!RetryPolicy.IsRetryable(response.StatusCode))
                        goto fail;

                    if (await retry.CanRetryAfterDelayAsync(cancellationToken).ConfigureAwait(false))
                        continue;

                fail:
                    _cachedObject = null;
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        async Task<byte[]> FetchObject(HttpResponseMessage response)
        {
            _lastModified = response.Content.Headers.LastModified;

            _etag = response.Headers.ETag;

            _cacheControl = response.Headers.CacheControl;

            return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        }

        HttpRequestMessage CreateRequest()
        {
            var url = WebReader.BaseAddress;

            var haveConditional = false;

            if (null != _cachedObject)
            {
                if (null != _lastModified)
                    haveConditional = true;

                if (null != _etag)
                    haveConditional = true;
            }

            // Do not rotate the nocache query string if the server has an explicit cache policy.
            if (_firstRequestCompleted && (!haveConditional && null == _cacheControl))
                _noCache = "nocache=" + Guid.NewGuid().ToString("N");

            if (null != _noCache)
            {
                var ub = new UriBuilder(url);

                if (string.IsNullOrEmpty(ub.Query))
                    ub.Query = _noCache;
                else
                    ub.Query = ub.Query.Substring(1) + "&" + _noCache;

                url = ub.Uri;
            }

            var hr = new HttpRequestMessage(HttpMethod.Get, url);

            if (null != _cachedObject && haveConditional)
            {
                if (null != _lastModified)
                    hr.Headers.IfModifiedSince = _lastModified;

                if (null != _etag)
                    hr.Headers.IfNoneMatch.Add(_etag);
            }

            if (!haveConditional)
                hr.Headers.CacheControl = NoCacheHeader;

            return hr;
        }
    }
}

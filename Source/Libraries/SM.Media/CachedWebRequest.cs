// -----------------------------------------------------------------------
//  <copyright file="CachedWebRequest.cs" company="Henric Jungheim">
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
using System.Threading.Tasks;
using SM.Media.Utility;

namespace SM.Media
{
    public class CachedWebRequest : ICachedWebRequest
    {
        static readonly CacheControlHeaderValue NoCacheHeader = new CacheControlHeaderValue
                                                                {
                                                                    NoCache = true
                                                                };

        readonly HttpClient _httpClient;
        readonly Uri _url;
        readonly Func<Uri, HttpWebRequest> _webRequestFactory;
        CacheControlHeaderValue _cacheControl;
        object _cachedObject;
        EntityTagHeaderValue _etag;
        DateTimeOffset? _lastModified;
        string _noCache;

        public CachedWebRequest(Uri url, HttpClient httpClient)
        {
            if (null == url)
                throw new ArgumentNullException("url");

            if (httpClient == null)
                throw new ArgumentNullException("httpClient");

            _url = url;
            _httpClient = httpClient;
        }

        #region ICachedWebRequest Members

        public Uri Url
        {
            get { return _url; }
        }

        public async Task<TCached> ReadAsync<TCached>(Func<byte[], TCached> factory, CancellationToken cancellationToken)
            where TCached : class
        {
            if (null == _cachedObject as TCached)
                _cachedObject = null;

            var retry = new Retry(2, 250, RetryPolicy.IsWebExceptionRetryable);

            await retry
                .CallAsync(() => Fetch(retry, factory, cancellationToken), cancellationToken)
                .ConfigureAwait(false);

            return _cachedObject as TCached;
        }

        #endregion

        async Task Fetch<TCached>(Retry retry, Func<byte[], TCached> factory, CancellationToken cancellationToken)
            where TCached : class
        {
            for (; ; )
            {
                using (var request = CreateRequest())
                using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
                                                       .ConfigureAwait(false))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        _cachedObject = factory(await FetchObject(response).ConfigureAwait(false));
                        return;
                    }

                    if (HttpStatusCode.NotModified == response.StatusCode)
                        return;

                    if (!RetryPolicy.IsRetryable(response.StatusCode))
                        goto fail;

                    if (await retry.CanRetryAfterDelay(cancellationToken).ConfigureAwait(false))
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
            var url = _url;

            var haveConditional = false;

            if (null != _cachedObject)
            {
                if (null != _lastModified)
                    haveConditional = true;

                if (null != _etag)
                    haveConditional = true;
            }

            // Do not rotate the nocache query string if the server has an explicit cache policy.
            if ((!haveConditional && null == _cacheControl) || null == _noCache)
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

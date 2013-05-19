// -----------------------------------------------------------------------
//  <copyright file="CachedWebRequest.cs" company="Henric Jungheim">
//  Copyright (c) 2012.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012 Henric Jungheim <software@henric.org>
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
using System.IO;
using System.Net;
using System.Threading.Tasks;
using SM.Media.Utility;

namespace SM.Media
{
    public class CachedWebRequest : ICachedWebRequest
    {
        readonly Uri _url;
        readonly Func<Uri, HttpWebRequest> _webRequestFactory;
        object _cachedObject;
        string _etag;
        string _lastModified;
        string _noCache;
        string _cacheControl;

        public CachedWebRequest(Uri url, Func<Uri, HttpWebRequest> webRequestFactory)
        {
            if (null == url)
                throw new ArgumentNullException("url");

            if (null == webRequestFactory)
                throw new ArgumentNullException("webRequestFactory");

            _url = url;
            _webRequestFactory = webRequestFactory;
        }

        #region ICachedWebRequest Members

        public Uri Url
        {
            get { return _url; }
        }

        public async Task<TCached> ReadAsync<TCached>(Func<byte[], TCached> factory)
            where TCached : class
        {
            if (null == _cachedObject as TCached)
                _cachedObject = null;

            await new Retry(4, 250, RetryPolicy.IsWebExceptionRetryable)
                .CallAsync(() => Fetch(factory))
                .ConfigureAwait(false);

            return _cachedObject as TCached;
        }

        #endregion

        async Task Fetch<TCached>(Func<byte[], TCached> factory)
            where TCached : class
        {
            var request = CreateRequest();

            using (var response = await GetHttpWebResponseAsync(request).ConfigureAwait(false))
            {
                switch (response.StatusCode)
                {
                    case HttpStatusCode.OK:
                        _cachedObject = factory(await FetchObject(response).ConfigureAwait(false));
                        break;
                    case HttpStatusCode.NotModified:
                        break;
                    default:
                        _cachedObject = null;
                        break;
                }
            }
        }

        async Task<byte[]> FetchObject(HttpWebResponse response)
        {
            var date = response.Headers["Last-Modified"];

            _lastModified = !string.IsNullOrWhiteSpace(date) ? date : null;

            var etag = response.Headers["ETag"];

            _etag = !string.IsNullOrWhiteSpace(etag) ? etag : null;

            var cacheControl = response.Headers["Cache-Control"];

            _cacheControl = !string.IsNullOrWhiteSpace(cacheControl) ? cacheControl : null;

            byte[] body;

            using (var stream = response.GetResponseStream())
            {
                var ms = response.ContentLength > 0 ? new MemoryStream((int)response.ContentLength) : new MemoryStream();

                using (ms)
                {
                    await stream.CopyToAsync(ms).ConfigureAwait(false);

                    body = ms.ToArray();
                }
            }

            return body;
        }

        async Task<HttpWebResponse> GetHttpWebResponseAsync(HttpWebRequest request)
        {
            try
            {
                return (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);
            }
            catch (WebException ex)
            {
                var response = ex.Response as HttpWebResponse;

                if (null != response && HttpStatusCode.NotModified == response.StatusCode)
                    return response;

                throw;
            }
        }

        HttpWebRequest CreateRequest()
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

            var hr = _webRequestFactory(url);

            if (null != _cachedObject && haveConditional)
            {
                if (null != _lastModified)
                {
                    hr.Headers[HttpRequestHeader.IfModifiedSince] = _lastModified;
                }

                if (null != _etag)
                    hr.Headers[HttpRequestHeader.IfNoneMatch] = _etag;
            }

            if (!haveConditional)
                hr.Headers[HttpRequestHeader.CacheControl] = "no-cache";

            return hr;
        }
    }
}

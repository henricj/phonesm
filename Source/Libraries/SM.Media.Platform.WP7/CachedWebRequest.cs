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
        static readonly DateTimeOffset VeryOldDate = new DateTime(1970, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        readonly Uri _url;
        readonly Func<Uri, HttpWebRequest> _webRequestFactory;
        object _cachedObject;
        string _etag;
        string _lastModified;
        string _noCache;

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
                .CallAsync(() => Fetch(factory));

            return _cachedObject as TCached;
        }

        #endregion

        async Task Fetch<TCached>(Func<byte[], TCached> factory)
            where TCached : class
        {
            var request = CreateRequest();

            using (var response = await GetHttpWebResponseAsync(request))
            {
                switch (response.StatusCode)
                {
                    case HttpStatusCode.OK:
                    {
                        var date = response.Headers["Last-Modified"];

                        _lastModified = !string.IsNullOrWhiteSpace(date) ? date : null;

                        var etag = response.Headers["ETag"];

                        _etag = !string.IsNullOrWhiteSpace(etag) ? etag : null;

                        byte[] body;

                        using (var stream = response.GetResponseStream())
                        {
                            var ms = response.ContentLength > 0 ? new MemoryStream((int)response.ContentLength) : new MemoryStream();

                            using (ms)
                            {
                                await stream.CopyToAsync(ms);

                                body = ms.ToArray();
                            }
                        }

                        _cachedObject = factory(body);
                    }
                        break;
                    case HttpStatusCode.NotModified:
                        break;
                    default:
                        _cachedObject = null;
                        break;
                }
            }
        }

        async Task<HttpWebResponse> GetHttpWebResponseAsync(HttpWebRequest request)
        {
            try
            {
                return (HttpWebResponse)await request.GetResponseAsync();
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

            if (!haveConditional || null == _noCache)
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
#if WINDOWS_PHONE
                    hr.Headers[HttpRequestHeader.IfModifiedSince] = _lastModified;
#else
                    hr.IfModifiedSince = DateTime.Parse(_lastModified);
#endif
                }

                if (null != _etag)
                    hr.Headers[HttpRequestHeader.IfNoneMatch] = _etag;
            }

            if (!haveConditional)
            {
                hr.Headers[HttpRequestHeader.CacheControl] = "no-cache";

#if false
    // The If-Modified-Since seems to defeat the phone's local cache.  Unfortunately,
    // some sites seem to always return "Not Modified" if they see an
    // If-Modified-since.  Make sure we get at least one 200 response code before
    // we try slay the WP cache.
                if (null != _cachedObject)
                {
                    //var date = DateTimeOffset.UtcNow;
                    var date = VeryOldDate;
#if WINDOWS_PHONE
                    hr.Headers[HttpRequestHeader.IfModifiedSince] = date.ToString("r");
#else
                    hr.IfModifiedSince = date.UtcDateTime;
#endif
                }
#endif
            }

            return hr;
        }
    }
}

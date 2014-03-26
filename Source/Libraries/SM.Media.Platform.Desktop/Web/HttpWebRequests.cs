// -----------------------------------------------------------------------
//  <copyright file="HttpWebRequests.cs" company="Henric Jungheim">
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
using SM.Media.Web.WebRequestReader;

namespace SM.Media.Web
{
    public class HttpWebRequests : HttpWebRequestsBase
    {
        public HttpWebRequests(ICredentials credentials = null, CookieContainer cookieContainer = null)
            : base(credentials, cookieContainer)
        { }

        public override bool SetReferrer(HttpWebRequest request, Uri referrer)
        {
            request.Referer = null == referrer ? null : referrer.ToString();

            return true;
        }

        public override bool SetIfModifiedSince(HttpWebRequest request, string ifModifiedSince)
        {
            if (null == ifModifiedSince)
            {
                request.IfModifiedSince = DateTime.MinValue;

                return true;
            }

            DateTime dateTime;

            if (!DateTime.TryParse(ifModifiedSince, out dateTime))
            {
                request.IfModifiedSince = DateTime.MinValue;

                return false;
            }

            request.IfModifiedSince = dateTime;

            return true;
        }

        public override bool SetIfNoneMatch(HttpWebRequest request, string etag)
        {
            if (string.IsNullOrEmpty(etag))
                request.Headers.Remove(HttpRequestHeader.IfNoneMatch);
            else
                request.Headers[HttpRequestHeader.IfNoneMatch] = etag;

            return true;
        }

        public override bool SetCacheControl(HttpWebRequest request, string cacheControl)
        {
            if (string.IsNullOrEmpty(cacheControl))
                request.Headers.Remove(HttpRequestHeader.CacheControl);
            else
                request.Headers[HttpRequestHeader.CacheControl] = cacheControl;

            return true;
        }

        protected override void SetRange(HttpWebRequest request, long? fromBytes, long? toBytes)
        {
            if (fromBytes.HasValue || toBytes.HasValue)
            {
                if (fromBytes.HasValue && toBytes.HasValue)
                    request.AddRange(fromBytes.Value, toBytes.Value);
                else if (fromBytes.HasValue)
                    request.AddRange(fromBytes.Value);
                else
                    request.AddRange(-toBytes.Value);
            }
        }
    }
}

// -----------------------------------------------------------------------
//  <copyright file="PclHttpWebRequests.cs" company="Henric Jungheim">
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
using System.Net;

namespace SM.Media.Web.WebRequestReader
{
    public class PclHttpWebRequests : HttpWebRequestsBase
    {
        public PclHttpWebRequests(ICredentials credentials = null, CookieContainer cookieContainer = null)
            : base(credentials, cookieContainer)
        { }

        public override bool SetReferrer(HttpWebRequest request, Uri referrer)
        {
            try
            {
                if (null != referrer)
                    request.Headers[HttpRequestHeader.Referer] = referrer.ToString();

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("HttpWebRequestsBase.SetReferrer() unable to set referrer: " + ex.Message);

                return false;
            }
        }

        public override bool SetIfModifiedSince(HttpWebRequest request, string ifModifiedSince)
        {
            try
            {
                if (null != ifModifiedSince)
                    request.Headers[HttpRequestHeader.IfModifiedSince] = ifModifiedSince;

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public override bool SetIfNoneMatch(HttpWebRequest request, string etag)
        {
            try
            {
                if (null != etag)
                    request.Headers[HttpRequestHeader.IfNoneMatch] = etag;

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public override bool SetCacheControl(HttpWebRequest request, string cacheControl)
        {
            try
            {
                if (null != cacheControl)
                    request.Headers[HttpRequestHeader.CacheControl] = cacheControl;

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        protected override void SetRange(HttpWebRequest request, long? fromBytes, long? toBytes)
        {
            if (fromBytes.HasValue || toBytes.HasValue)
            {
                request.Headers[HttpRequestHeader.Range] = String.Format("bytes={0}-{1}",
                    fromBytes.HasValue ? fromBytes.ToString() : String.Empty,
                    toBytes.HasValue ? toBytes.ToString() : String.Empty);
            }
        }
    }
}

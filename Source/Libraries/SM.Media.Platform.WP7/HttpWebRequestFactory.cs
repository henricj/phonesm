// -----------------------------------------------------------------------
//  <copyright file="HttpWebRequestFactory.cs" company="Henric Jungheim">
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
using System.Net;

namespace SM.Media
{
    public interface IHttpWebRequestFactory
    {
        HttpWebRequest Create(Uri url);
    }

    public class HttpWebRequestFactory : IHttpWebRequestFactory
    {
        readonly CookieContainer _cookieContainer;
        readonly ICredentials _credentials;
        readonly Uri _referrer;
        readonly string _userAgent;

        public HttpWebRequestFactory(Uri referrer = null, string userAgent = null, ICredentials credentials = null, CookieContainer cookieContainer = null)
        {
            _cookieContainer = cookieContainer;
            _userAgent = userAgent;
            _credentials = credentials;
            _referrer = referrer;
        }

        #region IHttpWebRequestFactory Members

        public HttpWebRequest Create(Uri url)
        {
            var request = WebRequest.CreateHttp(url);

            if (null != _userAgent)
                request.UserAgent = _userAgent;

            if (null != _credentials)
                request.Credentials = _credentials;

            if (null != _cookieContainer)
                request.CookieContainer = _cookieContainer;

            if (null != _referrer)
            {
#if WINDOWS_PHONE
                request.Headers[HttpRequestHeader.Referer] = _referrer.ToString();
#else
                request.Referer  = _referrer.ToString();
#endif
            }

            return request;
        }

        #endregion
    }
}

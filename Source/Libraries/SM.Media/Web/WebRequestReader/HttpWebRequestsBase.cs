// -----------------------------------------------------------------------
//  <copyright file="HttpWebRequestsBase.cs" company="Henric Jungheim">
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
using SM.Media.Content;

namespace SM.Media.Web.WebRequestReader
{
    public abstract class HttpWebRequestsBase : IHttpWebRequests
    {
        // TODO: We need to encode all these headers properly.

        static bool _canSetAllowReadStreamBuffering = true;
        readonly CookieContainer _cookieContainer;
        readonly ICredentials _credentials;

        protected HttpWebRequestsBase(ICredentials credentials, CookieContainer cookieContainer)
        {
            _credentials = credentials;
            _cookieContainer = cookieContainer;
        }

        #region IHttpWebRequests Members

        public virtual HttpWebRequest CreateWebRequest(Uri url, Uri referrer = null, string method = null, ContentType contentType = null, bool allowBuffering = true, long? fromBytes = null, long? toBytes = null)
        {
            var request = WebRequest.CreateHttp(url);

            if (null != method)
                request.Method = method;

            SetDefaultCookies(request);

            SetDefaultCredentials(request);

            SetReferrer(request, referrer);

            SetContentType(request, contentType);

            SetBuffering(request, allowBuffering);

            SetRange(request, fromBytes, toBytes);

            return request;
        }

        public abstract bool SetIfModifiedSince(HttpWebRequest request, string ifModifiedSince);
        public abstract bool SetIfNoneMatch(HttpWebRequest request, string etag);
        public abstract bool SetCacheControl(HttpWebRequest request, string cacheControl);

        #endregion

        protected abstract void SetRange(HttpWebRequest request, long? fromBytes, long? toBytes);

        protected virtual void SetBuffering(HttpWebRequest request, bool allowBuffering)
        {
#if !SM_MEDIA_LEGACY
            if (_canSetAllowReadStreamBuffering && request.AllowReadStreamBuffering != allowBuffering)
            {
                try
                {
                    request.AllowReadStreamBuffering = allowBuffering;
                }
                catch (InvalidOperationException ex)
                {
                    Debug.WriteLine("HttpWebRequestsBase.SetBuffering() unable to set AllowReadStreamBuffering to {0}: {1}", allowBuffering, ex.Message);
                    _canSetAllowReadStreamBuffering = false;
                }
            }
#endif // !SM_MEDIA_LEGACY
        }

        protected virtual void SetContentType(HttpWebRequest request, ContentType contentType)
        {
            if (null != contentType)
            {
                if (null != contentType.AlternateMimeTypes && contentType.AlternateMimeTypes.Count > 0)
                    request.Accept = string.Join(", ", new[] { contentType.MimeType }.Concat(contentType.AlternateMimeTypes));
                else
                    request.Accept = contentType.MimeType;
            }
        }

        public abstract bool SetReferrer(HttpWebRequest request, Uri referrer);

        protected virtual bool SetCredentials(HttpWebRequest request, ICredentials credentials)
        {
            request.Credentials = credentials;

            return true;
        }

        protected virtual bool SetDefaultCredentials(HttpWebRequest request)
        {
            return SetCredentials(request, _credentials);
        }

        protected virtual bool SetCookies(HttpWebRequest request, CookieContainer cookieContainer)
        {
            if (null != cookieContainer && request.SupportsCookieContainer)
            {
                request.CookieContainer = cookieContainer;
                return true;
            }

            return false;
        }

        protected virtual bool SetDefaultCookies(HttpWebRequest request)
        {
            return SetCookies(request, _cookieContainer);
        }
    }
}

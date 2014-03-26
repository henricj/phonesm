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
using System.Linq;
using System.Net;
using SM.Media.Content;

namespace SM.Media.Web.WebRequestReader
{
    public class PclHttpWebRequests : IHttpWebRequests
    {
        static bool _canSetAllowReadStreamBuffering = true;
        readonly CookieContainer _cookieContainer;
        readonly ICredentials _credentials;

        public PclHttpWebRequests(ICredentials credentials = null, CookieContainer cookieContainer = null)
        {
            _credentials = credentials;
            _cookieContainer = cookieContainer;
        }

        #region IHttpWebRequests Members

        public HttpWebRequest CreateWebRequest(Uri url, Uri referrer = null, string method = null, ContentType contentType = null, bool allowBuffering = true, long? fromBytes = null, long? toBytes = null)
        {
            var request = WebRequest.CreateHttp(url);

            if (null != _cookieContainer && request.SupportsCookieContainer)
                request.CookieContainer = _cookieContainer;

            if (null != _credentials)
                request.Credentials = _credentials;

            if (null != referrer)
            {
                try
                {
                    request.Headers[HttpRequestHeader.Referer] = referrer.ToString();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("PclHttpWebRequests.CreateWebRequest() unable to set referrer: " + ex.Message);
                }
            }

            if (null != contentType)
            {
                if (null != contentType.AlternateMimeTypes && contentType.AlternateMimeTypes.Count > 0)
                    request.Accept = string.Join(", ", new[] { contentType.MimeType }.Concat(contentType.AlternateMimeTypes));
                else
                    request.Accept = contentType.MimeType;
            }

            if (null != method)
                request.Method = method;

#if !SM_MEDIA_LEGACY
            if (_canSetAllowReadStreamBuffering && request.AllowReadStreamBuffering != allowBuffering)
            {
                try
                {
                    request.AllowReadStreamBuffering = allowBuffering;
                }
                catch (InvalidOperationException ex)
                {
                    Debug.WriteLine("PclHttpWebRequests.SendAsync() unable to set AllowReadStreamBuffering to {0}: {1}", allowBuffering, ex.Message);
                    _canSetAllowReadStreamBuffering = false;
                }
            }
#endif // !SM_MEDIA_LEGACY

            if (fromBytes.HasValue || toBytes.HasValue)
            {
                request.Headers[HttpRequestHeader.Range] = String.Format("{0}-{1}",
                    fromBytes.HasValue ? fromBytes.ToString() : String.Empty,
                    toBytes.HasValue ? toBytes.ToString() : String.Empty);
            }

            return request;
        }

        #endregion
    }
}

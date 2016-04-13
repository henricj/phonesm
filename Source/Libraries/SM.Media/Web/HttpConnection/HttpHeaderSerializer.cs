// -----------------------------------------------------------------------
//  <copyright file="HttpHeaderSerializer.cs" company="Henric Jungheim">
//  Copyright (c) 2012-2016.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012-2016 Henric Jungheim <software@henric.org>
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
using System.Text;

namespace SM.Media.Web.HttpConnection
{
    public interface IHttpHeaderSerializer
    {
        void WriteHeader(Stream stream, string method, HttpConnectionRequest request);
    }

    public class HttpHeaderSerializer : IHttpHeaderSerializer
    {
        const string HttpEol = "\r\n";
        readonly Encoding _headerEncoding;
        readonly string _userAgentLine;

        public HttpHeaderSerializer(IUserAgentEncoder userAgentEncoder, IHttpEncoding httpEncoding)
        {
            if (null == userAgentEncoder)
                throw new ArgumentNullException("userAgentEncoder");
            if (null == httpEncoding)
                throw new ArgumentNullException("httpEncoding");

            var userAgent = userAgentEncoder.UsAsciiUserAgent;

            if (!string.IsNullOrWhiteSpace(userAgent))
                _userAgentLine = "User-Agent: " + userAgent.Trim();

            _headerEncoding = httpEncoding.HeaderEncoding;
        }

        #region IHttpHeaderSerializer Members

        public void WriteHeader(Stream stream, string method, HttpConnectionRequest request)
        {
            var url = request.Url;

#if SM_MEDIA_LEGACY
            using (var tw = new StreamWriter(stream, _headerEncoding, 1024))
#else
            using (var tw = new StreamWriter(stream, _headerEncoding, 1024, true))
#endif
            {
                tw.NewLine = HttpEol;

                var requestTarget = GetRequestTarget(request);
                var host = GetHost(url);

                tw.WriteLine(method.ToUpperInvariant() + " " + requestTarget + " HTTP/1.1");
                tw.WriteLine("Host: " + host);
                tw.WriteLine(request.KeepAlive ? "Connection: Keep-Alive" : "Connection: Close");

                if (null != request.Referrer && request.Referrer.IsAbsoluteUri)
                    tw.WriteLine("Referer:" + request.Referrer.AbsoluteUri);

                if (request.RangeFrom.HasValue || request.RangeTo.HasValue)
                    tw.WriteLine("Range: bytes={0}-{1}", request.RangeFrom, request.RangeTo);

                if (null != _userAgentLine)
                    tw.WriteLine(_userAgentLine);

                if (!string.IsNullOrWhiteSpace(request.Accept))
                    tw.WriteLine("Accept: " + request.Accept.Trim());

                if (null != request.Cookies)
                {
                    var cookieHeader = request.Cookies.GetCookieHeader(url);

                    if (!string.IsNullOrWhiteSpace(cookieHeader))
                        tw.WriteLine("Cookies: " + cookieHeader);
                }

                if (null != request.Headers)
                {
                    foreach (var header in request.Headers)
                    {
                        var value = header.Value;

                        if (string.IsNullOrWhiteSpace(value))
                            value = string.Empty;
                        else
                            value = value.Trim();

                        tw.WriteLine(header.Key.Trim() + ": " + value);
                    }
                }

                tw.WriteLine();

                tw.Flush();
            }
        }

        static string GetHost(Uri url)
        {
#if SM_MEDIA_LEGACY
            return url.Host + ':' + url.Port;
#else
            return url.IsDefaultPort ? url.DnsSafeHost : url.DnsSafeHost + ':' + url.Port;
#endif
        }

        static string GetRequestTarget(HttpConnectionRequest request)
        {
            var url = request.Url;
            var proxy = request.Proxy;

            if (null != proxy)
            {
                var ub = new UriBuilder(url) { Fragment = null };

                return ub.Uri.AbsoluteUri;
            }

#if SM_MEDIA_LEGACY
            var target = url.AbsolutePath;

            if (!string.IsNullOrWhiteSpace(url.Query))
                target += "?" + url.Query;
#else
            var target = url.PathAndQuery;
#endif

            return target;
        }

        #endregion
    }
}

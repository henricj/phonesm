// -----------------------------------------------------------------------
//  <copyright file="HttpStatus.cs" company="Henric Jungheim">
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

namespace SM.Media.Web.HttpConnection
{
    public interface IHttpStatus
    {
        bool ChunkedEncoding { get; }
        long? ContentLength { get; }
        HttpStatusCode StatusCode { get; }
        int VersionMajor { get; }
        int VersionMinor { get; }
        string ResponsePhrase { get; }
        string Version { get; }
        bool IsHttp { get; }

        bool IsSuccessStatusCode { get; }
    }

    public sealed class HttpStatus : IHttpStatus
    {
        #region IHttpStatus Members

        public bool ChunkedEncoding { get; set; }
        public long? ContentLength { get; set; }
        public HttpStatusCode StatusCode { get; set; }
        public int VersionMajor { get; set; }
        public int VersionMinor { get; set; }
        public string ResponsePhrase { get; set; }
        public string Version { get; set; }
        public bool IsHttp { get; set; }

        public bool IsSuccessStatusCode
        {
            get
            {
                var statusCode = (int)StatusCode;

                return statusCode >= 200 && statusCode <= 299;
            }
        }

        #endregion
    }

    public static class HttpStatusExtensions
    {
        public static void EnsureSuccessStatusCode(this IHttpStatus httpStatus)
        {
            if (null == httpStatus)
                throw new ArgumentNullException(nameof(httpStatus));

            if (!httpStatus.IsSuccessStatusCode)
                throw new StatusCodeWebException(httpStatus.StatusCode, httpStatus.ResponsePhrase);
        }
    }
}

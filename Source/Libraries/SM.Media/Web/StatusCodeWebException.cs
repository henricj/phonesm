// -----------------------------------------------------------------------
//  <copyright file="StatusCodeWebException.cs" company="Henric Jungheim">
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

namespace SM.Media.Web
{
    public class StatusCodeWebException : WebException
    {
        readonly HttpStatusCode _statusCode;

        public StatusCodeWebException(HttpStatusCode statusCode, string message, Exception innerException = null)
            : base(message, innerException)
        {
            _statusCode = statusCode;
        }

        public HttpStatusCode StatusCode
        {
            get { return _statusCode; }
        }

        public static void ThrowIfNotSuccess(HttpStatusCode statusCode, string message)
        {
            var code = (int)statusCode;

            if (code < 200 || code >= 300)
                throw new StatusCodeWebException(statusCode, message);
        }
    }
}

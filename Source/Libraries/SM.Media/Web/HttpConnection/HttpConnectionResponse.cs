// -----------------------------------------------------------------------
//  <copyright file="HttpConnectionResponse.cs" company="Henric Jungheim">
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
using System.Linq;
using System.Net;

namespace SM.Media.Web.HttpConnection
{
    public interface IHttpConnectionResponse : IDisposable
    {
        ILookup<string, string> Headers { get; }
        Stream ContentReadStream { get; }
        IHttpStatus Status { get; }
        Uri ResponseUri { get; }

        bool IsSuccessStatusCode { get; }
        void EnsureSuccessStatusCode();
    }

    public class HttpConnectionResponse : IHttpConnectionResponse
    {
        IHttpConnection _connection;
        IHttpReader _reader;

        public HttpConnectionResponse(Uri url, IHttpConnection connection, IHttpReader reader, Stream stream, ILookup<string, string> headers, IHttpStatus status)
        {
            if (null == url)
                throw new ArgumentNullException(nameof(url));
            if (null == stream)
                throw new ArgumentNullException(nameof(stream));
            if (null == headers)
                throw new ArgumentNullException(nameof(headers));
            if (null == status)
                throw new ArgumentNullException(nameof(status));

            ResponseUri = url;
            _reader = reader;
            ContentReadStream = stream;
            Headers = headers;
            Status = status;
            _connection = connection;
        }

        #region IHttpConnectionResponse Members

        public void Dispose()
        {
            var stream = ContentReadStream;

            if (null != stream)
            {
                ContentReadStream = null;

                stream.Dispose();
            }

            var reader = _reader;

            if (null != reader)
            {
                _reader = null;

                reader.Dispose();
            }

            var connection = _connection;

            if (null != connection)
            {
                _connection = connection;

                connection.Dispose();
            }
        }

        public ILookup<string, string> Headers { get; }

        public Stream ContentReadStream { get; private set; }

        public IHttpStatus Status { get; }

        public Uri ResponseUri { get; }

        public bool IsSuccessStatusCode => (null != Status) && Status.IsSuccessStatusCode;

        public void EnsureSuccessStatusCode()
        {
            if (null == Status)
                throw new StatusCodeWebException(HttpStatusCode.InternalServerError, "No status available");

            Status.EnsureSuccessStatusCode();
        }

        #endregion
    }
}

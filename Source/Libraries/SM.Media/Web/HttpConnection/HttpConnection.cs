// -----------------------------------------------------------------------
//  <copyright file="HttpConnection.cs" company="Henric Jungheim">
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SM.Media.Utility;

namespace SM.Media.Web.HttpConnection
{
    public interface IHttpConnection : IDisposable
    {
        Task ConnectAsync(Uri url, CancellationToken cancellationToken);
        Task<IHttpConnectionResponse> GetAsync(HttpConnectionRequest request, bool closeConnection, CancellationToken cancellationToken);
        void Close();
    }

    public class HttpConnection : IHttpConnection
    {
        readonly Encoding _headerDecoding;
        readonly List<Tuple<string, string>> _headers = new List<Tuple<string, string>>();
        readonly IHttpHeaderSerializer _httpHeaderSerializer;
        int _disposed;
        HttpStatus _httpStatus;
        ISocket _socket;

        public HttpConnection(IHttpHeaderSerializer httpHeaderSerializer, IHttpEncoding httpEncoding, ISocket socket)
        {
            if (null == httpHeaderSerializer)
                throw new ArgumentNullException("httpHeaderSerializer");
            if (null == socket)
                throw new ArgumentNullException("socket");

            _httpHeaderSerializer = httpHeaderSerializer;
            _headerDecoding = httpEncoding.HeaderDecoding;
            _socket = socket;
        }

        #region IHttpConnection Members

        public void Dispose()
        {
            if (0 != Interlocked.Exchange(ref _disposed, 1))
                return;

            Dispose(true);

            GC.SuppressFinalize(this);
        }

        public virtual Task ConnectAsync(Uri url, CancellationToken cancellationToken)
        {
            return _socket.ConnectAsync(url, cancellationToken);
        }

        public async Task<IHttpConnectionResponse> GetAsync(HttpConnectionRequest request, bool closeConnection, CancellationToken cancellationToken)
        {
            StartRequest();

            // TODO:  Fix closeConnection vs KeepAlive hack.
            if (closeConnection)
                request.KeepAlive = false;

            var requestHeader = SerializeHeader("GET", request);

            var writeHeaderTask = WriteSocketAsync(requestHeader, 0, requestHeader.Length, cancellationToken);

            var httpReader = new HttpReader(ReadSocketAsync, _headerDecoding);

            try
            {
                var statusLine = await httpReader.ReadNonBlankLineAsync(cancellationToken).ConfigureAwait(false);

                ParseStatusLine(statusLine);

                await ReadHeadersAsync(httpReader, cancellationToken).ConfigureAwait(false);

                await writeHeaderTask.ConfigureAwait(false);
                writeHeaderTask = null;

                var stream = _httpStatus.ChunkedEncoding ? (Stream)new ChunkedStream(httpReader) : new ContentLengthStream(httpReader, _httpStatus.ContentLength);

                var response = new HttpConnectionResponse(request.Url, closeConnection ? this : null, httpReader, stream,
                    _headers.ToLookup(kv => kv.Item1, kv => kv.Item2, StringComparer.OrdinalIgnoreCase), _httpStatus);

                httpReader = null;

                return response;
            }
            finally
            {
                if (null != httpReader)
                    httpReader.Dispose();

                if (null != writeHeaderTask)
                    TaskCollector.Default.Add(writeHeaderTask, "HttpConnection GetAsync writer");
            }
        }

        public virtual void Close()
        {
            _socket.Close();
        }

        #endregion

        byte[] SerializeHeader(string method, HttpConnectionRequest request)
        {
            using (var ms = new MemoryStream())
            {
                _httpHeaderSerializer.WriteHeader(ms, method, request);

                return ms.ToArray();
            }
        }

        Task<int> ReadSocketAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
        {
            var socket = _socket;

            if (null == socket)
                throw new ObjectDisposedException(GetType().FullName);

            return socket.ReadAsync(buffer, offset, length, cancellationToken);
        }

        Task<int> WriteSocketAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
        {
            var socket = _socket;

            if (null == socket)
                throw new ObjectDisposedException(GetType().FullName);

            return socket.WriteAsync(buffer, offset, length, cancellationToken);
        }

        void StartRequest()
        {
            _httpStatus = new HttpStatus();
            _headers.Clear();
        }

        async Task ReadHeadersAsync(HttpReader httpReader, CancellationToken cancellationToken)
        {
            for (; ; )
            {
                var nameValue = await httpReader.ReadHeaderAsync(cancellationToken).ConfigureAwait(false);

                if (null == nameValue)
                    break;

                _headers.Add(nameValue);

                var value = nameValue.Item2;

                if (string.IsNullOrEmpty(value))
                    continue;

                var name = nameValue.Item1;

                if (string.Equals(name, "Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    long n;
                    if (long.TryParse(value, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out n))
                        _httpStatus.ContentLength = n;
                }
                else if (string.Equals(name, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
                {
                    var semicolon = value.IndexOf(';');

                    var token = semicolon > 1 ? value.Substring(0, semicolon).Trim() : value;

                    if (string.Equals(token, "chunked", StringComparison.OrdinalIgnoreCase))
                        _httpStatus.ChunkedEncoding = true;
                }
            }
        }

        void ParseStatusLine(string statusLine)
        {
            if (statusLine.StartsWith("HTTP", StringComparison.Ordinal))
            {
                ParseRealHttp(statusLine);

                _httpStatus.IsHttp = true;
                
                return;
            }

            // What else...? "ICY"?
            // We do assume that there are two or three space-separated components.
            // version [SP] code [SP] message
            // where the message is optional.

            var parts = statusLine.Split(' ');

            _httpStatus.Version = parts[0];

            if (parts.Length < 2 || parts.Length > 3)
                throw new WebException("Invalid status line: " + statusLine);

            int statusCode;
            if (!int.TryParse(parts[1], NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out statusCode))
                throw new WebException("Invalid status code: " + statusLine);

            _httpStatus.StatusCode = (HttpStatusCode)statusCode;

            if (parts.Length > 2)
            {
                var reasonPhrase = parts[2].Trim();

                if (reasonPhrase.Length > 0)
                    _httpStatus.ResponsePhrase = reasonPhrase;
            }
        }

        void ParseRealHttp(string statusLine)
        {
            var slash = statusLine.IndexOf('/');

            if (slash < 1 || slash + 1 >= statusLine.Length)
                throw new WebException("Invalid status line: " + statusLine);

            var firstSpace = statusLine.IndexOf(' ', slash + 1);

            if (firstSpace < 1 || firstSpace + 1 >= statusLine.Length)
                throw new WebException("Invalid status line: " + statusLine);

            var secondSpace = statusLine.IndexOf(' ', firstSpace + 1);

            Debug.Assert(slash + 1 < firstSpace, "Unable to parse status line 1");
            Debug.Assert(secondSpace < 0 || secondSpace > firstSpace, "Unable to parse status line 2");

            // Protocol

            var http = statusLine.Substring(0, slash);

            if (!string.Equals(http, "HTTP", StringComparison.Ordinal))
                throw new WebException("Invalid protocol: " + statusLine);

            var version = statusLine.Substring(slash + 1, firstSpace - slash - 1);

            _httpStatus.Version = version;

            var dot = version.IndexOf('.');

            if (dot < 1 || dot + 1 >= version.Length)
                throw new WebException("Invalid protocol: " + statusLine);

            var majorVersion = version.Substring(0, dot);
            var minorVersion = version.Substring(dot + 1, version.Length - dot - 1);

            int n;
            if (!int.TryParse(majorVersion, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out n))
                throw new WebException("Invalid protocol version: " + statusLine);

            _httpStatus.VersionMajor = n;

            if (!int.TryParse(minorVersion, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out n))
                throw new WebException("Invalid protocol version: " + statusLine);

            _httpStatus.VersionMinor = n;

            // Status Code

            var statusCodeString = secondSpace > firstSpace + 1 ? statusLine.Substring(firstSpace + 1, secondSpace - firstSpace - 1) : statusLine.Substring(firstSpace + 1);

            int statusCode;
            if (!int.TryParse(statusCodeString, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out statusCode))
                throw new WebException("Invalid status code: " + statusLine);

            if (statusCode < 100 || statusCode > 999)
                throw new WebException("Invalid status code: " + statusLine);

            _httpStatus.StatusCode = (HttpStatusCode)statusCode;

            // Response Phrase

            var responsePhrase = secondSpace > firstSpace ? statusLine.Substring(secondSpace + 1) : null;

            _httpStatus.ResponsePhrase = responsePhrase;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            var socket = _socket;
            if (null != socket)
            {
                _socket = null;
                socket.Dispose();
            }
        }
    }
}

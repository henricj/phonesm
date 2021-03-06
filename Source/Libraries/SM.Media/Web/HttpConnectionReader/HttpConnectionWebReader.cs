﻿// -----------------------------------------------------------------------
//  <copyright file="HttpConnectionWebReader.cs" company="Henric Jungheim">
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SM.Media.Content;
using SM.Media.Utility;
using SM.Media.Web.HttpConnection;

namespace SM.Media.Web.HttpConnectionReader
{
    public sealed class HttpConnectionWebReader : IWebReader
    {
        readonly IContentTypeDetector _contentTypeDetector;
        readonly Uri _referrer;
        readonly HttpConnectionWebReaderManager _webReaderManager;

        public HttpConnectionWebReader(HttpConnectionWebReaderManager webReaderManager, Uri baseAddress, Uri referrer, ContentType contentType, IContentTypeDetector contentTypeDetector)
        {
            if (null == webReaderManager)
                throw new ArgumentNullException(nameof(webReaderManager));
            if (contentTypeDetector == null)
                throw new ArgumentNullException(nameof(contentTypeDetector));

            _webReaderManager = webReaderManager;
            BaseAddress = baseAddress;
            _referrer = referrer;
            ContentType = contentType;
            _contentTypeDetector = contentTypeDetector;
        }


        #region IWebReader Members

        public Uri BaseAddress { get; }

        public Uri RequestUri { get; private set; }

        public ContentType ContentType { get; private set; }

        public IWebReaderManager Manager => _webReaderManager;

        public void Dispose()
        { }

        public async Task<IWebStreamResponse> GetWebStreamAsync(Uri url, bool waitForContent, CancellationToken cancellationToken,
            Uri referrer = null, long? from = null, long? to = null, WebResponse webResponse = null)
        {
            var request = _webReaderManager.CreateRequest(url, referrer, this, ContentType, allowBuffering: waitForContent, fromBytes: from, toBytes: to);

            var response = await _webReaderManager.GetAsync(request, cancellationToken).ConfigureAwait(false);

            Update(url, response, webResponse);

            return new HttpConnectionWebStreamResponse(response);
        }

        public async Task<byte[]> GetByteArrayAsync(Uri url, CancellationToken cancellationToken, WebResponse webResponse = null)
        {
            if (null != BaseAddress && !url.IsAbsoluteUri)
                url = new Uri(BaseAddress, url);

            using (var response = await _webReaderManager.SendAsync(url, this, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();

                Update(url, response, webResponse);

                using (var ms = new MemoryStream())
                {
                    await response.ContentReadStream.CopyToAsync(ms, 4096, cancellationToken).ConfigureAwait(false);

                    return ms.ToArray();
                }
            }
        }

        #endregion

        public async Task<IHttpConnectionResponse> SendAsync(HttpConnectionRequest request, bool allowBuffering, CancellationToken cancellationToken, WebResponse webResponse = null)
        {
            var url = request.Url;

            var response = await _webReaderManager.GetAsync(request, cancellationToken).ConfigureAwait(false);

            Update(url, response, webResponse);

            return response;
        }

        public HttpConnectionRequest CreateWebRequest(Uri url, Uri referrer = null)
        {
            return _webReaderManager.CreateRequest(url, referrer ?? _referrer, this, ContentType);
        }

        void Update(Uri url, IHttpConnectionResponse response, WebResponse webResponse)
        {
            if (null != webResponse)
            {
                webResponse.RequestUri = response.ResponseUri;
                webResponse.ContentLength = response.Status.ContentLength >= 0 ? response.Status.ContentLength : null;
                webResponse.Headers = GetHeaders(response.Headers);

                webResponse.ContentType = _contentTypeDetector.GetContentType(response.ResponseUri, ContentKind.Unknown, response.Headers["Content-Type"].FirstOrDefault()).SingleOrDefaultSafe();
            }

            if (url != BaseAddress)
                return;

            RequestUri = response.ResponseUri;

            if (null == ContentType)
                ContentType = _contentTypeDetector.GetContentType(RequestUri, ContentKind.Unknown, response.Headers["Content-Type"].FirstOrDefault()).SingleOrDefaultSafe();
        }

        IEnumerable<KeyValuePair<string, IEnumerable<string>>> GetHeaders(ILookup<string, string> headers)
        {
            return headers.Select(h => new KeyValuePair<string, IEnumerable<string>>(h.Key, h));
        }

        public override string ToString()
        {
            var contentType = null == ContentType ? "<unknown>" : ContentType.ToString();

            if (null != RequestUri && RequestUri != BaseAddress)
                return $"HttpConnectionReader {BaseAddress} [{RequestUri}] ({contentType})";

            return $"HttpConnectionReader {BaseAddress} ({contentType})";
        }
    }
}

// -----------------------------------------------------------------------
//  <copyright file="HttpWebRequestWebReader.cs" company="Henric Jungheim">
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
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using SM.Media.Content;
using SM.Media.Utility;

namespace SM.Media.Web.WebRequestReader
{
    public sealed class HttpWebRequestWebReader : IWebReader
    {
        readonly Uri _baseAddress;
        readonly IContentTypeDetector _contentTypeDetector;
        readonly Uri _referrer;
        readonly HttpWebRequestWebReaderManager _webReaderManager;

        public HttpWebRequestWebReader(HttpWebRequestWebReaderManager webReaderManager, Uri baseAddress, Uri referrer, ContentType contentType, IContentTypeDetector contentTypeDetector)
        {
            if (null == webReaderManager)
                throw new ArgumentNullException("webReaderManager");
            if (contentTypeDetector == null)
                throw new ArgumentNullException("contentTypeDetector");

            _webReaderManager = webReaderManager;
            _baseAddress = baseAddress;
            _referrer = referrer;
            ContentType = contentType;
            _contentTypeDetector = contentTypeDetector;
        }

        #region IWebReader Members

        public Uri BaseAddress
        {
            get { return _baseAddress; }
        }

        public Uri RequestUri { get; private set; }

        public ContentType ContentType { get; private set; }

        public IWebReaderManager Manager
        {
            get { return _webReaderManager; }
        }

        public void Dispose()
        { }

        public async Task<IWebStreamResponse> GetWebStreamAsync(Uri url, bool waitForContent, CancellationToken cancellationToken,
            Uri referrer = null, long? from = null, long? to = null, WebResponse webResponse = null)
        {
            var request = _webReaderManager.CreateRequest(url, referrer, this, ContentType, allowBuffering: waitForContent, fromBytes: from, toBytes: to);

            var response = await request.SendAsync(cancellationToken);

            Update(url, response, webResponse);

            return new HttpWebRequestWebStreamResponse(request, response);
        }

        public async Task<byte[]> GetByteArrayAsync(Uri url, CancellationToken cancellationToken, WebResponse webResponse = null)
        {
            if (null != _baseAddress && !url.IsAbsoluteUri)
                url = new Uri(_baseAddress, url);

            using (var response = await _webReaderManager.SendAsync(url, this, cancellationToken, allowBuffering: true).ConfigureAwait(false))
            {
                Update(url, response, webResponse);

                return await response.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        #endregion

        public async Task<HttpWebResponse> SendAsync(HttpWebRequest request, bool allowBuffering, CancellationToken cancellationToken, WebResponse webResponse = null)
        {
            var url = request.RequestUri;

            var response = await _webReaderManager.SendAsync(_baseAddress, this, cancellationToken, allowBuffering: allowBuffering).ConfigureAwait(false);

            Update(url, response, webResponse);

            return response;
        }

        public HttpWebRequest CreateWebRequest(Uri url)
        {
            return _webReaderManager.CreateRequest(url, null, this, ContentType);
        }

        void Update(Uri url, HttpWebResponse response, WebResponse webResponse)
        {
            if (null != webResponse)
            {
                webResponse.RequestUri = response.ResponseUri;
                webResponse.ContentLength = response.ContentLength >= 0 ? response.ContentLength : null as long?;
                webResponse.Headers = GetHeaders(response.Headers);
                webResponse.ContentType = _contentTypeDetector.GetContentType(RequestUri, response.Headers[HttpRequestHeader.ContentType]).SingleOrDefaultSafe();
            }

            if (url != BaseAddress)
                return;

            RequestUri = response.ResponseUri;

            if (null == ContentType)
                ContentType = _contentTypeDetector.GetContentType(RequestUri, response.Headers[HttpRequestHeader.ContentType]).SingleOrDefaultSafe();
        }

        IEnumerable<KeyValuePair<string, IEnumerable<string>>> GetHeaders(WebHeaderCollection myWebHeaderCollection)
        {
            foreach (var key in myWebHeaderCollection.AllKeys)
            {
                var joinedValues = myWebHeaderCollection[key];

                if (null != joinedValues)
                {
                    var values = joinedValues.Split(',');

                    yield return new KeyValuePair<string, IEnumerable<string>>(key, values);
                }
            }
        }

        public override string ToString()
        {
            var contentType = null == ContentType ? "<unknown>" : ContentType.ToString();

            if (null != RequestUri && RequestUri != BaseAddress)
                return string.Format("HttpWebReader {0} [{1}] ({2})", BaseAddress, RequestUri, contentType);

            return string.Format("HttpWebReader {0} ({1})", BaseAddress, contentType);
        }
    }
}

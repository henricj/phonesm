// -----------------------------------------------------------------------
//  <copyright file="WinRtHttpClientWebReader.cs" company="Henric Jungheim">
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Web.Http;
using SM.Media.Content;
using SM.Media.Utility;
using SM.Media.Web;

namespace SM.Media.WinRtHttpClientReader
{
    public sealed class WinRtHttpClientWebReader : IWebReader
    {
        readonly Uri _baseAddress;
        readonly IContentTypeDetector _contentTypeDetector;
        readonly CancellationTokenSource _disposedCancellationTokenSource = new CancellationTokenSource();
        readonly HttpClient _httpClient;
        readonly IWebReaderManager _webReaderManager;
        int _isDisposed;

        public WinRtHttpClientWebReader(IWebReaderManager webReaderManager, Uri baseAddress, HttpClient httpClient, ContentType contentType, IContentTypeDetector contentTypeDetector)
        {
            if (null == webReaderManager)
                throw new ArgumentNullException("webReaderManager");
            if (null == httpClient)
                throw new ArgumentNullException("httpClient");
            if (contentTypeDetector == null)
                throw new ArgumentNullException("contentTypeDetector");

            _webReaderManager = webReaderManager;
            _baseAddress = baseAddress;
            _httpClient = httpClient;
            ContentType = contentType;
            _contentTypeDetector = contentTypeDetector;
        }

        #region IWebReader Members

        public void Dispose()
        {
            if (0 != Interlocked.Exchange(ref _isDisposed, 1))
                return;

            try
            {
                _disposedCancellationTokenSource.Cancel();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("WinRtHttpClientWebReader.Dispose() Cancellation failed: " + ex.Message);
            }

            _httpClient.Dispose();
        }

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

        public async Task<IWebStreamResponse> GetWebStreamAsync(Uri url, bool waitForContent, CancellationToken cancellationToken,
            Uri referrer = null, long? from = null, long? to = null, WebResponse webResponse = null)
        {
            ThrowIfDisposed();

            var completionOption = waitForContent ? HttpCompletionOption.ResponseContentRead : HttpCompletionOption.ResponseHeadersRead;

            if (null == referrer && null == from && null == to)
            {
                //Debug.WriteLine("WinRtHttpClientWebReader.GetWebStreamAsync() url {0} wait {1}", url, waitForContent);

                var response = await _httpClient.GetAsync(url, completionOption, cancellationToken).ConfigureAwait(false);

                Update(url, response, webResponse);

                return new WinRtHttpClientWebStreamResponse(response);
            }
            else
            {
                //Debug.WriteLine("WinRtHttpClientWebReader.GetWebStreamAsync() url {0} wait {1} referrer {2} [{3}-{4}]",
                //    url, waitForContent, null == referrer ? "<none>" : referrer.ToString(), from, to);

                var request = new HttpRequestMessage(HttpMethod.Get, url);

                var response = await _httpClient.SendAsync(request, completionOption, cancellationToken, referrer, from, to).ConfigureAwait(false);

                Update(url, response, webResponse);

                return new WinRtHttpClientWebStreamResponse(request, response);
            }
        }

        public async Task<byte[]> GetByteArrayAsync(Uri url, CancellationToken cancellationToken, WebResponse webResponse = null)
        {
            ThrowIfDisposed();

            using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();

                Update(url, response, webResponse);

                return await response.Content.ReadAsByteArray(cancellationToken).ConfigureAwait(false);
            }
        }

        #endregion

        void ThrowIfDisposed()
        {
            if (0 == _isDisposed)
                return;

            throw new ObjectDisposedException(GetType().Name);
        }

        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption responseContentRead, CancellationToken cancellationToken, WebResponse webResponse = null)
        {
            ThrowIfDisposed();

            var url = request.RequestUri;

            var response = await _httpClient.SendRequestAsync(request, responseContentRead, cancellationToken).ConfigureAwait(false);

            Update(url, response, webResponse);

            return response;
        }

        void Update(Uri url, HttpResponseMessage response, WebResponse webResponse)
        {
            if (!response.IsSuccessStatusCode)
                return;

            if (null != webResponse)
            {
                webResponse.RequestUri = response.RequestMessage.RequestUri;
                webResponse.ContentLength = (long?)response.Content.Headers.ContentLength;
                webResponse.Headers = response.Headers.Concat(response.Content.Headers)
                    .ToLookup(kv => kv.Key, kv => kv.Value)
                    .Select(l => new KeyValuePair<string, IEnumerable<string>>(l.Key, l));


                webResponse.ContentType = _contentTypeDetector.GetContentType(RequestUri, response.Content.Headers, response.Content.FileName()).SingleOrDefaultSafe();
            }

            if (url != BaseAddress)
                return;

            RequestUri = response.RequestMessage.RequestUri;

            if (null == ContentType)
                ContentType = _contentTypeDetector.GetContentType(RequestUri, response.Content.Headers, response.Content.FileName()).SingleOrDefaultSafe();
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

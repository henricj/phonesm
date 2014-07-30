// -----------------------------------------------------------------------
//  <copyright file="HttpClientWebStreamResponse.cs" company="Henric Jungheim">
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
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SM.Media.Web.HttpClientReader
{
    public sealed class HttpClientWebStreamResponse : IWebStreamResponse
    {
        readonly HttpRequestMessage _request;
        readonly HttpResponseMessage _response;
        Stream _stream;

        public HttpClientWebStreamResponse(HttpResponseMessage response)
        {
            if (null == response)
                throw new ArgumentNullException("response");

            _response = response;
        }

        public HttpClientWebStreamResponse(HttpRequestMessage request, HttpResponseMessage response)
            : this(response)
        {
            _request = request;
        }

        #region IWebStreamResponse Members

        public void Dispose()
        {
            using (_stream)
            { }

            _response.Dispose();

            using (_request)
            { }
        }

        public bool IsSuccessStatusCode
        {
            get { return _response.IsSuccessStatusCode; }
        }

        public void EnsureSuccessStatusCode()
        {
            _response.EnsureSuccessStatusCode();
        }

        public Uri ActualUrl
        {
            get { return _response.RequestMessage.RequestUri; }
        }

        public int HttpStatusCode
        {
            get { return (int)_response.StatusCode; }
        }

        public async Task<Stream> GetStreamAsync(CancellationToken cancellationToken)
        {
            if (null == _stream)
            {
                using (cancellationToken.Register(r => ((HttpRequestMessage)r).Dispose(), _request, false))
                {
                    _stream = await _response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                }
            }

            return _stream;
        }

        public long? ContentLength
        {
            get { return _response.Content.Headers.ContentLength; }
        }

        #endregion
    }
}

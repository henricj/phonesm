// -----------------------------------------------------------------------
//  <copyright file="HttpWebRequestWebStreamResponse.cs" company="Henric Jungheim">
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
using System.Net;
using System.Threading.Tasks;

namespace SM.Media.Web.WebRequestReader
{
    public sealed class HttpWebRequestWebStreamResponse : IWebStreamResponse
    {
        readonly int _httpStatusCode;
        readonly HttpWebResponse _webResponse;

        public HttpWebRequestWebStreamResponse(HttpWebResponse webResponse)
        {
            if (null == webResponse)
                throw new ArgumentNullException("webResponse");

            _webResponse = webResponse;
            _httpStatusCode = (int)_webResponse.StatusCode;
        }

        public HttpWebRequestWebStreamResponse(HttpStatusCode statusCode)
        {
            _httpStatusCode = (int)statusCode;
        }

        #region IWebStreamResponse Members

        public void Dispose()
        {
            using (_webResponse)
            { }
        }

        public bool IsSuccessStatusCode
        {
            get { return null != _webResponse; }
        }

        public Uri ActualUrl
        {
            get { return null == _webResponse ? null : _webResponse.ResponseUri; }
        }

        public int HttpStatusCode
        {
            get { return _httpStatusCode; }
        }

        public long? ContentLength
        {
            get { return null == _webResponse ? null : _webResponse.ContentLength >= 0 ? _webResponse.ContentLength : null as long?; }
        }

        public void EnsureSuccessStatusCode()
        {
            if (_httpStatusCode < 200 || _httpStatusCode >= 300)
                throw new WebException("Invalid status: " + _httpStatusCode);
        }

        public Task<Stream> GetStreamAsync()
        {
            return TaskEx.FromResult(_webResponse.GetResponseStream());
        }

        #endregion
    }
}

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
using System.Threading;
using System.Threading.Tasks;

namespace SM.Media.Web.WebRequestReader
{
    public sealed class HttpWebRequestWebStreamResponse : IWebStreamResponse
    {
        readonly int _httpStatusCode;
        readonly HttpWebRequest _request;
        readonly HttpWebResponse _response;
        Task<Stream> _streamTask;

        public HttpWebRequestWebStreamResponse(HttpWebRequest request, HttpWebResponse response)
        {
            if (null == response)
                throw new ArgumentNullException("response");

            _request = request;
            _response = response;
            _httpStatusCode = (int)_response.StatusCode;
        }

        public HttpWebRequestWebStreamResponse(HttpStatusCode statusCode)
        {
            _httpStatusCode = (int)statusCode;
        }

        #region IWebStreamResponse Members

        public void Dispose()
        {
            if (null != _streamTask && _streamTask.IsCompleted)
                _streamTask.Result.Dispose();

            using (_response)
            { }
        }

        public bool IsSuccessStatusCode
        {
            get { return null != _response; }
        }

        public Uri ActualUrl
        {
            get { return null == _response ? null : _response.ResponseUri; }
        }

        public int HttpStatusCode
        {
            get { return _httpStatusCode; }
        }

        public long? ContentLength
        {
            get { return null == _response ? null : _response.ContentLength >= 0 ? _response.ContentLength : null as long?; }
        }

        public void EnsureSuccessStatusCode()
        {
            if (_httpStatusCode < 200 || _httpStatusCode >= 300)
                throw new WebException("Invalid status: " + _httpStatusCode);
        }

        public Task<Stream> GetStreamAsync(CancellationToken cancellationToken)
        {
            if (null != _streamTask)
                return _streamTask;

            using (cancellationToken.Register(r => ((HttpWebRequest)r).Abort(), _request))
            {
                var stream = _response.GetResponseStream();

                _streamTask = TaskEx.FromResult(stream);

                return _streamTask;
            }
        }

        #endregion
    }
}

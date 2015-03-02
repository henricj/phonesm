// -----------------------------------------------------------------------
//  <copyright file="HttpConnectionRequestFactory.cs" company="Henric Jungheim">
//  Copyright (c) 2012-2015.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012-2015 Henric Jungheim <software@henric.org>
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
using System.Text;
using SM.Media.Content;
using SM.Media.Web.HttpConnection;

namespace SM.Media.Web.HttpConnectionReader
{
    public interface IHttpConnectionRequestFactory
    {
        HttpConnectionRequest CreateRequest(Uri url, Uri referrer, ContentType contentType, long? fromBytes, long? toBytes, IEnumerable<KeyValuePair<string, string>> headers);
    }

    public class HttpConnectionRequestFactory : IHttpConnectionRequestFactory
    {
        readonly IHttpConnectionRequestFactoryParameters _parameters;

        public HttpConnectionRequestFactory(IHttpConnectionRequestFactoryParameters parameters)
        {
            if (null == parameters)
                throw new ArgumentNullException("parameters");

            _parameters = parameters;
        }

        #region IHttpConnectionRequestFactory Members

        public virtual HttpConnectionRequest CreateRequest(Uri url, Uri referrer, ContentType contentType, long? fromBytes, long? toBytes, IEnumerable<KeyValuePair<string, string>> headers)
        {
            var request = new HttpConnectionRequest
            {
                Url = url,
                Referrer = referrer,
                RangeFrom = fromBytes,
                RangeTo = toBytes,
                Proxy = _parameters.Proxy,
                Headers = headers
            };

            if (null != contentType)
                request.Accept = CreateAcceptHeader(contentType);

            return request;
        }

        #endregion

        protected virtual string CreateAcceptHeader(ContentType contentType)
        {
            var sb = new StringBuilder();

            sb.Append(contentType.MimeType);

            if (null != contentType.AlternateMimeTypes)
            {
                foreach (var mimeType in contentType.AlternateMimeTypes)
                {
                    sb.Append(", ");
                    sb.Append(mimeType);
                }
            }

            sb.Append(", */*; q=0.1");

            var accept = sb.ToString();

            return accept;
        }
    }
}

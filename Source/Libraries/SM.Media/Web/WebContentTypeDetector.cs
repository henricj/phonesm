// -----------------------------------------------------------------------
//  <copyright file="WebContentTypeDetector.cs" company="Henric Jungheim">
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SM.Media.Content;

namespace SM.Media.Web
{
    public interface IWebContentTypeDetector
    {
        Task<ContentType> GetContentTypeAsync(Uri source, CancellationToken cancellationToken);
    }

    public class WebContentTypeDetector : IWebContentTypeDetector
    {
        readonly IContentTypeDetector _contentTypeDetector;
        readonly IHttpHeaderReader _headerReader;

        public WebContentTypeDetector(IHttpHeaderReader headerReader, IContentTypeDetector contentTypeDetector)
        {
            _headerReader = headerReader;
            _contentTypeDetector = contentTypeDetector;
        }

        #region IWebContentTypeDetector Members

        public async Task<ContentType> GetContentTypeAsync(Uri source, CancellationToken cancellationToken)
        {
            var contentTypes = _contentTypeDetector.GetContentType(source);

            var contentType = contentTypes.SingleOrDefault();

            if (null != contentType)
                return contentType;

            var headers = await _headerReader.GetHeadersAsync(source, cancellationToken).ConfigureAwait(false);

            if (null == headers)
                return null;

            contentTypes = _contentTypeDetector.GetContentType(source, headers);

            return contentTypes.SingleOrDefault();
        }

        #endregion
    }
}

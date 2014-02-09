// -----------------------------------------------------------------------
//  <copyright file="WebCacheFactory.cs" company="Henric Jungheim">
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
using System.Threading.Tasks;
using SM.Media.Content;

namespace SM.Media.Web
{
    public interface IWebCacheFactory
    {
        Task<IWebCache> CreateAsync(Uri url, Uri baseUrl = null, ContentType contentType = null);
    }

    public class WebCacheFactory : IWebCacheFactory
    {
        readonly IHttpClients _httpClients;

        public WebCacheFactory(IHttpClients httpClients)
        {
            if (null == httpClients)
                throw new ArgumentNullException("httpClients");

            _httpClients = httpClients;
        }

        #region IWebCacheFactory Members

        public Task<IWebCache> CreateAsync(Uri url, Uri baseUrl = null, ContentType contentType = null)
        {
            // TODO: Detect the content type, if none is provided.

            // TODO: Do stuff with the contentType to select the right HttpClient

            return TaskEx.FromResult<IWebCache>(new WebCache(url, _httpClients.CreatePlaylistClient(baseUrl)));
        }

        #endregion
    }
}

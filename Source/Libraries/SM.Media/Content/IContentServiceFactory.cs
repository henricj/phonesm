// -----------------------------------------------------------------------
//  <copyright file="IContentServiceFactory.cs" company="Henric Jungheim">
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
using System.Threading;
using System.Threading.Tasks;
using SM.Media.Web;

namespace SM.Media.Content
{
    public interface IContentServiceFactory<TService, TParameter>
    {
        Task<TService> CreateAsync(TParameter parameter, ContentType contentType, CancellationToken cancellationToken);
        Task<TService> CreateAsync(TParameter parameter, CancellationToken cancellationToken);
    }

    public abstract class ContentServiceFactory<TService, TParameter> : IContentServiceFactory<TService, TParameter>
    {
        static readonly Task<TService> NoHandler = TaskEx.FromResult(default(TService));
        readonly IContentServiceFactoryFinder<TService, TParameter> _factoryFinder;
        readonly IWebContentTypeDetector _webContentTypeDetector;

        public ContentServiceFactory(IWebContentTypeDetector webContentTypeDetector, IContentServiceFactoryFinder<TService, TParameter> factoryFinder)
        {
            if (null == webContentTypeDetector)
                throw new ArgumentNullException("webContentTypeDetector");
            if (null == factoryFinder)
                throw new ArgumentNullException("factoryFinder");

            _webContentTypeDetector = webContentTypeDetector;
            _factoryFinder = factoryFinder;
        }

        #region IContentServiceFactory<TService,TParameter> Members

        public virtual Task<TService> CreateAsync(TParameter parameter, ContentType contentType, CancellationToken cancellationToken)
        {
            if (null == contentType)
                throw new ArgumentNullException("contentType");

            var factory = _factoryFinder.GetFactory(contentType);

            if (null != factory)
                return factory.CreateAsync(parameter, contentType, cancellationToken);

            return NoHandler;
        }

        public virtual async Task<TService> CreateAsync(TParameter parameter, CancellationToken cancellationToken)
        {
            var sources = Sources(parameter);

            if (null == sources)
                return default(TService);

            foreach (var source in sources)
            {
                var contentType = await _webContentTypeDetector.GetContentTypeAsync(source, cancellationToken);

                if (null == contentType)
                    return default(TService);

                return await CreateAsync(parameter, contentType, cancellationToken).ConfigureAwait(false);
            }

            return default(TService);
        }

        #endregion

        protected abstract IEnumerable<Uri> Sources(TParameter parameter);
    }
}

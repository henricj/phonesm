// -----------------------------------------------------------------------
//  <copyright file="SegmentManagerFactory.cs" company="Henric Jungheim">
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
using System.Threading;
using System.Threading.Tasks;
using SM.Media.Content;
using SM.Media.Web;

namespace SM.Media.Segments
{
    public interface ISegmentManagerFactory
    {
        Task<ISegmentManager> CreateAsync(Uri source, ContentType contentType, CancellationToken cancellationToken);
        Task<ISegmentManager> CreateAsync(Uri source, CancellationToken cancellationToken);
    }

    public class SegmentManagerFactory : ISegmentManagerFactory
    {
        static readonly Task<ISegmentManager> NoHandler = TaskEx.FromResult(null as ISegmentManager);
        readonly Func<ContentType, SegmentManagerFactoryDelegate> _factoryFinder;
        readonly IWebContentTypeDetector _webContentTypeDetector;

        public SegmentManagerFactory(IWebContentTypeDetector webContentTypeDetector, Func<ContentType, SegmentManagerFactoryDelegate> factoryFinder)
        {
            if (null == webContentTypeDetector)
                throw new ArgumentNullException("webContentTypeDetector");
            if (null == factoryFinder)
                throw new ArgumentNullException("factoryFinder");

            _webContentTypeDetector = webContentTypeDetector;
            _factoryFinder = factoryFinder;
        }

        #region ISegmentManagerFactory Members

        public virtual Task<ISegmentManager> CreateAsync(Uri source, ContentType contentType, CancellationToken cancellationToken)
        {
            if (null == contentType)
                throw new ArgumentNullException("contentType");

            var factory = _factoryFinder(contentType);

            if (null != factory)
                return factory(source, contentType, cancellationToken);

            return NoHandler;
        }

        public virtual async Task<ISegmentManager> CreateAsync(Uri source, CancellationToken cancellationToken)
        {
            var contentType = await _webContentTypeDetector.GetContentTypeAsync(source, cancellationToken);

            if (null == contentType)
                return null;

            return await CreateAsync(source, contentType, cancellationToken).ConfigureAwait(false);
        }

        #endregion
    }
}

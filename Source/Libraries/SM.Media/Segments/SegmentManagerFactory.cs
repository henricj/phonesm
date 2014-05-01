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

using System.Threading;
using System.Threading.Tasks;
using SM.Media.Content;
using SM.Media.Web;

namespace SM.Media.Segments
{
    public interface ISegmentManagerFactory : IContentServiceFactory<ISegmentManager, ISegmentManagerParameters>
    {
        Task<ISegmentManager> CreateAsync(ISegmentManagerParameters parameters, CancellationToken cancellationToken);
    }

    public class SegmentManagerFactory : ContentServiceFactory<ISegmentManager, ISegmentManagerParameters>, ISegmentManagerFactory
    {
        readonly IWebReaderManager _webReaderManager;

        public SegmentManagerFactory(ISegmentManagerFactoryFinder factoryFinder, IWebReaderManager webReaderManager)
            : base(factoryFinder)
        {
            _webReaderManager = webReaderManager;
        }

        #region ISegmentManagerFactory Members

        public async Task<ISegmentManager> CreateAsync(ISegmentManagerParameters parameters, CancellationToken cancellationToken)
        {
            foreach (var source in parameters.Source)
            {
                var contentType = await _webReaderManager.DetectContentTypeAsync(source, ContentKind.Unknown, cancellationToken).ConfigureAwait(false);

                if (null == contentType)
                    continue;

                return await CreateAsync(parameters, contentType, cancellationToken).ConfigureAwait(false);
            }

            return null;
        }

        #endregion
    }
}

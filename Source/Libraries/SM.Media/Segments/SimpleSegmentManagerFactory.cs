// -----------------------------------------------------------------------
//  <copyright file="SimpleSegmentManagerFactory.cs" company="Henric Jungheim">
//  Copyright (c) 2012-2016.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012-2016 Henric Jungheim <software@henric.org>
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
using SM.Media.Content;
using SM.Media.Web;

namespace SM.Media.Segments
{
    public class SimpleSegmentManagerFactory : ISegmentManagerFactoryInstance
    {
        static readonly ICollection<ContentType> Types = new[]
        {
            ContentTypes.Aac,
            ContentTypes.Ac3,
            ContentTypes.Mp3,
            ContentTypes.TransportStream
        };

        readonly IWebReaderManager _webReaderManager;

        public SimpleSegmentManagerFactory(IWebReaderManager webReaderManager)
        {
            if (null == webReaderManager)
                throw new ArgumentNullException(nameof(webReaderManager));

            _webReaderManager = webReaderManager;
        }

        #region ISegmentManagerFactoryInstance Members

        public ICollection<ContentType> KnownContentTypes => Types;

        public Task<ISegmentManager> CreateAsync(ISegmentManagerParameters parameters, ContentType contentType, CancellationToken cancellationToken)
        {
            var webReader = parameters.WebReader ?? _webReaderManager.CreateRootReader(ContentKind.AnyMedia, contentType);

            var segmentManager = new SimpleSegmentManager(webReader, parameters.Source, parameters.ContentType ?? contentType, parameters.StreamContentType);

            return Task.FromResult<ISegmentManager>(segmentManager);
        }

        #endregion
    }
}

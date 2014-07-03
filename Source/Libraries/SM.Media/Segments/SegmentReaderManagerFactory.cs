// -----------------------------------------------------------------------
//  <copyright file="SegmentReaderManagerFactory.cs" company="Henric Jungheim">
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
using System.Threading;
using System.Threading.Tasks;
using SM.Media.Content;
using SM.Media.Utility;
using SM.Media.Web;

namespace SM.Media.Segments
{
    public interface ISegmentReaderManagerFactory
    {
        Task<ISegmentReaderManager> CreateAsync(ISegmentManagerParameters parameters, ContentType contentType, CancellationToken cancellationToken);
    }

    public class SegmentReaderManagerFactory : ISegmentReaderManagerFactory
    {
        readonly IPlatformServices _platformServices;
        readonly IRetryManager _retryManager;
        readonly ISegmentManagerFactory _segmentManagerFactory;
        readonly IWebReaderManager _webReaderManager;

        public SegmentReaderManagerFactory(IWebReaderManager webReaderManager, ISegmentManagerFactory segmentManagerFactory, IRetryManager retryManager, IPlatformServices platformServices)
        {
            if (null == webReaderManager)
                throw new ArgumentNullException("webReaderManager");
            if (null == segmentManagerFactory)
                throw new ArgumentNullException("segmentManagerFactory");
            if (null == retryManager)
                throw new ArgumentNullException("retryManager");
            if (null == platformServices)
                throw new ArgumentNullException("platformServices");

            _webReaderManager = webReaderManager;
            _segmentManagerFactory = segmentManagerFactory;
            _retryManager = retryManager;
            _platformServices = platformServices;
        }

        #region ISegmentReaderManagerFactory Members

        public async Task<ISegmentReaderManager> CreateAsync(ISegmentManagerParameters parameters, ContentType contentType, CancellationToken cancellationToken)
        {
            ISegmentManager playlist;

            if (null == contentType)
                playlist = await _segmentManagerFactory.CreateAsync(parameters, cancellationToken).ConfigureAwait(false);
            else
                playlist = await _segmentManagerFactory.CreateAsync(parameters, contentType, cancellationToken).ConfigureAwait(false);

            if (null == playlist)
                throw new FileNotFoundException("Unable to create playlist");

            return new SegmentReaderManager(new[] { playlist }, _webReaderManager.RootWebReader, _retryManager, _platformServices);
        }

        #endregion
    }

    public static class SegmentReaderManagerFactoryExtensions
    {
        public static Task<ISegmentReaderManager> CreateAsync(this ISegmentReaderManagerFactory factory, ISegmentManagerParameters parameters, CancellationToken cancellationToken)
        {
            return factory.CreateAsync(parameters, null, cancellationToken);
        }
    }
}

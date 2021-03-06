// -----------------------------------------------------------------------
//  <copyright file="HlsSegmentsFactory.cs" company="Henric Jungheim">
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
using SM.Media.M3U8;
using SM.Media.Segments;
using SM.Media.Web;

namespace SM.Media.Hls
{
    public interface IHlsSegmentsFactory
    {
        Task<ICollection<ISegment>> CreateSegmentsAsync(M3U8Parser parser, IWebReader webReader, CancellationToken cancellationToken);
    }

    public class HlsSegmentsFactory : IHlsSegmentsFactory
    {
        readonly IHlsStreamSegmentsFactory _streamSegmentsFactory;

        public HlsSegmentsFactory(IHlsStreamSegmentsFactory streamSegmentsFactory)
        {
            if (null == streamSegmentsFactory)
                throw new ArgumentNullException(nameof(streamSegmentsFactory));

            _streamSegmentsFactory = streamSegmentsFactory;
        }

        #region IHlsSegmentsFactory Members

        public Task<ICollection<ISegment>> CreateSegmentsAsync(M3U8Parser parser, IWebReader webReader, CancellationToken cancellationToken)
        {
            var streamSegments = _streamSegmentsFactory.Create(parser, webReader);

            return streamSegments.CreateSegmentsAsync(cancellationToken);
        }

        #endregion
    }
}

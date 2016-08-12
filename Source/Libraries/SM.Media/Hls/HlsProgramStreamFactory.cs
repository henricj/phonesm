// -----------------------------------------------------------------------
//  <copyright file="HlsProgramStreamFactory.cs" company="Henric Jungheim">
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
using SM.Media.Content;
using SM.Media.Metadata;
using SM.Media.Utility;
using SM.Media.Web;

namespace SM.Media.Hls
{
    public interface IHlsProgramStreamFactory
    {
        IHlsProgramStream Create(ICollection<Uri> urls, IWebReader webReader, ContentType contentType, ContentType streamContentType);
    }

    public class HlsProgramStreamFactory : IHlsProgramStreamFactory
    {
        readonly IPlatformServices _platformServices;
        readonly IRetryManager _retryManager;
        readonly IHlsSegmentsFactory _segmentsFactory;
        readonly IWebMetadataFactory _webMetadataFactory;

        public HlsProgramStreamFactory(IHlsSegmentsFactory segmentsFactory, IWebMetadataFactory webMetadataFactory, IPlatformServices platformServices, IRetryManager retryManager)
        {
            if (null == segmentsFactory)
                throw new ArgumentNullException(nameof(segmentsFactory));
            if (null == webMetadataFactory)
                throw new ArgumentNullException(nameof(webMetadataFactory));
            if (null == platformServices)
                throw new ArgumentNullException(nameof(platformServices));
            if (null == retryManager)
                throw new ArgumentNullException(nameof(retryManager));

            _segmentsFactory = segmentsFactory;
            _webMetadataFactory = webMetadataFactory;
            _platformServices = platformServices;
            _retryManager = retryManager;
        }

        #region IHlsProgramStreamFactory Members

        public IHlsProgramStream Create(ICollection<Uri> urls, IWebReader webReader, ContentType contentType, ContentType streamContentType)
        {
            return new HlsProgramStream(webReader, urls, contentType, streamContentType, _segmentsFactory, _webMetadataFactory, _platformServices, _retryManager);
        }

        #endregion
    }
}

// -----------------------------------------------------------------------
//  <copyright file="HlsPlaylistSegmentManagerFactory.cs" company="Henric Jungheim">
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
using SM.Media.Segments;
using SM.Media.Utility;

namespace SM.Media.Hls
{
    public class HlsPlaylistSegmentManagerFactory : ISegmentManagerFactoryInstance
    {
        static readonly ICollection<ContentType> Types = new[] { ContentTypes.M3U8, ContentTypes.M3U };
        readonly IHlsPlaylistSegmentManagerPolicy _hlsPlaylistSegmentManagerPolicy;
        readonly IPlatformServices _platformServices;

        public HlsPlaylistSegmentManagerFactory(IHlsPlaylistSegmentManagerPolicy hlsPlaylistSegmentManagerPolicy, IPlatformServices platformServices)
        {
            if (null == hlsPlaylistSegmentManagerPolicy)
                throw new ArgumentNullException(nameof(hlsPlaylistSegmentManagerPolicy));
            if (null == platformServices)
                throw new ArgumentNullException(nameof(platformServices));

            _hlsPlaylistSegmentManagerPolicy = hlsPlaylistSegmentManagerPolicy;
            _platformServices = platformServices;
        }

        #region ISegmentManagerFactoryInstance Members

        public ICollection<ContentType> KnownContentTypes => Types;

        public async Task<ISegmentManager> CreateAsync(ISegmentManagerParameters parameters, ContentType contentType, CancellationToken cancellationToken)
        {
            var subProgram = await _hlsPlaylistSegmentManagerPolicy.CreateSubProgramAsync(parameters.Source, parameters.ContentType ?? contentType, parameters.StreamContentType, cancellationToken).ConfigureAwait(false);

            var segmentManager = new HlsPlaylistSegmentManager(subProgram.Video, parameters.ContentType ?? contentType, parameters.StreamContentType, _platformServices, cancellationToken);

            return segmentManager;
        }

        #endregion
    }
}

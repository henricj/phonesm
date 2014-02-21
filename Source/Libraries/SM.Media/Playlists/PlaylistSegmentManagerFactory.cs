// -----------------------------------------------------------------------
//  <copyright file="PlaylistSegmentManagerFactory.cs" company="Henric Jungheim">
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SM.Media.Content;
using SM.Media.M3U8;
using SM.Media.Segments;
using SM.Media.Web;

namespace SM.Media.Playlists
{
    public class PlaylistSegmentManagerFactory : ISegmentManagerFactoryInstance
    {
        static readonly ICollection<ContentType> Types = new[] { ContentTypes.M3U8, ContentTypes.M3U };
        public static Func<IEnumerable<ISubProgram>, ISubProgram> SelectSubProgram = programs => programs.FirstOrDefault();
        readonly IHttpClients _httpClients;
        readonly IPlaylistSegmentManagerParameters _parameters;
        readonly Func<M3U8Parser, IStreamSegments> _segmentsFactory;
        readonly IWebCacheFactory _webCacheFactory;
        readonly IWebContentTypeDetector _webContentTypeDetector;

        public PlaylistSegmentManagerFactory(IHttpClients httpClients, IWebCacheFactory webCacheFactory,
            IWebContentTypeDetector webContentTypeDetector, IPlaylistSegmentManagerParameters parameters)
        {
            if (null == httpClients)
                throw new ArgumentNullException("httpClients");
            if (webCacheFactory == null) throw new ArgumentNullException("webCacheFactory");
            if (webContentTypeDetector == null) throw new ArgumentNullException("webContentTypeDetector");
            if (parameters == null) throw new ArgumentNullException("parameters");

            _httpClients = httpClients;
            _webCacheFactory = webCacheFactory;
            _webContentTypeDetector = webContentTypeDetector;
            _parameters = parameters;
            _segmentsFactory = new SegmentsFactory(httpClients).CreateStreamSegments;
        }

        #region ISegmentManagerFactoryInstance Members

        public ICollection<ContentType> KnownContentTypes
        {
            get { return Types; }
        }

        public async Task<ISegmentManager> CreateAsync(ICollection<Uri> source, ContentType contentType, CancellationToken cancellationToken)
        {
            var programManager = new ProgramManager(_httpClients, _segmentsFactory)
                                 {
                                     Playlists = source
                                 };

            var segmentManager = new PlaylistSegmentManager(_parameters, programManager, contentType, _webCacheFactory, _segmentsFactory, _webContentTypeDetector, cancellationToken);

            return segmentManager;
        }

        #endregion
    }
}

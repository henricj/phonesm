// -----------------------------------------------------------------------
//  <copyright file="MediaStreamFascadeParameters.cs" company="Henric Jungheim">
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
using System.Diagnostics;
using SM.Media.Content;
using SM.Media.Segments;
using SM.Media.Web;

namespace SM.Media
{
    public class MediaStreamFascadeParameters : IMediaStreamFascadeParameters
    {
        readonly IHttpClients _httpClients;
        readonly Func<IMediaStreamSource> _mediaStreamSourceFactory;
        Func<Uri, ICachedWebRequest> _cachedWebRequestFactory;
        IMediaManagerParameters _mediaManagerParameters;
        ISegmentManagerFactory _segmentManagerFactory;

        public MediaStreamFascadeParameters(IHttpClients httpClients, Func<IMediaStreamSource> mediaStreamSourceFactory)
        {
            if (null == httpClients)
                throw new ArgumentNullException("httpClients");
            if (null == mediaStreamSourceFactory)
                throw new ArgumentNullException("mediaStreamSourceFactory");

            _httpClients = httpClients;
            _mediaStreamSourceFactory = mediaStreamSourceFactory;
        }

        #region IMediaStreamFascadeParameters Members

        public IHttpClients HttpClients
        {
            get { return _httpClients; }
        }

        public ISegmentManagerFactory SegmentManagerFactory
        {
            get
            {
                if (null == _segmentManagerFactory)
                    _segmentManagerFactory = CreateSegmentManagerFactory();

                Debug.Assert(null != _segmentManagerFactory);

                return _segmentManagerFactory;
            }

            set { _segmentManagerFactory = value; }
        }

        public Func<IMediaStreamSource> MediaStreamSourceFactory
        {
            get { return _mediaStreamSourceFactory; }
        }

        public IMediaManagerParameters MediaManagerParameters
        {
            get
            {
                if (null == _mediaManagerParameters)
                    _mediaManagerParameters = new MediaManagerParameters();

                return _mediaManagerParameters;
            }

            set { _mediaManagerParameters = value; }
        }

        public Func<Uri, ICachedWebRequest> CachedWebRequestFactory
        {
            get
            {
                if (null == _cachedWebRequestFactory)
                    _cachedWebRequestFactory = uri => new CachedWebRequest(uri, _httpClients.CreatePlaylistClient(uri));

                return _cachedWebRequestFactory;
            }
            set { _cachedWebRequestFactory = value; }
        }

        #endregion

        protected virtual ISegmentManagerFactory CreateSegmentManagerFactory()
        {
            var httpHeaderReader = new HttpHeaderReader(_httpClients);
            var contentTypeDetector = new ContentTypeDetector(ContentTypes.AllTypes);
            var webContentTypeDector = new WebContentTypeDetector(httpHeaderReader, contentTypeDetector);
            var segmentManagerFactories = new SegmentManagerFactories(_httpClients, httpHeaderReader, contentTypeDetector, webContentTypeDector, CachedWebRequestFactory);

            return new SegmentManagerFactory(webContentTypeDector, segmentManagerFactories.GetFactory);
        }

        public static MediaStreamFascadeParameters Create<TMediaStreamSource>(IHttpClients httpClients)
            where TMediaStreamSource : IMediaStreamSource, new()
        {
            return new MediaStreamFascadeParameters(httpClients, () => new TMediaStreamSource());
        }
    }
}

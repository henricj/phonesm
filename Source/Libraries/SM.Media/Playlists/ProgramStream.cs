// -----------------------------------------------------------------------
//  <copyright file="ProgramStream.cs" company="Henric Jungheim">
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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SM.Media.Content;
using SM.Media.M3U8;
using SM.Media.Segments;
using SM.Media.Web;

namespace SM.Media.Playlists
{
    public interface IProgramStream
    {
        string StreamType { get; }
        string Language { get; }

        /// <summary>
        ///     A list of URLs representing the same data.  They are provided in order of preference.
        /// </summary>
        ICollection<Uri> Urls { get; }

        Uri ActualUrl { get; }
        bool IsDyanmicPlaylist { get; }
        ICollection<ISegment> Segments { get; }

        Task RefreshPlaylistAsync(CancellationToken cancellationToken);
        Task<ContentType> GetContentTypeAsync(CancellationToken cancellationToken);
    }

    public class ProgramStream : IProgramStream
    {
        static readonly ISegment[] NoPlaylist = new ISegment[0];
        readonly Func<M3U8Parser, IStreamSegments> _segmentsFactory;
        readonly IWebCacheFactory _webCacheFactory;
        readonly IWebContentTypeDetector _webContentTypeDetector;
        Uri _actualUrl;
        bool _isDyanmicPlaylist = true;
        ICollection<ISegment> _segments = NoPlaylist;
        IWebCache _subPlaylistCache;

        public ProgramStream(Func<M3U8Parser, IStreamSegments> segmentsFactory, IWebCacheFactory webCacheFactory, IWebContentTypeDetector webContentTypeDetector, M3U8Parser parser = null)
        {
            if (null == segmentsFactory)
                throw new ArgumentNullException("segmentsFactory");
            if (null == webCacheFactory)
                throw new ArgumentNullException("webCacheFactory");
            if (null == webContentTypeDetector)
                throw new ArgumentNullException("webContentTypeDetector");

            _segmentsFactory = segmentsFactory;
            _webCacheFactory = webCacheFactory;
            _webContentTypeDetector = webContentTypeDetector;

            if (null != parser)
                Update(parser);
        }

        #region IProgramStream Members

        public string StreamType { get; internal set; }
        public string Language { get; internal set; }
        public ICollection<Uri> Urls { get; internal set; }

        public Uri ActualUrl
        {
            get { return _actualUrl; }
        }

        public bool IsDyanmicPlaylist
        {
            get { return _isDyanmicPlaylist; }
        }

        public ICollection<ISegment> Segments
        {
            get { return _segments; }
        }

        public async Task RefreshPlaylistAsync(CancellationToken cancellationToken)
        {
            if (!_isDyanmicPlaylist && null != _segments)
                return;

            var parser = await FetchPlaylistAsync(cancellationToken).ConfigureAwait(false);

            Update(parser);
        }

        public Task<ContentType> GetContentTypeAsync(CancellationToken cancellationToken)
        {
            if (null == _segments)
                return null;

            var segment0 = _segments.FirstOrDefault();

            if (null == segment0 || null == segment0.Url)
                return null;

            return _webContentTypeDetector.GetContentTypeAsync(segment0.Url, cancellationToken);
        }

        #endregion

        void Update(M3U8Parser parser)
        {
            var segments = _segmentsFactory(parser)
                .GetPlaylist();

            _segments = segments;
            _isDyanmicPlaylist = PlaylistSettings.Parameters.IsDyanmicPlaylist(parser);
            _actualUrl = parser.BaseUrl;
        }

        async Task<M3U8Parser> FetchPlaylistAsync(CancellationToken cancellationToken)
        {
            var urls = Urls;

            if (null == urls || urls.Count < 1)
                return null;

            foreach (var playlist in urls)
            {
                if (null == _subPlaylistCache || _subPlaylistCache.Url != playlist)
                    _subPlaylistCache = await _webCacheFactory.CreateAsync(playlist).ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();

                var parsedPlaylist = await _subPlaylistCache.ReadAsync(
                    bytes =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (bytes.Length < 1)
                            return null;

                        var parser = new M3U8Parser();

                        using (var ms = new MemoryStream(bytes))
                        {
                            parser.Parse(_subPlaylistCache.RequestUri, ms);
                        }

                        return parser;
                    }, cancellationToken)
                                                            .ConfigureAwait(false);

                if (null != parsedPlaylist)
                    return parsedPlaylist;
            }

            return null;
        }

        public override string ToString()
        {
            return string.Format("dynamic {0} segments {1} url {2}", _isDyanmicPlaylist, null == _segments ? 0 : _segments.Count, _actualUrl);
        }
    }
}

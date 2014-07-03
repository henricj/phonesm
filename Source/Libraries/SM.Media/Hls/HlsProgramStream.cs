// -----------------------------------------------------------------------
//  <copyright file="HlsProgramStream.cs" company="Henric Jungheim">
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
using SM.Media.Playlists;
using SM.Media.Segments;
using SM.Media.Utility;
using SM.Media.Web;

namespace SM.Media.Hls
{
    public class HlsProgramStream : IProgramStream
    {
        static readonly ISegment[] NoPlaylist = new ISegment[0];
        readonly IHlsSegmentsFactory _segmentsFactory;
        readonly IWebReader _webReader;
        Uri _actualUrl;
        ContentType _contentType;
        bool _isDynamicPlaylist = true;
        ICollection<ISegment> _segments = NoPlaylist;
        IWebCache _subPlaylistCache;

        public HlsProgramStream(IWebReader webReader, IPlatformServices platformServices, M3U8Parser parser = null)
        {
            if (null == webReader)
                throw new ArgumentNullException("webReader");
            if (null == platformServices)
                throw new ArgumentNullException("platformServices");

            _webReader = webReader;

            _segmentsFactory = new HlsSegmentsFactory(platformServices);

            if (null != parser)
            {
                UpdateSubPlaylistCache(parser.BaseUrl);

                Update(parser);
            }
        }

        #region IProgramStream Members

        public IWebReader WebReader
        {
            get { return _webReader; }
        }

        public string StreamType { get; internal set; }
        public string Language { get; internal set; }
        public ICollection<Uri> Urls { get; internal set; }

        public bool IsDynamicPlaylist
        {
            get { return _isDynamicPlaylist; }
        }

        public ICollection<ISegment> Segments
        {
            get { return _segments; }
        }

        public async Task RefreshPlaylistAsync(CancellationToken cancellationToken)
        {
            if (!_isDynamicPlaylist && null != _segments && _segments.Count > 0)
                return;

            var parser = await FetchPlaylistAsync(cancellationToken).ConfigureAwait(false);

            Update(parser);
        }

        public async Task<ContentType> GetContentTypeAsync(CancellationToken cancellationToken)
        {
            if (null == _segments)
                return null;

            var segment0 = _segments.FirstOrDefault();

            if (null == segment0 || null == segment0.Url)
                return null;

            _contentType = await _subPlaylistCache.WebReader.DetectContentTypeAsync(segment0.Url, ContentKind.AnyMedia, cancellationToken).ConfigureAwait(false);

            return _contentType;
        }

        #endregion

        void Update(M3U8Parser parser)
        {
            _segments = _segmentsFactory.CreateSegments(parser, _subPlaylistCache.WebReader);
            _isDynamicPlaylist = HlsPlaylistSettings.Parameters.IsDynamicPlaylist(parser);
            _actualUrl = parser.BaseUrl;
        }

        void UpdateSubPlaylistCache(Uri playlist)
        {
            if (null == _subPlaylistCache || _subPlaylistCache.WebReader.BaseAddress != playlist)
                _subPlaylistCache = _webReader.CreateWebCache(playlist, ContentKind.Playlist);
        }

        async Task<M3U8Parser> FetchPlaylistAsync(CancellationToken cancellationToken)
        {
            var urls = Urls;

            if (null == urls || urls.Count < 1)
                return null;

            foreach (var playlist in urls)
            {
                UpdateSubPlaylistCache(playlist);

                cancellationToken.ThrowIfCancellationRequested();

                var parsedPlaylist = await _subPlaylistCache.ReadAsync(
                    (actualUri, bytes) =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (bytes.Length < 1)
                            return null;

                        var parser = new M3U8Parser();

                        using (var ms = new MemoryStream(bytes))
                        {
                            parser.Parse(actualUri, ms);
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
            return string.Format("dynamic {0} segments {1} url {2}", _isDynamicPlaylist, null == _segments ? 0 : _segments.Count, _actualUrl);
        }
    }
}

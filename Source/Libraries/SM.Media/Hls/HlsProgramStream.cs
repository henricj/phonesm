// -----------------------------------------------------------------------
//  <copyright file="HlsProgramStream.cs" company="Henric Jungheim">
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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SM.Media.Content;
using SM.Media.M3U8;
using SM.Media.Metadata;
using SM.Media.Playlists;
using SM.Media.Segments;
using SM.Media.Utility;
using SM.Media.Web;

namespace SM.Media.Hls
{
    public interface IHlsProgramStream : IProgramStream
    {
        Task SetParserAsync(M3U8Parser parser, CancellationToken cancellationToken);
    }

    public class HlsProgramStream : IHlsProgramStream
    {
        static readonly ISegment[] NoPlaylist = new ISegment[0];
        readonly IHlsSegmentsFactory _segmentsFactory;
        readonly IWebMetadataFactory _webMetadataFactory;
        readonly IWebReader _webReader;
        Uri _actualUrl;
        ContentType _contentType;
        bool _isDynamicPlaylist = true;
        ICollection<ISegment> _segments = NoPlaylist;
        IWebCache _subPlaylistCache;

        public HlsProgramStream(IWebReader webReader, ICollection<Uri> urls, ContentType contentType, ContentType streamContentType, IHlsSegmentsFactory segmentsFactory, IWebMetadataFactory webMetadataFactory, IPlatformServices platformServices, IRetryManager retryManager)
        {
            if (null == segmentsFactory)
                throw new ArgumentNullException(nameof(segmentsFactory));
            if (null == webMetadataFactory)
                throw new ArgumentNullException(nameof(webMetadataFactory));
            if (null == webReader)
                throw new ArgumentNullException(nameof(webReader));
            if (null == platformServices)
                throw new ArgumentNullException(nameof(platformServices));
            if (null == retryManager)
                throw new ArgumentNullException(nameof(retryManager));

            _webReader = webReader;
            _segmentsFactory = segmentsFactory;
            _webMetadataFactory = webMetadataFactory;
            Urls = urls;
            ContentType = contentType;
            StreamContentType = streamContentType;
        }

        #region IHlsProgramStream Members

        public IWebReader WebReader => _webReader;

        public string StreamType { get; }
        public string Language { get; }
        public ICollection<Uri> Urls { get; }
        public ContentType ContentType { get; }
        public ContentType StreamContentType { get; }

        public bool IsDynamicPlaylist => _isDynamicPlaylist;

        public IStreamMetadata StreamMetadata { get; set; }

        public ICollection<ISegment> Segments => _segments;

        public async Task RefreshPlaylistAsync(CancellationToken cancellationToken)
        {
            if (!_isDynamicPlaylist && null != _segments && _segments.Count > 0)
                return;

            var parser = await FetchPlaylistAsync(cancellationToken).ConfigureAwait(false);

            await UpdateAsync(parser, cancellationToken).ConfigureAwait(false);
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

        public Task SetParserAsync(M3U8Parser parser, CancellationToken cancellationToken)
        {
            UpdateSubPlaylistCache(parser.BaseUrl);

            return UpdateAsync(parser, cancellationToken);
        }

        #endregion

        async Task UpdateAsync(M3U8Parser parser, CancellationToken cancellationToken)
        {
            _segments = await _segmentsFactory.CreateSegmentsAsync(parser, _subPlaylistCache.WebReader, cancellationToken).ConfigureAwait(false);
            _isDynamicPlaylist = HlsPlaylistSettings.Parameters.IsDynamicPlaylist(parser);
            _actualUrl = parser.BaseUrl;
        }

        void UpdateSubPlaylistCache(Uri playlist)
        {
            if (null == _subPlaylistCache || _subPlaylistCache.WebReader.BaseAddress != playlist)
            {
                if (null != _subPlaylistCache)
                    _subPlaylistCache.WebReader.Dispose();

                _subPlaylistCache = _webReader.CreateWebCache(playlist, ContentKind.Playlist, ContentType);
            }
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

                WebResponse webResponse = null;

                if (null == StreamMetadata)
                    webResponse = new WebResponse();

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
                    }, cancellationToken, webResponse)
                    .ConfigureAwait(false);


                if (null != parsedPlaylist)
                {
                    if (null != webResponse)
                        StreamMetadata = _webMetadataFactory.CreateStreamMetadata(webResponse);

                    return parsedPlaylist;
                }
            }

            return null;
        }

        public override string ToString()
        {
            return string.Format("dynamic {0} segments {1} url {2}", _isDynamicPlaylist, null == _segments ? 0 : _segments.Count, _actualUrl);
        }
    }
}

// -----------------------------------------------------------------------
//  <copyright file="PlsSegmentManagerFactory.cs" company="Henric Jungheim">
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using SM.Media.Content;
using SM.Media.Segments;
using SM.Media.Utility;
using SM.Media.Web;

namespace SM.Media.Pls
{
    public class PlsSegmentManagerFactory : ISegmentManagerFactoryInstance
    {
        static readonly ICollection<ContentType> Types = new[] { ContentTypes.Pls };

        readonly IContentTypeDetector _contentTypeDetector;
        readonly IHttpHeaderReader _headerReader;
        readonly IHttpClients _httpClients;

        public PlsSegmentManagerFactory(IHttpClients httpClients, IHttpHeaderReader headerReader, IContentTypeDetector contentTypeDetector)
        {
            if (null == httpClients)
                throw new ArgumentNullException("httpClients");
            if (null == headerReader)
                throw new ArgumentNullException("headerReader");
            if (null == contentTypeDetector)
                throw new ArgumentNullException("contentTypeDetector");

            _httpClients = httpClients;
            _headerReader = headerReader;
            _contentTypeDetector = contentTypeDetector;
        }

        #region ISegmentManagerFactoryInstance Members

        public ICollection<ContentType> KnownContentTypes
        {
            get { return Types; }
        }

        public async Task<ISegmentManager> CreateAsync(IEnumerable<Uri> source, ContentType contentType, CancellationToken cancellationToken)
        {
            var httpClient = _httpClients.RootPlaylistClient;
            var pls = new PlsParser();

            foreach (var url in source)
            {
                var retry = new Retry(3, 333, RetryPolicy.IsWebExceptionRetryable);

                var localUrl = url;

                var segmentManager = await retry.CallAsync(() => ReadPlaylistAsync(localUrl, httpClient, pls, cancellationToken), cancellationToken);

                if (null != segmentManager)
                    return segmentManager;
            }

            return null;
        }

        #endregion

        async Task<ISegmentManager> CreateManagerAsync(PlsParser pls, Uri playlistUri, CancellationToken cancellationToken)
        {
            var tracks = pls.Tracks;

            if (tracks.Count < 1)
                return null;

            if (tracks.Count > 1)
                Debug.WriteLine("PlsSegmentManagerFactory.CreateSegmentManager() multiple tracks are not supported");

            var track = tracks.First();

            if (null == track.File)
                Debug.WriteLine("PlsSegmentManagerFactory.CreateSegmentManager() track does not have a file");

            Uri trackUrl;
            if (!Uri.TryCreate(playlistUri, track.File, out trackUrl))
            {
                Debug.WriteLine("PlsSegmentManagerFactory.CreateSegmentManager() invalid track file: " + track.File);
                return null;
            }

            var headers = await _headerReader.GetHeadersAsync(trackUrl, false, cancellationToken).ConfigureAwait(false);

            if (null == headers)
            {
                Debug.WriteLine("PlsSegmentManagerFactory.CreateSegmentManager() unable fetch headers for " + trackUrl);
                return null;
            }

            DumpIcy(headers.ResponseHeaders);

            var contentType = _contentTypeDetector.GetContentType(headers.Url, headers.ContentHeaders).SingleOrDefault();

            if (null == contentType)
            {
                Debug.WriteLine("PlsSegmentManagerFactory.CreateSegmentManager() unable to detect type for " + trackUrl);
                return null;
            }

            return new SimpleSegmentManager(new[] { trackUrl }, contentType);
        }

        [Conditional("DEBUG")]
        void DumpIcy(HttpResponseHeaders httpResponseHeaders)
        {
            var icys = httpResponseHeaders.Where(kv => kv.Key.StartsWith("icy-", StringComparison.OrdinalIgnoreCase));

            foreach (var icy in icys)
            {
                Debug.WriteLine("Icecast {0}: ", icy.Key);

                foreach (var v in icy.Value)
                    Debug.WriteLine("       " + v);
            }
        }

        async Task<ISegmentManager> ReadPlaylistAsync(Uri url, HttpClient httpClient, PlsParser pls, CancellationToken cancellationToken)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            using (var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();

                bool ret;

                using (var tr = new StreamReader(await response.Content.ReadAsStreamAsync().ConfigureAwait(false)))
                {
                    ret = await pls.Parse(tr).ConfigureAwait(false);
                }

                if (!ret)
                    return null;

                return await CreateManagerAsync(pls, response.RequestMessage.RequestUri, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}

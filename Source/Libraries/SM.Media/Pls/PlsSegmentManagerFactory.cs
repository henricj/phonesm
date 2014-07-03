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
        readonly IRetryManager _retryManager;

        readonly IWebReader _rootWebReader;

        public PlsSegmentManagerFactory(IWebReaderManager webReaderManager, IRetryManager retryManager)
        {
            if (null == webReaderManager)
                throw new ArgumentNullException("webReaderManager");
            if (null == retryManager)
                throw new ArgumentNullException("retryManager");

            _rootWebReader = webReaderManager.RootWebReader;
            _retryManager = retryManager;
        }

        #region ISegmentManagerFactoryInstance Members

        public ICollection<ContentType> KnownContentTypes
        {
            get { return Types; }
        }

        public async Task<ISegmentManager> CreateAsync(ISegmentManagerParameters parameters, ContentType contentType, CancellationToken cancellationToken)
        {
            foreach (var url in parameters.Source)
            {
                var localUrl = url;

                var retry = _retryManager.CreateWebRetry(3, 333);

                var segmentManager = await retry.CallAsync(
                    async () =>
                    {
                        var webReader = _rootWebReader.CreateChild(localUrl, ContentTypes.Pls.Kind, ContentTypes.Pls);

                        try
                        {
                            using (var webStream = await webReader.GetWebStreamAsync(localUrl, false, cancellationToken).ConfigureAwait(false))
                            {
                                if (!webStream.IsSuccessStatusCode)
                                {
                                    webReader.Dispose();
                                    return null;
                                }

                                using (var stream = await webStream.GetStreamAsync(cancellationToken).ConfigureAwait(false))
                                {
                                    return await ReadPlaylistAsync(webReader, webStream.ActualUrl, stream, cancellationToken).ConfigureAwait(false);
                                }
                            }
                        }
                        catch (Exception)
                        {
                            webReader.Dispose();
                            throw;
                        }
                    },
                    cancellationToken);

                if (null != segmentManager)
                    return segmentManager;
            }

            return null;
        }

        #endregion

        async Task<ISegmentManager> CreateManagerAsync(PlsParser pls, IWebReader webReader, CancellationToken cancellationToken)
        {
            var playlistUri = webReader.RequestUri;

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

            var contentType = await webReader.DetectContentTypeAsync(trackUrl, ContentKind.AnyMedia, cancellationToken).ConfigureAwait(false);

            //DumpIcy(headers.ResponseHeaders);

            if (null == contentType)
            {
                Debug.WriteLine("PlsSegmentManagerFactory.CreateSegmentManager() unable to detect type for " + trackUrl);
                return null;
            }

            return new SimpleSegmentManager(webReader, new[] { trackUrl }, contentType);
        }

        [Conditional("DEBUG")]
        void DumpIcy(IEnumerable<KeyValuePair<string, IEnumerable<string>>> httpResponseHeaders)
        {
            var icys = httpResponseHeaders.Where(kv => kv.Key.StartsWith("icy-", StringComparison.OrdinalIgnoreCase));

            foreach (var icy in icys)
            {
                Debug.WriteLine("Icecast {0}: ", icy.Key);

                foreach (var v in icy.Value)
                    Debug.WriteLine("       " + v);
            }
        }

        async Task<ISegmentManager> ReadPlaylistAsync(IWebReader webReader, Uri url, Stream stream, CancellationToken cancellationToken)
        {
            var pls = new PlsParser();

            using (var tr = new StreamReader(stream))
            {
                var ret = await pls.Parse(tr).ConfigureAwait(false);

                if (!ret)
                    return null;
            }

            return await CreateManagerAsync(pls, webReader, cancellationToken).ConfigureAwait(false);
        }
    }
}

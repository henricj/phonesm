﻿// -----------------------------------------------------------------------
//  <copyright file="PlsSegmentManagerFactory.cs" company="Henric Jungheim">
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
        readonly IPlsSegmentManagerPolicy _plsSegmentManagerPolicy;
        readonly IRetryManager _retryManager;
        readonly IWebReaderManager _webReaderManager;

        public PlsSegmentManagerFactory(IWebReaderManager webReaderManager, IPlsSegmentManagerPolicy plsSegmentManagerPolicy, IRetryManager retryManager)
        {
            if (null == webReaderManager)
                throw new ArgumentNullException(nameof(webReaderManager));
            if (null == plsSegmentManagerPolicy)
                throw new ArgumentNullException(nameof(plsSegmentManagerPolicy));
            if (null == retryManager)
                throw new ArgumentNullException(nameof(retryManager));

            _webReaderManager = webReaderManager;
            _plsSegmentManagerPolicy = plsSegmentManagerPolicy;
            _retryManager = retryManager;
        }

        protected virtual async Task<ISegmentManager> CreateManagerAsync(PlsParser pls, IWebReader webReader, ContentType contentType, ContentType streamContentType, CancellationToken cancellationToken)
        {
            var trackUrl = await _plsSegmentManagerPolicy.GetTrackAsync(pls, webReader.ContentType, cancellationToken);

            if (null == trackUrl)
                return null;

            if (null == streamContentType)
                streamContentType = await webReader.DetectContentTypeAsync(trackUrl, ContentKind.AnyMedia, cancellationToken).ConfigureAwait(false);

            //DumpIcy(headers.ResponseHeaders);

            if (null == streamContentType)
            {
                Debug.WriteLine("PlsSegmentManagerFactory.CreateManagerAsync() unable to detect type for " + trackUrl);
                return null;
            }

            return new SimpleSegmentManager(webReader, new[] { trackUrl }, contentType, streamContentType);
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

        protected virtual async Task<ISegmentManager> ReadPlaylistAsync(IWebReader webReader, Uri url, Stream stream, ContentType contentType, ContentType streamContentType, CancellationToken cancellationToken)
        {
            var pls = new PlsParser(url);

            using (var tr = new StreamReader(stream))
            {
                var ret = await pls.ParseAsync(tr).ConfigureAwait(false);

                if (!ret)
                    return null;
            }

            return await CreateManagerAsync(pls, webReader, contentType, streamContentType, cancellationToken).ConfigureAwait(false);
        }

        #region ISegmentManagerFactoryInstance Members

        public ICollection<ContentType> KnownContentTypes => Types;

        public virtual async Task<ISegmentManager> CreateAsync(ISegmentManagerParameters parameters, ContentType contentType, CancellationToken cancellationToken)
        {
            foreach (var url in parameters.Source)
            {
                var localUrl = url;

                var retry = _retryManager.CreateWebRetry(3, 333);

                var segmentManager = await retry.CallAsync(
                    async () =>
                    {
                        var webReader = _webReaderManager.CreateReader(localUrl, ContentTypes.Pls.Kind, contentType: ContentTypes.Pls);

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
                                    return await ReadPlaylistAsync(webReader, webStream.ActualUrl, stream, parameters.ContentType, parameters.StreamContentType, cancellationToken).ConfigureAwait(false);
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
    }
}

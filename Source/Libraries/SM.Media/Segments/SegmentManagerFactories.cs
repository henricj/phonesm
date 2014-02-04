// -----------------------------------------------------------------------
//  <copyright file="SegmentManagerFactories.cs" company="Henric Jungheim">
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
using System.Threading;
using System.Threading.Tasks;
using SM.Media.Content;
using SM.Media.Playlists;
using SM.Media.Pls;
using SM.Media.Web;

namespace SM.Media.Segments
{
    public delegate Task<ISegmentManager> SegmentManagerFactoryDelegate(Uri source, ContentType contentType, CancellationToken cancellationToken);

    public class SegmentManagerFactories
    {
        readonly PlaylistSegmentManagerFactory _playlistSegmentManagerFactory;
        readonly PlsSegmentManagerFactory _plsSegmentManagerFactory;
        volatile Dictionary<ContentType, SegmentManagerFactoryDelegate> _factories;

        public SegmentManagerFactories(IHttpClients httpClients, IHttpHeaderReader httpHeaderReader, IContentTypeDetector contentTypeDetector, IWebContentTypeDetector webContentTypeDetector, Func<Uri, ICachedWebRequest> webRequestFactory)
        {
            _playlistSegmentManagerFactory = new PlaylistSegmentManagerFactory(httpClients, webContentTypeDetector, webRequestFactory);
            _plsSegmentManagerFactory = new PlsSegmentManagerFactory(httpClients, httpHeaderReader, contentTypeDetector);

            _factories = new Dictionary<ContentType, SegmentManagerFactoryDelegate>
                         {
                             { ContentTypes.Mp3, CreateSimple },
                             { ContentTypes.Aac, CreateSimple },
                             { ContentTypes.TransportStream, CreateSimple },
                             { ContentTypes.M3U8, CreateM3U8Playlist },
                             { ContentTypes.M3U, CreateM3U8Playlist },
                             { ContentTypes.Pls, CreatePlsPlaylist }
                         };
        }

        public SegmentManagerFactoryDelegate GetFactory(ContentType contentType)
        {
            SegmentManagerFactoryDelegate factory;

            if (_factories.TryGetValue(contentType, out factory))
                return factory;

            return null;
        }

        public void Register(ContentType contentType, SegmentManagerFactoryDelegate factory)
        {
            SafeChangeFactories(factories => factories[contentType] = factory);
        }

        public void Deregister(ContentType contentType)
        {
            SafeChangeFactories(factories => factories.Remove(contentType));
        }

        void SafeChangeFactories(Action<Dictionary<ContentType, SegmentManagerFactoryDelegate>> changeAction)
        {
            var oldFactories = _factories;

            for (; ; )
            {
                var newFactories = new Dictionary<ContentType, SegmentManagerFactoryDelegate>(oldFactories);

                changeAction(newFactories);

#pragma warning disable 420
                var factories = Interlocked.CompareExchange(ref _factories, newFactories, oldFactories);
#pragma warning restore 420

                if (oldFactories == factories)
                    return;

                oldFactories = factories;
            }
        }

        static Task<ISegmentManager> CreateSimple(Uri source, ContentType contentType, CancellationToken cancellationToken)
        {
            return TaskEx.FromResult<ISegmentManager>(new SimpleSegmentManager(new[] { source }, contentType));
        }

        Task<ISegmentManager> CreateM3U8Playlist(Uri source, ContentType contentType, CancellationToken cancellationToken)
        {
            return _playlistSegmentManagerFactory.CreatePlaylistSegmentManager(new[] { source }, contentType, cancellationToken);
        }

        Task<ISegmentManager> CreatePlsPlaylist(Uri source, ContentType contentType, CancellationToken cancellationToken)
        {
            return _plsSegmentManagerFactory.CreateSegmentManager(new[] { source }, contentType, cancellationToken);
        }
    }
}

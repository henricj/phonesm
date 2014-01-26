// -----------------------------------------------------------------------
//  <copyright file="SegmentManagerFactory.cs" company="Henric Jungheim">
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
using System.Threading.Tasks;
using SM.Media.Playlists;
using SM.Media.Web;

namespace SM.Media.Segments
{
    public interface ISegmentManagerFactory
    {
        Task<ISegmentManager> CreateAsync(Uri source, string ext);
    }

    public class SegmentManagerFactory : ISegmentManagerFactory
    {
        readonly Dictionary<string, Func<Uri, Task<ISegmentManager>>> _factories =
            new Dictionary<string, Func<Uri, Task<ISegmentManager>>>(StringComparer.OrdinalIgnoreCase)
            {
                { ".mp3", CreateSimple },
                { ".aac", CreateSimple },
                { ".ts", CreateSimple }
            };

        readonly PlaylistSegmentManagerFactory _playlistSegmentManagerFactory;

        public SegmentManagerFactory(PlaylistSegmentManagerFactory playlistSegmentManagerFactory)
        {
            _playlistSegmentManagerFactory = playlistSegmentManagerFactory;

            _factories[".m3u8"] = CreatePlaylist;
            _factories[".m3u"] = CreatePlaylist;
        }

        #region ISegmentManagerFactory Members

        public Task<ISegmentManager> CreateAsync(Uri source, string ext)
        {
            Func<Uri, Task<ISegmentManager>> factory;
            if (null != ext && _factories.TryGetValue(ext, out factory))
                return factory(source);

            return CreatePlaylist(source);
        }

        #endregion

        Task<ISegmentManager> CreatePlaylist(Uri source)
        {
            return _playlistSegmentManagerFactory.CreatePlaylistSegmentManager(source);
        }

        static Task<ISegmentManager> CreateSimple(Uri source)
        {
            return TaskEx.FromResult(new SimpleSegmentManager(new[] { source }) as ISegmentManager);
        }
    }

    public static class SegmentManagerFactoryExtensions
    {
        public static Task<ISegmentManager> CreateDefaultAsync(this ISegmentManagerFactory segmentManagerFactory, Uri source, string defaultType = ".m3u8")
        {
            var ext = source.GetExtension() ?? defaultType;

            return segmentManagerFactory.CreateAsync(source, ext);
        }
    }
}

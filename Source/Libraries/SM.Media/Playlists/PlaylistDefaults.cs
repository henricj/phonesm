// -----------------------------------------------------------------------
//  <copyright file="PlaylistDefaults.cs" company="Henric Jungheim">
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

using System.Linq;
using SM.Media.M3U8;

namespace SM.Media.Playlists
{
    public static class PlaylistDefaults
    {
        /// <summary>
        ///     A playlist is dynamic if it does not have an #EXT-X-ENDLIST tag and every segment has an #EXTINF with a valid
        ///     duration.
        /// </summary>
        /// <param name="parser"></param>
        /// <returns></returns>
        public static bool IsDynamicPlayist(M3U8Parser parser)
        {
            if (null != parser.GlobalTags.Tag(M3U8Tags.ExtXEndList))
                return false;

            var validDuration = parser.Playlist.All(
                p =>
                {
                    var extInf = M3U8Tags.ExtXInf.Find(p.Tags);

                    return null != extInf && extInf.Duration >= 0;
                });

            return validDuration;
        }
    }
}

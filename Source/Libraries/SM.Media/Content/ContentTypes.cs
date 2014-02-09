// -----------------------------------------------------------------------
//  <copyright file="ContentTypes.cs" company="Henric Jungheim">
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

using System.Collections.Generic;

namespace SM.Media.Content
{
    public static class ContentTypes
    {
        public static readonly ContentType Mp3 = new ContentType("MP3", ContentKind.Audio, ".mp3", "audio/mpeg", new[] { "audio/mpeg3", "audio/x-mpeg-3", "audio/x-mp3" });
        public static readonly ContentType Aac = new ContentType("AAC", ContentKind.Audio, ".aac", "audio/aac");
        public static readonly ContentType Ac3 = new ContentType("AC3", ContentKind.Audio, ".ac3", "audio/ac3");
        public static readonly ContentType TransportStream = new ContentType("MPEG-2 Transport Stream", ContentKind.Container, ".ts", "video/MP2T");
        public static readonly ContentType M3U8 = new ContentType("M3U8 Playlist", ContentKind.Playlist, ".m3u8", "application/vnd.apple.mpegurl", new[] { "application/x-mpegURL" });
        public static readonly ContentType M3U = new ContentType("M3U Playlist", ContentKind.Playlist, ".m3u", "application/x-mpegURL");
        public static readonly ContentType Pls = new ContentType("PLS Playlist", ContentKind.Playlist, ".pls", "audio/x-scpls");

        static readonly ContentType[] AllContentTypes = { Mp3, Aac, Ac3, TransportStream, M3U8, M3U, Pls };

        public static IEnumerable<ContentType> AllTypes
        {
            get { return AllContentTypes; }
        }
    }
}

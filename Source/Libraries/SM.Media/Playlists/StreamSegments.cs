// -----------------------------------------------------------------------
//  <copyright file="StreamSegments.cs" company="Henric Jungheim">
//  Copyright (c) 2012, 2013.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012, 2013 Henric Jungheim <software@henric.org>
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
using SM.Media.M3U8;
using SM.Media.M3U8.AttributeSupport;

namespace SM.Media.Playlists
{
    public static class StreamSegments
    {
        public static SubStreamSegment CreateStreamSegment(M3U8Parser parser, M3U8Parser.M3U8Uri uri)
        {
            var url = parser.ResolveUrl(uri.Uri);

            var segment = new SubStreamSegment(url);

            var tags = uri.Tags;

            if (null == tags || 0 == tags.Length)
                return segment;

            var info = M3U8Tags.ExtXInf.Find(tags);

            if (null != info)
                segment.Duration = TimeSpan.FromSeconds((double)info.Duration);

            var extKey = M3U8Tags.ExtXKey.Find(tags);

            if (null != extKey)
            {
                var method = extKey.AttributeObject(ExtKeySupport.AttrMethod);
                var keyUri = extKey.AttributeObject(ExtKeySupport.AttrUri);
                var iv = extKey.AttributeObject(ExtKeySupport.AttrIv);
            } 
            
            return segment;
        }
    }
}

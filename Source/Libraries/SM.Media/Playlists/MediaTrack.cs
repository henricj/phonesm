// -----------------------------------------------------------------------
//  <copyright file="MediaTrack.cs" company="Henric Jungheim">
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SM.Media.Content;

namespace SM.Media.Playlists
{
    public class MediaTrack
    {
        public Uri Url { get; set; }
        public string Title { get; set; }
        public bool UseNativePlayer { get; set; }
        public ContentType ContentType { get; set; }
        public ContentType StreamContentType { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(Title))
            {
                sb.Append('"');
                sb.Append(Title);
                sb.Append('"');
            }

            if (null != Url)
            {
                if (sb.Length > 0)
                    sb.Append(' ');

                sb.Append('<' + Url.OriginalString + '>');
            }

            if (UseNativePlayer)
            {
                if (sb.Length > 0)
                    sb.Append(' ');

                sb.Append("[native]");
            }

            return sb.ToString();
        }
    }

    public static class MediaTrackExtensions
    {
        public static Task<TMediaStreamSource> CreateMediaStreamSourceAsync<TMediaStreamSource>(
            this IMediaStreamFacadeBase<TMediaStreamSource> mediaStreamFacade,
            MediaTrack mediaTrack,
            CancellationToken cancellationToken)
            where TMediaStreamSource : class
        {
            if (null != mediaTrack.ContentType)
                mediaStreamFacade.ContentType = mediaTrack.ContentType;

            if (null != mediaTrack.StreamContentType)
                mediaStreamFacade.StreamContentType = mediaTrack.StreamContentType;

            return mediaStreamFacade.CreateMediaStreamSourceAsync(mediaTrack.Url, cancellationToken);
        }
    }
}

// -----------------------------------------------------------------------
//  <copyright file="StreamMetadata.cs" company="Henric Jungheim">
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
using SM.Media.Content;

namespace SM.Media.Metadata
{
    public interface IStreamMetadata
    {
        Uri Url { get; }
        ContentType ContentType { get; }
        ContentType StreamContentType { get; }

        int? Bitrate { get; }
        TimeSpan? Duration { get; }

        string Name { get; }
        string Description { get; }
        string Genre { get; }

        Uri Website { get; }
    }

    public class StreamMetadata : IStreamMetadata
    {
        #region IStreamMetadata Members

        public Uri Url { get; set; }
        public ContentType ContentType { get; set; }
        public ContentType StreamContentType { get; set; }

        public int? Bitrate { get; set; }
        public TimeSpan? Duration { get; set; }

        public string Name { get; set; }
        public string Description { get; set; }
        public string Genre { get; set; }

        public Uri Website { get; set; }

        #endregion

        public override string ToString()
        {
            var name = string.IsNullOrWhiteSpace(Name) ? "{null}" : '"' + Name + '"';
            var url = null == Url ? "null" : Url.ToString();
            var type = null == ContentType ? "<unknown>" : ContentType.Name;

            return "Stream " + name + " <" + url + "> " + type;
        }
    }
}

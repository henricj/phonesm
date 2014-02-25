// -----------------------------------------------------------------------
//  <copyright file="M3U8Parser.cs" company="Henric Jungheim">
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
using System.Linq;

namespace SM.Media.M3U8
{
    public class M3U8Parser
    {
        readonly List<M3U8TagInstance> _globalTags = new List<M3U8TagInstance>();
        readonly List<M3U8Uri> _playlist = new List<M3U8Uri>();
        readonly List<M3U8TagInstance> _sharedTags = new List<M3U8TagInstance>();
        readonly List<M3U8TagInstance> _tags = new List<M3U8TagInstance>();
        Uri _baseUrl;

        public IEnumerable<M3U8TagInstance> GlobalTags
        {
            get { return _globalTags; }
        }

        public IEnumerable<M3U8Uri> Playlist
        {
            get { return _playlist; }
        }

        public Uri BaseUrl
        {
            get { return _baseUrl; }
        }

        /// <summary>
        ///     Resolve a possibly relative Url.
        /// </summary>
        /// <param name="url">Absolute Uri or Url relative to this playlist.</param>
        /// <returns>An absolute Uri</returns>
        public Uri ResolveUrl(Uri url)
        {
            if (url.IsAbsoluteUri)
                return url;

            return new Uri(_baseUrl, url);
        }

        /// <summary>
        ///     http://tools.ietf.org/html/draft-pantos-http-live-streaming-12
        /// </summary>
        /// <param name="baseUri"></param>
        /// <param name="lines"> </param>
        public void Parse(Uri baseUri, IEnumerable<string> lines)
        {
            _baseUrl = baseUri;

            _playlist.Clear();

            var first = true;

            foreach (var line in lines)
            {
                if (first)
                {
                    first = false;

                    if (line != "#EXTM3U")
                    {
                        var d = lines as IDisposable;

                        if (null != d)
                            d.Dispose();

                        return;
                    }

                    continue;
                }

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (line.StartsWith("#EXT"))
                    HandleExt(line);
                else if (!line.StartsWith("#"))
                {
                    var uri = new M3U8Uri
                              {
                                  Uri = line
                              };

                    if (_tags.Count > 0 || _sharedTags.Count > 0)
                    {
                        uri.Tags = _tags.Union(_sharedTags).ToArray();

                        _tags.Clear();
                    }

                    _playlist.Add(uri);
                }
            }
        }

        void HandleExt(string line)
        {
            var extIndex = line.IndexOf(':');

            var tag = line;
            var value = null as string;

            if (extIndex > 3)
            {
                tag = line.Substring(0, extIndex);
                if (extIndex + 1 < line.Length)
                    value = line.Substring(extIndex + 1);
            }

            var tagInstance = M3U8Tags.Default.Create(tag, value);

            if (null == tagInstance)
                return;

            switch (tagInstance.Tag.Scope)
            {
                case M3U8TagScope.Global:
                    _globalTags.Add(tagInstance);
                    break;
                case M3U8TagScope.Shared:
                    ResolveShared(tagInstance);
                    break;
                case M3U8TagScope.Segment:
                    _tags.Add(tagInstance);
                    break;
            }
        }

        void ResolveShared(M3U8TagInstance tagInstance)
        {
            // TODO: Shared tags needs a conflict/update policy (e.g., a new key of the same type should remove the old one)
            _sharedTags.Add(tagInstance);
        }

        #region Nested type: M3U8Uri

        public class M3U8Uri
        {
            public M3U8TagInstance[] Tags;
            public string Uri;

            public override string ToString()
            {
                return Uri;
            }
        }

        #endregion
    }
}

// -----------------------------------------------------------------------
//  <copyright file="ShoutcastHeaders.cs" company="Henric Jungheim">
//  Copyright (c) 2012-2015.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012-2015 Henric Jungheim <software@henric.org>
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

namespace SM.Media.Metadata
{
    class ShoutcastHeaders
    {
        readonly int? _bitrate;
        readonly string _description;
        readonly string _genre;
        readonly int? _metaInterval;
        readonly string _name;
        readonly bool _supportsIcyMetadata;
        readonly Uri _website;

        public ShoutcastHeaders(Uri streamUrl, IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
        {
            foreach (var header in headers)
            {
                switch (header.Key.ToLowerInvariant())
                {
                    case "icy-br":
                        foreach (var br in header.Value)
                        {
                            int bitrate;
                            if (int.TryParse(br, out bitrate))
                            {
                                if (bitrate > 0)
                                {
                                    _bitrate = bitrate * 1000;
                                    break;
                                }
                            }
                        }
                        break;
                    case "icy-description":
                        _description = header.Value.FirstOrDefault();
                        break;
                    case "icy-genre":
                        _genre = header.Value.FirstOrDefault();
                        break;
                    case "icy-metadata":
                        _supportsIcyMetadata = true;
                        break;
                    case "icy-metaint":
                        foreach (var metaint in header.Value)
                        {
                            int interval;
                            if (int.TryParse(metaint, out interval))
                            {
                                if (interval > 0)
                                {
                                    _metaInterval = interval;
                                    break;
                                }
                            }
                        }
                        break;
                    case "icy-name":
                        _name = header.Value.FirstOrDefault();
                        break;
                    case "icy-url":
                        foreach (var site in header.Value)
                        {
                            Uri url;
                            if (Uri.TryCreate(streamUrl, site, out url))
                            {
                                _website = url;
                                break;
                            }
                        }
                        break;
                }
            }
        }

        public int? Bitrate
        {
            get { return _bitrate; }
        }

        public string Description
        {
            get { return _description; }
        }

        public string Genre
        {
            get { return _genre; }
        }

        public int? MetaInterval
        {
            get { return _metaInterval; }
        }

        public string Name
        {
            get { return _name; }
        }

        public bool SupportsIcyMetadata
        {
            get { return _supportsIcyMetadata; }
        }

        public Uri Website
        {
            get { return _website; }
        }
    }
}

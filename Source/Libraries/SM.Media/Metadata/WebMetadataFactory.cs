// -----------------------------------------------------------------------
//  <copyright file="WebMetadataFactory.cs" company="Henric Jungheim">
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
using System.Linq;
using SM.Media.Web;

namespace SM.Media.Metadata
{
    public interface IWebMetadataFactory
    {
        IStreamMetadata CreateStreamMetadata(WebResponse webResponse);
        ISegmentMetadata CreateSegmentMetadata(WebResponse webResponse);
    }

    public class WebMetadataFactory : IWebMetadataFactory
    {
        #region IWebMetadataFactory Members

        public IStreamMetadata CreateStreamMetadata(WebResponse webResponse)
        {
            var streamMetadata = new ShoutcastStreamMetadata
            {
                Url = webResponse.RequestUri,
                ContentType = webResponse.ContentType
            };

            foreach (var header in webResponse.Headers)
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
                                    streamMetadata.Bitrate = bitrate * 1000;
                                    break;
                                }
                            }
                        }
                        break;
                    case "icy-description":
                        streamMetadata.Description = header.Value.FirstOrDefault();
                        break;
                    case "icy-genre":
                        streamMetadata.Genre = header.Value.FirstOrDefault();
                        break;
                    case "icy-metadata":
                        streamMetadata.SupportsIcyMetadata = true;
                        break;
                    case "icy-metaint":
                        foreach (var metaint in header.Value)
                        {
                            int interval;
                            if (int.TryParse(metaint, out interval))
                            {
                                if (interval > 0)
                                {
                                    streamMetadata.IcyMetaInt = interval;
                                    break;
                                }
                            }
                        }
                        break;
                    case "icy-name":
                        streamMetadata.Name = header.Value.FirstOrDefault();
                        break;
                    case "icy-url":
                        foreach (var site in header.Value)
                        {
                            Uri url;
                            if (Uri.TryCreate(streamMetadata.Url, site, out url))
                            {
                                streamMetadata.Website = url;
                                break;
                            }
                        }
                        break;
                }
            }

            return streamMetadata;
        }

        public ISegmentMetadata CreateSegmentMetadata(WebResponse webResponse)
        {
            var segmentMetadata = new SegmentMetadata
            {
                Url = webResponse.RequestUri,
                ContentType = webResponse.ContentType,
                Length = webResponse.ContentLength
            };

            return segmentMetadata;
        }

        #endregion
    }
}

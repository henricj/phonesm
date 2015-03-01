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

using SM.Media.Content;
using SM.Media.Web;

namespace SM.Media.Metadata
{
    public interface IWebMetadataFactory
    {
        IStreamMetadata CreateStreamMetadata(WebResponse webResponse, ContentType contentType = null);
        ISegmentMetadata CreateSegmentMetadata(WebResponse webResponse, ContentType contentType = null);
    }

    public class WebMetadataFactory : IWebMetadataFactory
    {
        #region IWebMetadataFactory Members

        public IStreamMetadata CreateStreamMetadata(WebResponse webResponse, ContentType contentType = null)
        {
            var shoutcast = new ShoutcastHeaders(webResponse.RequestUri, webResponse.Headers);

            var streamMetadata = new StreamMetadata
            {
                Url = webResponse.RequestUri,
                ContentType = contentType ?? webResponse.ContentType,
                Bitrate = shoutcast.Bitrate,
                Description = shoutcast.Description,
                Genre = shoutcast.Genre,
                Name = shoutcast.Name,
                Website = shoutcast.Website
            };

            return streamMetadata;
        }

        public ISegmentMetadata CreateSegmentMetadata(WebResponse webResponse, ContentType contentType)
        {
            var shoutcast = new ShoutcastHeaders(webResponse.RequestUri, webResponse.Headers);

            if (shoutcast.MetaInterval > 0 || shoutcast.SupportsIcyMetadata)
            {
                var segmentMetadata = new ShoutcastSegmentMetadata
                {
                    Url = webResponse.RequestUri,
                    ContentType = contentType ?? webResponse.ContentType,
                    Length = webResponse.ContentLength,
                    IcyMetaInt = shoutcast.MetaInterval,
                    SupportsIcyMetadata = shoutcast.SupportsIcyMetadata
                };

                return segmentMetadata;
            }

            var streamMetadata = new SegmentMetadata
            {
                Url = webResponse.RequestUri,
                ContentType = contentType ?? webResponse.ContentType,
                Length = webResponse.ContentLength
            };

            return streamMetadata;
        }

        #endregion
    }
}

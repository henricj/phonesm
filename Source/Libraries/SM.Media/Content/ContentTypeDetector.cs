// -----------------------------------------------------------------------
//  <copyright file="ContentTypeDetector.cs" company="Henric Jungheim">
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
using System.Net.Http.Headers;
using SM.Media.Web;

namespace SM.Media.Content
{
    public interface IContentTypeDetector
    {
        ICollection<ContentType> GetContentType(Uri url, string mimeType = null);
    }

    public class ContentTypeDetector : IContentTypeDetector
    {
        protected static readonly ContentType[] NoContent = new ContentType[0];
        protected readonly ContentType[] ContentTypes;
        protected readonly ILookup<string, ContentType> ExtensionLookup;
        protected readonly ILookup<string, ContentType> MimeLookup;

        public ContentTypeDetector(IEnumerable<ContentType> contentTypes)
        {
            if (null == contentTypes)
                throw new ArgumentNullException("contentTypes");

            ContentTypes = contentTypes.ToArray();

            ExtensionLookup = ContentTypes
                .SelectMany(ct => ct.FileExts, (ct, ext) => new
                                                            {
                                                                ext,
                                                                ContentType = ct
                                                            })
                .ToLookup(arg => arg.ext, x => x.ContentType, StringComparer.OrdinalIgnoreCase);

            var mimeTypes = ContentTypes
                .Select(ct => new
                              {
                                  ct.MimeType,
                                  ContentType = ct
                              });

            var alternateMimeTypes = ContentTypes
                .Where(ct => null != ct.AlternateMimeTypes)
                .SelectMany(ct => ct.AlternateMimeTypes, (ct, mime) => new
                                                                       {
                                                                           MimeType = mime,
                                                                           ContentType = ct
                                                                       });

            MimeLookup = alternateMimeTypes
                .Union(mimeTypes)
                .ToLookup(arg => arg.MimeType, x => x.ContentType, StringComparer.OrdinalIgnoreCase);
        }

        #region IContentTypeDetector Members

        public virtual ICollection<ContentType> GetContentType(Uri url, string mimeType = null)
        {
            var contentType = GetContentTypeByUrl(url);

            if (null != contentType && contentType.Any())
                return contentType;

            if (null == mimeType)
                return NoContent;

            return GetContentTypeByContentHeaders(mimeType) ?? NoContent;
        }

        #endregion

        protected virtual ICollection<ContentType> GetContentTypeByUrl(Uri url)
        {
            var ext = url.GetExtension();

            if (null == ext)
                return null;

            return ExtensionLookup[ext].ToArray();
        }

        protected virtual ICollection<ContentType> GetContentTypeByContentHeaders(string mimeType)
        {
            if (null == mimeType)
                return null;

            return MimeLookup[mimeType].ToArray();
        }
    }

    public static class ContentTypeDetectorExtensions
    {
        public static ICollection<ContentType> GetContentType(this IContentTypeDetector contentTypeDetector, Uri url, HttpContentHeaders headers)
        {
            var mimeType = default(string);

            var contentTypeHeader = headers.ContentType;
            if (null != contentTypeHeader)
                mimeType = contentTypeHeader.MediaType;

            return contentTypeDetector.GetContentType(url, mimeType);
        }
    }
}

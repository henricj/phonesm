// -----------------------------------------------------------------------
//  <copyright file="ContentTypeDetector.cs" company="Henric Jungheim">
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
using System.Collections.Generic;
using System.Linq;
using SM.Media.Web;

namespace SM.Media.Content
{
    public interface IContentTypeDetector
    {
        ICollection<ContentType> GetContentType(Uri url, ContentKind kind, string mimeType = null, string fileName = null);
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
                throw new ArgumentNullException(nameof(contentTypes));

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

        public virtual ICollection<ContentType> GetContentType(Uri url, ContentKind kind, string mimeType = null, string fileName = null)
        {
            if (null == url)
                throw new ArgumentNullException(nameof(url));

            ICollection<ContentType> contentTypes;

            if (null != mimeType)
            {
                contentTypes = GetContentTypeByContentHeaders(mimeType, kind);

                if (null != contentTypes)
                    return contentTypes;
            }

            contentTypes = GetContentTypeByUrl(url, kind);

            if (null != contentTypes && contentTypes.Any())
                return contentTypes;

            if (string.IsNullOrWhiteSpace(fileName))
                return NoContent;

            contentTypes = GetContentTypeByFileName(fileName, kind);

            return contentTypes ?? NoContent;
        }

        #endregion

        protected virtual IEnumerable<ContentType> FilterByKind(IEnumerable<ContentType> types, ContentKind requiredKind)
        {
            return types.Where(type => requiredKind.IsCompatible(type.Kind));
        }

        protected virtual ICollection<ContentType> GetContentTypeByUrl(Uri url, ContentKind requiredKind)
        {
            var ext = url.GetExtension();

            if (null == ext)
                return null;

            return FilterByKind(ExtensionLookup[ext], requiredKind).ToArray();
        }

        protected virtual ICollection<ContentType> GetContentTypeByContentHeaders(string mimeType, ContentKind requiredKind)
        {
            if (null == mimeType)
                return null;

            return FilterByKind(MimeLookup[mimeType], requiredKind).ToArray();
        }

        protected virtual ICollection<ContentType> GetContentTypeByFileName(string filename, ContentKind requiredKind)
        {
            if (string.IsNullOrWhiteSpace(filename))
                return null;

            var ext = UriExtensions.GetExtension(filename);

            return FilterByKind(ExtensionLookup[ext], requiredKind).ToArray();
        }
    }
}

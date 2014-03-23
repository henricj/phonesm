// -----------------------------------------------------------------------
//  <copyright file="ContentType.cs" company="Henric Jungheim">
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

namespace SM.Media.Content
{
    public class ContentType : IEquatable<ContentType>
    {
        readonly ICollection<string> _alternateMimeTypes;
        readonly ICollection<string> _fileExts;
        readonly ContentKind _kind;
        readonly string _mimeType;
        readonly string _name;

        public ContentType(string name, ContentKind kind, string mimeType, string fileExt, IEnumerable<string> alternateMimeTypes = null)
            : this(name, kind, mimeType, new[] { fileExt }, alternateMimeTypes)
        { }

        public ContentType(string name, ContentKind kind, string mimeType, IEnumerable<string> fileExts, IEnumerable<string> alternateMimeTypes = null)
        {
            if (null == name)
                throw new ArgumentNullException("name");
            if (mimeType == null)
                throw new ArgumentNullException("mimeType");
            if (null == fileExts)
                throw new ArgumentNullException("fileExts");

            _name = name;
            _kind = kind;
            _mimeType = mimeType;
            _alternateMimeTypes = null == alternateMimeTypes ? new List<string>() : alternateMimeTypes.ToList();
            _fileExts = fileExts.ToList();
        }

        public string Name
        {
            get { return _name; }
        }

        public ICollection<string> AlternateMimeTypes
        {
            get { return _alternateMimeTypes; }
        }

        public ICollection<string> FileExts
        {
            get { return _fileExts; }
        }

        public ContentKind Kind
        {
            get { return _kind; }
        }

        public string MimeType
        {
            get { return _mimeType; }
        }

        #region IEquatable<ContentType> Members

        public bool Equals(ContentType other)
        {
            if (ReferenceEquals(this, other))
                return true;

            if (ReferenceEquals(null, other))
                return false;

            return string.Equals(_mimeType, other._mimeType, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(_mimeType);
        }

        public override bool Equals(object obj)
        {
            var other = obj as ContentType;

            if (ReferenceEquals(null, other))
                return false;

            return Equals(other);
        }

        public static bool operator ==(ContentType a, ContentType b)
        {
            if (ReferenceEquals(a, b))
                return true;

            if (ReferenceEquals(a, null))
                return false;

            return a.Equals(b);
        }

        public static bool operator !=(ContentType a, ContentType b)
        {
            return !(a == b);
        }

        public override string ToString()
        {
            return string.Format("{0} ({1})", _name, _mimeType);
        }
    }
}

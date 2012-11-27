// -----------------------------------------------------------------------
//  <copyright file="M3U8Attribute.cs" company="Henric Jungheim">
//  Copyright (c) 2012.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012 Henric Jungheim <software@henric.org>
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

namespace SM.Media.M3U8
{
    public class M3U8Attribute : IEquatable<M3U8Attribute>
    {
        public readonly Func<M3U8Attribute, string, M3U8AttributeInstance> CreateInstance;
        public readonly bool IsRequired;
        public readonly string Name;

        public M3U8Attribute(string name, bool isRequired, Func<M3U8Attribute, string, M3U8AttributeInstance> createInstance)
        {
            Name = name;
            IsRequired = isRequired;
            CreateInstance = createInstance;
        }

        #region IEquatable<M3U8Attribute> Members

        public bool Equals(M3U8Attribute other)
        {
            if (ReferenceEquals(this, other))
                return true;

            if (null == other)
                return false;

            return Name == other.Name;
        }

        #endregion

        public override bool Equals(object obj)
        {
            var other = obj as M3U8Tag;

            if (null == other)
                return false;

            return Equals(other);
        }

        public static bool operator ==(M3U8Attribute x, M3U8Attribute y)
        {
            if (ReferenceEquals(x, y))
                return true;

            if (null == x || null == y)
                return false;

            return x.Equals(y);
        }

        public static bool operator !=(M3U8Attribute x, M3U8Attribute y)
        {
            return !(x == y);
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        public override string ToString()
        {
            return Name;
        }
    }
}

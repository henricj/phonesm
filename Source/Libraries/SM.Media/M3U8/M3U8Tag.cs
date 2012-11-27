// -----------------------------------------------------------------------
//  <copyright file="M3U8Tag.cs" company="Henric Jungheim">
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
    public enum M3U8TagScope
    {
        Invalid = 0,
        Global = 1,
        Shared = 2,
        Segment = 3
    }

    public class M3U8Tag : IEquatable<M3U8Tag>
    {
        public readonly Func<M3U8Tag, string, M3U8TagInstance> CreateInstance;
        public readonly string Name;
        public readonly M3U8TagScope Scope;

        public M3U8Tag(string name, M3U8TagScope scope, Func<M3U8Tag, string, M3U8TagInstance> createInstance)
        {
            Name = name;
            Scope = scope;
            CreateInstance = createInstance;
        }

        #region IEquatable<M3U8Tag> Members

        public bool Equals(M3U8Tag other)
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

        public static bool operator ==(M3U8Tag x, M3U8Tag y)
        {
            if (ReferenceEquals(x, y))
                return true;

            if (null == x || null == y)
                return false;

            return x.Equals(y);
        }

        public static bool operator !=(M3U8Tag x, M3U8Tag y)
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

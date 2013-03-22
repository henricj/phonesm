// -----------------------------------------------------------------------
//  <copyright file="ByterangeAttributeInstance.cs" company="Henric Jungheim">
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

using System.Globalization;

namespace SM.Media.M3U8.AttributeSupport
{
    sealed class ByterangeAttributeInstance : M3U8AttributeInstance
    {
        public readonly long Length;
        public readonly long? Offset;

        public ByterangeAttributeInstance(M3U8Attribute attribute, long length, long? offset)
            : base(attribute)
        {
            Length = length;
            Offset = offset;
        }

        public static M3U8AttributeInstance Create(M3U8Attribute attribute, string value)
        {
            // TODO: Consolidate code between ByterangeAttributeInstance and ByterangeTagInstance

            var index = value.IndexOf('@');

            if (index < 0 || index + 1 >= value.Length)
                return new ByterangeAttributeInstance(attribute, long.Parse(value, CultureInfo.InvariantCulture), null);

            var length = long.Parse(value.Substring(0, index), CultureInfo.InvariantCulture);
            var offset = long.Parse(value.Substring(index + 1), CultureInfo.InvariantCulture);

            return new ByterangeAttributeInstance(attribute, length, offset);
        }

        public override string ToString()
        {
            // TODO: Consolidate code between ByterangeAttributeInstance and ByterangeTagInstance

            if (Offset.HasValue)
                return string.Format(CultureInfo.InvariantCulture, "{0}={1}@{2}", Attribute, Length, Offset.Value);

            return string.Format(CultureInfo.InvariantCulture, "{0}={1}", Attribute, Length);
        }
    }
}

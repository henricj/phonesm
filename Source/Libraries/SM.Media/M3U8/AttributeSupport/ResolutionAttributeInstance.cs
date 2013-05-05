// -----------------------------------------------------------------------
//  <copyright file="ResolutionAttributeInstance.cs" company="Henric Jungheim">
//  Copyright (c) 2012, 2013.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012, 2013 Henric Jungheim <software@henric.org>
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
    sealed class ResolutionAttributeInstance : M3U8AttributeInstance
    {
        static readonly char[] ResolutionSeparator = { 'x', 'X' };
        public readonly int X;
        public readonly int Y;

        public ResolutionAttributeInstance(M3U8Attribute attribute, int x, int y)
            : base(attribute)
        {
            X = x;
            Y = y;
        }

        public static M3U8AttributeInstance Create(M3U8Attribute attribute, string value)
        {
            var index = value.IndexOfAny(ResolutionSeparator);

            if (index < 1 || index + 1 >= value.Length)
                return null;

            var x = int.Parse(value.Substring(0, index), CultureInfo.InvariantCulture);
            var y = int.Parse(value.Substring(index + 1), CultureInfo.InvariantCulture);

            return new ResolutionAttributeInstance(attribute, x, y);
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}={1}x{2}", Attribute.Name, X, Y);
        }
    }
}

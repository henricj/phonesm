// -----------------------------------------------------------------------
//  <copyright file="ExtMapSupport.cs" company="Henric Jungheim">
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

using System.Collections.Generic;
using System.Linq;

namespace SM.Media.M3U8.AttributeSupport
{
    static class ExtMapSupport
    {
        public static readonly M3U8ValueAttribute<string> AttrUri = new M3U8ValueAttribute<string>("URI", true, M3U8AttributeSupport.QuotedStringParser);
        public static readonly M3U8Attribute AttrByterange = new M3U8Attribute("BYTERANGE", false, ByterangeAttributeInstance.Create);

        internal static readonly IDictionary<string, M3U8Attribute> Attributes =
            (new[]
             {
                 AttrUri,
                 AttrByterange
             }
            ).ToDictionary(a => a.Name);
    }
}

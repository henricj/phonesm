// -----------------------------------------------------------------------
//  <copyright file="M3U8AttributeParserSupport.cs" company="Henric Jungheim">
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
using System.Text;

namespace SM.Media.M3U8.AttributeSupport
{
    static class M3U8AttributeParserSupport
    {
        static readonly M3U8AttributeInstance[] NoAttributes = new M3U8AttributeInstance[0];
        static readonly char[] PostEqualsChars = { ',', '"' };

        internal static IEnumerable<M3U8AttributeInstance> ParseAttributes(string value, IDictionary<string, M3U8Attribute> attributes)
        {
            if (string.IsNullOrWhiteSpace(value))
                return NoAttributes;

            var lastIndex = 0;
            var index = 0;
            var sb = new StringBuilder();
            var attributeInstances = new List<M3U8AttributeInstance>();

            while (index < value.Length)
            {
                index = value.IndexOf('=', lastIndex);

                if (index < 0)
                    index = value.Length;

                sb.Length = 0;

                for (var i = lastIndex; i < index; ++i)
                {
                    var c = value[i];

                    // See section 3.2 ofthe RFC
                    // http://tools.ietf.org/html/draft-pantos-http-live-streaming-12#section-3.2
                    if ((c >= 'A' && c <= 'Z') || '-' == c)
                        sb.Append(c);
                }

                var attributeName = sb.ToString();

                // Skip the "=".
                lastIndex = index + 1;

                index = value.IndexOfAny(PostEqualsChars, lastIndex);

                if (index < 0)
                    index = value.Length;

                if (index < value.Length && '"' == value[index])
                {
                    // Find the closing quote.
                    index = value.IndexOf('"', index + 1);

                    if (index < 0)
                    {
                        // Unterminated string.
                        break;
                    }

                    // Find the comma.

                    index = value.IndexOf(',', index + 1);

                    if (index < 0)
                        index = value.Length;
                }

                if (index <= lastIndex)
                    break;

                var attributeValue = value.Substring(lastIndex, index - lastIndex);

                lastIndex = Math.Min(index + 1, value.Length);

                M3U8Attribute attribute;
                if (attributes.TryGetValue(attributeName, out attribute))
                {
                    var attributeInstance = attribute.CreateInstance(attribute, attributeValue);

                    if (null != attributeInstance)
                        attributeInstances.Add(attributeInstance);
                }
            }

            if (attributeInstances.Count < 1)
                return NoAttributes;

            return attributeInstances;
        }
    }
}

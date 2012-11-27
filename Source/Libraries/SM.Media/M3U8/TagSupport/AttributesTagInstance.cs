// -----------------------------------------------------------------------
//  <copyright file="AttributesTagInstance.cs" company="Henric Jungheim">
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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SM.Media.M3U8.AttributeSupport;

namespace SM.Media.M3U8.TagSupport
{
    public class AttributesTagInstance : M3U8TagInstance
    {
        internal AttributesTagInstance(M3U8Tag tag, IEnumerable<M3U8AttributeInstance> attributes)
            : base(tag)
        {
            Attributes = attributes;
        }

        public IEnumerable<M3U8AttributeInstance> Attributes { get; private set; }

        public static M3U8TagInstance Create(M3U8Tag tag, string value, IDictionary<string, M3U8Attribute> attributes)
        {
            return Create(tag, value, v => M3U8AttributeParserSupport.ParseAttributes(v, attributes));
        }

        static M3U8TagInstance Create(M3U8Tag tag, string value, Func<string, IEnumerable<M3U8AttributeInstance>> attributeParser)
        {
            return new AttributesTagInstance(tag, ParseAttributes(value, attributeParser));
        }

        protected static IEnumerable<M3U8AttributeInstance> ParseAttributes(string value, Func<string, IEnumerable<M3U8AttributeInstance>> attributeParser)
        {
            IEnumerable<M3U8AttributeInstance> attributes = null;

            if (!String.IsNullOrWhiteSpace(value))
            {
                if (null != attributeParser)
                    attributes = attributeParser(value);
            }
            return attributes;
        }

        protected static IEnumerable<M3U8AttributeInstance> ParseAttributes(string value, IDictionary<string, M3U8Attribute> attributes)
        {
            return ParseAttributes(value, v => M3U8AttributeParserSupport.ParseAttributes(v, attributes));
        }

        public override string ToString()
        {
            var attributes = Attributes;

            if (null == attributes || !attributes.Any())
                return base.ToString();

            var sb = new StringBuilder(Tag.Name);

            sb.Append(':');

            var first = true;

            foreach (var attribute in attributes)
            {
                if (first)
                    first = false;
                else
                    sb.Append(',');

                sb.Append(attribute);
            }

            return sb.ToString();
        }
    }
}

// -----------------------------------------------------------------------
//  <copyright file="M3U8TagInstanceExtensions.cs" company="Henric Jungheim">
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

using System;
using System.Collections.Generic;
using System.Linq;
using SM.Media.M3U8.AttributeSupport;
using SM.Media.M3U8.TagSupport;

namespace SM.Media.M3U8
{
    public static class M3U8TagInstanceExtensions
    {
        static readonly IEnumerable<M3U8AttributeInstance> NoAttributeInstances = new M3U8AttributeInstance[0];

        public static IEnumerable<M3U8AttributeInstance> Attributes(this M3U8TagInstance tagInstance)
        {
            var attributesTagInstance = tagInstance as AttributesTagInstance;

            if (null == attributesTagInstance)
                return NoAttributeInstances;

            return attributesTagInstance.Attributes;
        }

        public static IEnumerable<M3U8AttributeInstance> Attributes(this M3U8TagInstance tagInstance, M3U8Attribute attribute)
        {
            return tagInstance.Attributes()
                              .Where(a => a.Attribute == attribute);
        }

        public static IEnumerable<M3U8AttributeValueInstance<TValue>> Attributes<TValue>(this M3U8TagInstance tagInstance, M3U8ValueAttribute<TValue> attribute)
        {
            return tagInstance.Attributes()
                              .OfType<M3U8AttributeValueInstance<TValue>>()
                              .Where(a => a.Attribute == attribute);
        }

        public static M3U8AttributeValueInstance<TValue> Attribute<TValue>(this M3U8TagInstance tagInstance, M3U8ValueAttribute<TValue> attribute)
        {
            return tagInstance.Attributes()
                              .OfType<M3U8AttributeValueInstance<TValue>>()
                              .FirstOrDefault(a => a.Attribute == attribute);
        }

        public static M3U8AttributeValueInstance<TValue> Attribute<TValue>(this M3U8TagInstance tagInstance, M3U8ValueAttribute<TValue> attribute, TValue value)
            where TValue : IEquatable<TValue>
        {
            return tagInstance.Attributes()
                              .OfType<M3U8AttributeValueInstance<TValue>>()
                              .FirstOrDefault(a => a.Attribute == attribute && a.Value.Equals(value));
        }

        public static TValue? AttributeValue<TValue>(this M3U8TagInstance tagInstance, M3U8ValueAttribute<TValue> attribute)
            where TValue : struct
        {
            var attributeInstance = tagInstance.Attribute(attribute);

            if (null == attributeInstance)
                return null;

            return attributeInstance.Value;
        }

        public static TValue AttributeObject<TValue>(this M3U8TagInstance tagInstance, M3U8ValueAttribute<TValue> attribute)
            where TValue : class
        {
            var attributeInstance = tagInstance.Attribute(attribute);

            if (null == attributeInstance)
                return null;

            return attributeInstance.Value;
        }

        public static M3U8TagInstance Tag(this IEnumerable<M3U8TagInstance> tags, M3U8Tag tag)
        {
            return tags.FirstOrDefault(t => t.Tag == tag);
        }

        public static TTagInstance Tag<TTag, TTagInstance>(this IEnumerable<M3U8TagInstance> tags, TTag tag)
            where TTag : M3U8Tag
            where TTagInstance : M3U8TagInstance
        {
            return tags.OfType<TTagInstance>()
                       .FirstOrDefault(t => t.Tag == tag);
        }

        public static IEnumerable<M3U8TagInstance> Tags(this IEnumerable<M3U8TagInstance> tags, M3U8Tag tag)
        {
            return tags.Where(t => t.Tag == tag);
        }

        public static IEnumerable<TTagInstance> Tags<TTag, TTagInstance>(this IEnumerable<M3U8TagInstance> tags, TTag tag)
            where TTag : M3U8Tag
            where TTagInstance : M3U8TagInstance
        {
            return tags.OfType<TTagInstance>()
                       .Where(t => t.Tag == tag);
        }
    }
}

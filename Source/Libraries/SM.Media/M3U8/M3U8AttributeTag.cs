//-----------------------------------------------------------------------
// <copyright file="M3U8AttributeTag.cs" company="Henric Jungheim">
// Copyright (c) 2012.
// <author>Henric Jungheim</author>
// </copyright>
//-----------------------------------------------------------------------
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
using System.Threading;

namespace SM.Media.M3U8
{
    public class M3U8AttributeTag : M3U8Tag
    {
        volatile IDictionary<string, M3U8Attribute> _attributes;

        public M3U8AttributeTag(string name, M3U8TagScope scope, IDictionary<string, M3U8Attribute> attributes, Func<M3U8Tag, string, M3U8TagInstance> createInstance)
            : base(name, scope, createInstance)
        {
            _attributes = attributes;
        }

        public virtual IDictionary<string, M3U8Attribute> Attributes
        {
            get { return _attributes; }
        }

        public void Register(M3U8Attribute attribute)
        {
            var oldAttributes = _attributes;

            for (;;)
            {
                var attributes = new Dictionary<string, M3U8Attribute>(oldAttributes);

                attributes[attribute.Name] = attribute;

#pragma warning disable 0420
                var currentAttributes = Interlocked.CompareExchange(ref _attributes, attributes, oldAttributes);
#pragma warning restore 0420

                if (currentAttributes == oldAttributes)
                    return;

                oldAttributes = currentAttributes;
            }
        }
    }
}

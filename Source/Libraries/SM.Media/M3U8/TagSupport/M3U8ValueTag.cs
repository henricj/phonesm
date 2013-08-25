// -----------------------------------------------------------------------
//  <copyright file="M3U8ValueTag.cs" company="Henric Jungheim">
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

namespace SM.Media.M3U8.TagSupport
{
    public class M3U8ValueTag : M3U8Tag
    {
        public M3U8ValueTag(string name, M3U8TagScope scope, Func<M3U8Tag, string, ValueTagInstance> createInstance)
            : base(name, scope, (tag, value) => createInstance(tag, value))
        { }

        public ValueTagInstance Find(IEnumerable<M3U8TagInstance> tags)
        {
            if (null == tags)
                return null;

            return tags.Tag<M3U8ValueTag, ValueTagInstance>(this);
        }

        public T? GetValue<T>(IEnumerable<M3U8TagInstance> tags)
            where T : struct
        {
            var tag = Find(tags);

            if (null == tag)
                return null;

            return (T)tag.Value;
        }

        public T GetObject<T>(IEnumerable<M3U8TagInstance> tags)
            where T : class
        {
            var tag = Find(tags);

            if (null == tag)
                return null;

            return (T)tag.Value;
        }
    }
}

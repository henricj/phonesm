//-----------------------------------------------------------------------
// <copyright file="ExtinfTagInstance.cs" company="Henric Jungheim">
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
using System.Globalization;

namespace SM.Media.M3U8.M38UTags
{
    public sealed class ExtinfTagInstance : M3U8TagInstance
    {
        public ExtinfTagInstance(M3U8Tag tag, decimal duration, string title = null)
            : base(tag)
        {
            Duration = duration;
            Title = title ?? String.Empty;
        }

        public decimal Duration { get; private set; }
        public string Title { get; private set; }

        internal static ExtinfTagInstance Create(M3U8Tag tag, string value)
        {
            var index = value.IndexOf(',');

            if (index < 0)
                return new ExtinfTagInstance(tag, Decimal.Parse(value));

            var duration = Decimal.Parse(value.Substring(0, index));

            var title = string.Empty;

            if (index + 1 < value.Length)
                title = value.Substring(index);

            return new ExtinfTagInstance(tag, duration, title);
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}:{1},{2}", Tag, Duration, Title);
        }
    }
}

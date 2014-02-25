// -----------------------------------------------------------------------
//  <copyright file="DateTimeTagInstance.cs" company="Henric Jungheim">
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
using System.Diagnostics;
using System.Globalization;

namespace SM.Media.M3U8.TagSupport
{
    public class DateTimeTagInstance : M3U8TagInstance
    {
        public DateTimeTagInstance(M3U8Tag tag, DateTimeOffset dateTime)
            : base(tag)
        {
            DateTime = dateTime;
        }

        public DateTimeOffset DateTime { get; private set; }

        internal static M3U8TagInstance Create(M3U8Tag tag, string value)
        {
            DateTimeOffset dateTimeOffset;

            if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out dateTimeOffset))
            {
                Debug.WriteLine("*** unable to parse date/time: " + value);

                return null;
            }

            return new DateTimeTagInstance(tag, dateTimeOffset);
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}:{1:o}", Tag, DateTime);
        }
    }
}

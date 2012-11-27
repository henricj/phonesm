// -----------------------------------------------------------------------
//  <copyright file="CsvStringsAttributeInstance.cs" company="Henric Jungheim">
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
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace SM.Media.M3U8.AttributeSupport
{
    class CsvStringsAttributeInstance : M3U8AttributeValueInstance<IEnumerable<string>>
    {
        public CsvStringsAttributeInstance(M3U8Attribute attribute, IEnumerable<string> codecs)
            : base(attribute, codecs)
        { }

        public override string ToString()
        {
            var values = Value;

            if (null == values)
            {
                // TODO: Complain.
                Debug.Assert(false);
                return string.Format(CultureInfo.InvariantCulture, "{0}=\"{1}\"", Attribute.Name, Value);
            }

            var sb = new StringBuilder(Attribute.Name);

            sb.Append("=\"");

            var first = true;
            foreach (var v in values)
            {
                if (first)
                    first = false;
                else
                    sb.Append(", ");

                sb.Append(v);
            }

            sb.Append('"');

            return sb.ToString();
        }
    }
}

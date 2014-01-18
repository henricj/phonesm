// -----------------------------------------------------------------------
//  <copyright file="M3U8AttributeSupport.cs" company="Henric Jungheim">
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
using System.Globalization;
using System.Linq;

namespace SM.Media.M3U8.AttributeSupport
{
    public static class M3U8AttributeSupport
    {
        public static M3U8TagInstance CreateInstance(M3U8Tag tag, string value)
        {
            return new M3U8TagInstance(tag);
        }

        public static M3U8AttributeValueInstance<long> DecimalIntegerParser(M3U8Attribute attribute, string value)
        {
            return new M3U8AttributeValueInstance<long>(attribute, long.Parse(value, CultureInfo.InvariantCulture));
        }

        public static M3U8AttributeValueInstance<string> QuotedStringParser(M3U8Attribute attribute, string value)
        {
            // TODO: Remove escape characters here.  Fixup StringAttributeInstance.ToString() to match.

            if (value.Length < 2 || '"' != value[0] || '"' != value[value.Length - 1])
            {
                // TODO: Complain...?
                return null;
            }

            return new StringAttributeInstance(attribute, value.Substring(1, value.Length - 2));
        }

        static string StripQuotes(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return null;

            s = s.Trim();

            if (s.Length < 2 || '"' != s[0] || '"' != s[s.Length - 1])
            {
                // TODO: Complain...?
                return null;
            }

            return s.Substring(1, s.Length - 2);
        }

        public static M3U8AttributeValueInstance<IEnumerable<string>> QuotedCsvParser(M3U8Attribute attribute, string value)
        {
            value = StripQuotes(value);

            var values = value.Split(',').Select(s => s.Trim()).ToArray();

            return new CsvStringsAttributeInstance(attribute, values);
        }

        /// <summary>
        ///     Parse "hexadecimal-integer" consisting of 0x or 0X followed by 1..n
        /// </summary>
        /// <param name="attribute"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static M3U8AttributeValueInstance<byte[]> HexadecialIntegerParser(M3U8Attribute attribute, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var start = value.IndexOf("0x", StringComparison.OrdinalIgnoreCase);

            if (start < 0 || start + 2 >= value.Length)
                return null;

            start += 2;

            var bytes = new List<byte>(16);

            var currentNibble = 0;
            var haveNibble = false;

            for (var i = start; i < value.Length; ++i)
            {
                var c = value[i];
                byte v;

                if (c >= '0' && c <= '9')
                    v = (byte)(c - '0');
                else if (c >= 'a' && c <= 'f')
                    v = (byte)(c - 'a' + 10);
                else if (c >= 'A' && c <= 'F')
                    v = (byte)(c - 'A' + 10);
                else
                {
                    // Skip everything else or fail?
                    // Are spaces okay?

                    continue;
                }

                if (haveNibble)
                {
                    bytes.Add((byte)((currentNibble << 4) | v));
                    haveNibble = false;
                }
                else
                {
                    currentNibble = v;
                    haveNibble = true;
                }
            }

            if (haveNibble)
            {
                // We now discover that we actually needed a leading zero when we first started...
                bytes.Add((byte)(currentNibble << 4));

                currentNibble = 0;

                for (var i = 0; i < bytes.Count; ++i)
                {
                    var b = bytes[i];

                    bytes[i] = (byte)((currentNibble << 4) | (b >> 4));

                    currentNibble = (byte)(b & 0x0f);
                }
            }

            return new HexadecimalIntegerAttributeInstance(attribute, bytes.ToArray());
        }
    }
}

// -----------------------------------------------------------------------
//  <copyright file="Rfc2047Encoding.cs" company="Henric Jungheim">
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
using System.Linq;
using System.Text;

namespace SM.Media.Web
{
    public static class Rfc2047Encoding
    {
        static readonly bool[] NeedsEncodingFlags = new bool[0x7f];

        static Rfc2047Encoding()
        {
            for (var c = (char)0; c < NeedsEncodingFlags.Length; ++c)
            {
                if (char.IsControl(c) || char.IsWhiteSpace(c)
                    || '=' == c || '"' == c)
                    NeedsEncodingFlags[c] = true;
            }
        }

        public static bool NeedsRfc2047Encoding(char value)
        {
            if (value >= NeedsEncodingFlags.Length)
                return true;

            return NeedsEncodingFlags[value];
        }

        public static bool NeedsRfc2047Encoding(string value)
        {
            return value.Cast<char>().Any(NeedsRfc2047Encoding);
        }

        public static string Rfc2047Encode(this string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            if (!NeedsRfc2047Encoding(value))
                return value;

            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

            return "=?utf-8?B?" + encoded + "?=";
        }
    }
}

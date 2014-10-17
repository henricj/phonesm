// -----------------------------------------------------------------------
//  <copyright file="Windows1252Encoding.cs" company="Henric Jungheim">
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

using System.Collections.Generic;
using System.Text;

namespace SM.Media.Utility.TextEncodings
{
    /// <summary>
    ///     A simplified CP1252 encoding that makes no attempt
    ///     to normalize, deal with combining characters, surrogates,
    ///     or such.
    /// </summary>
    public class Windows1252Encoding : Encoding
    {
        static readonly char[] CharLookup =
        {
            '\u20ac', '\ufffd', '\u201a', '\u0192',
            '\u201e', '\u2026', '\u2020', '\u2021',
            '\u02c6', '\u2030', '\u0160', '\u2039',
            '\u0152', '\ufffd', '\u017d', '\ufffd',
            '\ufffd', '\u2018', '\u2019', '\u201c',
            '\u201d', '\u2022', '\u2013', '\u2014',
            '\u02dc', '\u2122', '\u0161', '\u203a',
            '\u0153', '\ufffd', '\u017e', '\u0178'
        };

        static readonly Dictionary<char, byte> ByteLookup;
        static readonly int ByteLookupMax;

        static Windows1252Encoding()
        {
            ByteLookup = new Dictionary<char, byte>(CharLookup.Length);

            for (var i = 0; i < CharLookup.Length; ++i)
            {
                var c = CharLookup[i];

                if (EncodingHelpers.ReplacementCharacter == c)
                    continue;

                if (c > ByteLookupMax)
                    ByteLookupMax = c;

                ByteLookup[c] = (byte)(i + 0x80);
            }
        }

        public override int GetByteCount(char[] chars, int index, int count)
        {
            return count;
        }

        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
        {
            for (var i = 0; i < charCount; ++i)
            {
                var c = chars[i + charIndex];

                byte b;

                if (c < 0x80)
                    b = (byte)c;
                else if (c < 0xa0)
                {
                    if (!ByteLookup.TryGetValue(c, out b))
                        b = (byte)'?';
                }
                else if (c < 0x100)
                    b = (byte)c;
                else
                {
                    b = (byte)'?';
                }

                bytes[i + byteIndex] = b;
            }

            return charCount;
        }

        public override int GetCharCount(byte[] bytes, int index, int count)
        {
            return count;
        }

        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
        {
            for (var i = 0; i < byteCount; ++i)
            {
                var b = bytes[i + byteIndex];

                char c;

                if (b <= 0x80 || b >= 0xa0)
                    c = (char)b;
                else
                    c = CharLookup[b - 0x80];

                chars[i + charIndex] = c;
            }

            return byteCount;
        }

        public override int GetMaxByteCount(int charCount)
        {
            return charCount;
        }

        public override int GetMaxCharCount(int byteCount)
        {
            return byteCount;
        }
    }
}

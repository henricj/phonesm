// -----------------------------------------------------------------------
//  <copyright file="EncodingHelpers.cs" company="Henric Jungheim">
//  Copyright (c) 2012-2016.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012-2016 Henric Jungheim <software@henric.org>
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

namespace SM.Media.Utility.TextEncodings
{
    public static class EncodingHelpers
    {
        public const char ReplacementCharacter = '\ufffd';

        public static bool HasSurrogate(char[] chars, int charIndex, int charCount)
        {
            for (var i = charIndex; i < charCount + charIndex; ++i)
            {
                if (char.IsSurrogate(chars[i]))
                    return true;
            }

            return false;
        }

        public static IEnumerable<int> CodePoints(char[] chars, int charIndex, int charCount)
        {
            char? highSurrogate = null;

            for (var i = charIndex; i < charCount + charIndex; ++i)
            {
                var c = chars[i];

                if (highSurrogate.HasValue)
                {
                    if (char.IsSurrogatePair(highSurrogate.Value, c))
                        yield return char.ConvertToUtf32(highSurrogate.Value, c);
                    else
                        yield return '?';

                    highSurrogate = null;
                }
                else
                {
                    if (char.IsSurrogate(c))
                        highSurrogate = c;
                    else
                        yield return c;
                }
            }
        }
    }
}

/*
 * Latin1Encoding.cs - Implementation of the
 *			"System.Text.Latin1Encoding" class.
 *
 * Copyright (c) 2002  Southern Storm Software, Pty Ltd
 *
 * Permission is hereby granted, free of charge, to any person obtaining
 * a copy of this software and associated documentation files (the "Software"),
 * to deal in the Software without restriction, including without limitation
 * the rights to use, copy, modify, merge, publish, distribute, sublicense,
 * and/or sell copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included
 * in all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS
 * OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
 * THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR
 * OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
 * ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
 * OTHER DEALINGS IN THE SOFTWARE.
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace SM.Media.Mono.Text
{
    public class Latin1Encoding : Encoding
    {
        // Magic number used by Windows for the ISO Latin1 code page.
        internal const int ISOLATIN_CODE_PAGE = 28591;

        EncoderFallback EncoderFallback
        {
            get { return EncoderFallback.ReplacementFallback; }
        }

        // Get the number of bytes needed to encode a character buffer.
        public override int GetByteCount(char[] chars, int index, int count)
        {
            if (chars == null)
                throw new ArgumentNullException("chars");
            if (index < 0 || index > chars.Length)
                throw new ArgumentOutOfRangeException("index");
            if (count < 0 || count > (chars.Length - index))
                throw new ArgumentOutOfRangeException("count");
            return count;
        }

        // Convenience wrappers for "GetByteCount".
        public override int GetByteCount(String s)
        {
            if (s == null)
                throw new ArgumentNullException("s");
            return s.Length;
        }

        // Get the bytes that result from encoding a character buffer.
        public override int GetBytes(char[] chars, int charIndex, int charCount,
            byte[] bytes, int byteIndex)
        {
            EncoderFallbackBuffer buffer = null;
            char[] fallback_chars = null;
            return GetBytes(chars, charIndex, charCount, bytes,
                byteIndex, ref buffer, ref fallback_chars);
        }

        int GetBytes(char[] chars, int charIndex, int charCount,
            byte[] bytes, int byteIndex,
            ref EncoderFallbackBuffer buffer,
            ref char[] fallback_chars)
        {
            if (chars == null)
                throw new ArgumentNullException("chars");

            return InternalGetBytes(chars, chars.Length, charIndex, charCount, bytes, byteIndex, ref buffer, ref fallback_chars);
        }

        // Convenience wrappers for "GetBytes".
        public override int GetBytes(String s, int charIndex, int charCount,
            byte[] bytes, int byteIndex)
        {
            EncoderFallbackBuffer buffer = null;
            char[] fallback_chars = null;
            return GetBytes(s, charIndex, charCount, bytes, byteIndex,
                ref buffer, ref fallback_chars);
        }

        int GetBytes(String s, int charIndex, int charCount,
            byte[] bytes, int byteIndex,
            ref EncoderFallbackBuffer buffer,
            ref char[] fallback_chars)
        {
            if (s == null)
                throw new ArgumentNullException("s");

            var chars = s.ToCharArray();

            return InternalGetBytes(chars, chars.Length, charIndex, charCount, bytes, byteIndex, ref buffer, ref fallback_chars);
        }

        int InternalGetBytes(IEnumerable<char> chars, int charLength, int charIndex, int charCount,
            byte[] bytes, int byteIndex,
            ref EncoderFallbackBuffer buffer,
            ref char[] fallback_chars)
        {
            if (bytes == null)
                throw new ArgumentNullException("bytes");
            if (charIndex < 0 || charIndex > charLength)
                throw new ArgumentOutOfRangeException("charIndex");
            if (charCount < 0 || charCount > (charLength - charIndex))
                throw new ArgumentOutOfRangeException("charCount");
            if (byteIndex < 0 || byteIndex > bytes.Length)
                throw new ArgumentOutOfRangeException("byteIndex");
            if ((bytes.Length - byteIndex) < charCount)
                throw new ArgumentException("Insufficient space", "bytes");

            using (var charIter = chars.GetEnumerator())
            {
                for (var i = 0; i <= charIndex; ++i)
                    charIter.MoveNext();

                var count = charCount;
                while (count-- > 0)
                {
                    var ch = charIter.Current;

                    ++charIndex;
                    charIter.MoveNext();

                    if (ch < (char)0x0100)
                        bytes[byteIndex++] = (byte)ch;
                    else if (ch >= '\uFF01' && ch <= '\uFF5E')
                        bytes[byteIndex++] = (byte)(ch - 0xFEE0);
                    else
                    {
                        if (buffer == null)
                            buffer = EncoderFallback.CreateFallbackBuffer();

                        if (Char.IsSurrogate(ch) && count > 1 &&
                            Char.IsSurrogate(charIter.Current))
                        {
                            buffer.Fallback(ch, charIter.Current, charIndex++ - 1);
                            charIter.MoveNext();
                        }
                        else
                            buffer.Fallback(ch, charIndex - 1);

                        if (fallback_chars == null || fallback_chars.Length < buffer.Remaining)
                            fallback_chars = new char[buffer.Remaining];

                        for (var i = 0; i < fallback_chars.Length; i++)
                            fallback_chars[i] = buffer.GetNextChar();

                        byteIndex += GetBytes(fallback_chars, 0,
                            fallback_chars.Length, bytes, byteIndex,
                            ref buffer, ref fallback_chars);
                    }
                }
            }
            return charCount;
        }

        // Get the number of characters needed to decode a byte buffer.
        public override int GetCharCount(byte[] bytes, int index, int count)
        {
            if (bytes == null)
                throw new ArgumentNullException("bytes");
            if (index < 0 || index > bytes.Length)
                throw new ArgumentOutOfRangeException("index");
            if (count < 0 || count > (bytes.Length - index))
                throw new ArgumentOutOfRangeException("count");

            return count;
        }

        // Get the characters that result from decoding a byte buffer.
        public override int GetChars(byte[] bytes, int byteIndex, int byteCount,
            char[] chars, int charIndex)
        {
            if (bytes == null)
                throw new ArgumentNullException("bytes");
            if (chars == null)
                throw new ArgumentNullException("chars");
            if (byteIndex < 0 || byteIndex > bytes.Length)
                throw new ArgumentOutOfRangeException("byteIndex");
            if (byteCount < 0 || byteCount > (bytes.Length - byteIndex))
                throw new ArgumentOutOfRangeException("byteCount");
            if (charIndex < 0 || charIndex > chars.Length)
                throw new ArgumentOutOfRangeException("charIndex");
            if ((chars.Length - charIndex) < byteCount)
                throw new ArgumentException("chars");

            var count = byteCount;
            while (count-- > 0)
                chars[charIndex++] = (char)(bytes[byteIndex++]);

            return byteCount;
        }

        // Get the maximum number of bytes needed to encode a
        // specified number of characters.
        public override int GetMaxByteCount(int charCount)
        {
            if (charCount < 0)
                throw new ArgumentOutOfRangeException("charCount");
            return charCount;
        }

        // Get the maximum number of characters needed to decode a
        // specified number of bytes.
        public override int GetMaxCharCount(int byteCount)
        {
            if (byteCount < 0)
                throw new ArgumentOutOfRangeException("byteCount");
            return byteCount;
        }

        // Decode a buffer of bytes into a string.
        public override String GetString(byte[] bytes, int index, int count)
        {
            if (bytes == null)
                throw new ArgumentNullException("bytes");
            if (index < 0 || index > bytes.Length)
                throw new ArgumentOutOfRangeException("index");
            if (count < 0 || count > (bytes.Length - index))
                throw new ArgumentOutOfRangeException("count");
            if (count == 0)
                return string.Empty;

            var sb = new StringBuilder(count);

            for (var i = index; i < index + count; ++i)
                sb.Append((char)bytes[i]);

            return sb.ToString();
        }

        //public override String GetString (byte[] bytes)
        //{
        //    if (bytes == null) {
        //        throw new ArgumentNullException ("bytes");
        //    }

        //    return GetString (bytes, 0, bytes.Length);
        //}

        // Get the IANA-preferred Web name for this encoding.
        public override String WebName
        {
            get { return "iso-8859-1"; }
        }
    }; // class Latin1Encoding
} ; // namespace System.Text

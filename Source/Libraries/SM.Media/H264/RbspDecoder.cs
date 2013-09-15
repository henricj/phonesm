// -----------------------------------------------------------------------
//  <copyright file="RbspDecoder.cs" company="Henric Jungheim">
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
using System.Diagnostics;

namespace SM.Media.H264
{
    public class RbspDecoder : INalParser
    {
        readonly List<byte> _outputBuffer = new List<byte>();
        public Action<IList<byte>> CompletionHandler { get; set; }

        #region INalParser Members

        public bool Parse(byte[] buffer, int offset, int length, bool hasEscape)
        {
            if (hasEscape)
            {
                buffer = RemoveEscapes(buffer, offset, length);
                offset = 0;
                length = buffer.Length;
            }

            var count = length;

            // Find and strip zeros after "rbsp_stop_one_bit".  We are still stuck
            // with full bytes, but this should help deal with NAL decoding issues.
            for (var i = length - 1; i > 0; --i)
            {
                if (0 != buffer[offset + i])
                    break;

                Debug.WriteLine("RBSP with trailing zero");

                count = i + 1;
            }

            if (count <= 0)
                return false;

            if (null != CompletionHandler)
            {
                if (!hasEscape || count != length)
                {
                    var data = new byte[count];

                    Array.Copy(buffer, offset, data, 0, count);

                    buffer = data;
                    length = count;
                    offset = 0;
                }

                CompletionHandler(buffer);
            }

            return true;
        }

        #endregion

        byte[] RemoveEscapes(byte[] buffer, int offset, int length)
        {
            PrepareOutputBuffer(length);

            var count = 0;

            for (var i = 0; i < length; ++i)
            {
                var v = buffer[offset + i];

                if (2 == count)
                {
                    if (v < 0x03)
                        throw new FormatException("Invalid escape sequence");

                    if (0x03 == v)
                    {
                        if (i + 1 < length && buffer[offset + i + 1] > 0x03)
                            throw new FormatException("Invalid escape sequence");

                        count = 0;
                        continue;
                    }
                }

                if (0 == v)
                    ++count;
                else
                    count = 0;

                _outputBuffer.Add(v);
            }

            return _outputBuffer.ToArray();
        }

        void PrepareOutputBuffer(int length)
        {
            _outputBuffer.Clear();

            if (_outputBuffer.Capacity < length)
            {
                var newLength = _outputBuffer.Capacity;

                if (newLength < 8)
                    newLength = 8;
                else if (length > int.MaxValue / 4)
                    newLength = length;

                while (newLength < length)
                    newLength *= 2;

                _outputBuffer.Capacity = newLength;
            }
        }
    }
}

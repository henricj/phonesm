//-----------------------------------------------------------------------
// <copyright file="RbspDecoder.cs" company="Henric Jungheim">
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
using System.Collections.Generic;

namespace SM.Media.H264
{
    class RbspDecoder : INalParser
    {
        readonly List<byte> _buffer = new List<byte>(1024);
        int _zeroCount;
        public Action<IList<byte>> CompletionHandler { get; set; }

        #region INalParser Members

        public void Start()
        {
            _zeroCount = 0;
            _buffer.Clear();
        }

        public bool Parse(byte[] buffer, int offset, int length)
        {
            for (var i = offset; i < offset + length; ++i)
            {
                var v = buffer[i];

                // RBSP Decode
                var previousZeroCount = _zeroCount;

                if (0 == v)
                    ++_zeroCount;
                else
                    _zeroCount = 0;

                if (2 == previousZeroCount && v == 3)
                    continue; // Skip the 0x03 in a 0x000003 pattern

                _buffer.Add(v);
            }

            return true;
        }

        public void Finish()
        {
            if (null != CompletionHandler)
                CompletionHandler(_buffer);
        }

        #endregion
    }
}

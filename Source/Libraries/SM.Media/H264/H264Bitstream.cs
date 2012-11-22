//-----------------------------------------------------------------------
// <copyright file="H264Bitstream.cs" company="Henric Jungheim">
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
    class H264Bitstream : IDisposable
    {
        byte _bitIndex;
        IEnumerator<byte> _bytes;

        public H264Bitstream(IEnumerable<byte> buffer)
        {
            _bytes = buffer.GetEnumerator();

            _bitIndex = 8;
        }

        #region IDisposable Members

        /// <summary>
        ///   Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            using (_bytes)
            { }
        }

        #endregion

        public uint ReadBits(int count)
        {
            if (null == _bytes)
                return 0;

            var ret = 0u;

            while (count > 0)
            {
                if (_bitIndex >= 8u)
                {
                    if (!_bytes.MoveNext())
                    {
                        _bytes.Dispose();
                        _bytes = null;

                        return ret;
                    }

                    _bitIndex = 0;
                }

                var v = _bytes.Current;

                if (0 == _bitIndex && count >= 8)
                {
                    ret = (ret << 8) | v;

                    count -= 8;
                    _bitIndex = 8;

                    continue;
                }

                var bitsLeft = (byte)(8 - _bitIndex);

                var takeBits = (byte)Math.Min(bitsLeft, count);

                ret <<= takeBits;

                var mask = (1u << takeBits) - 1u;

                v >>= bitsLeft - takeBits;

                ret |= v & mask;

                _bitIndex += takeBits;
                count -= takeBits;
            }

            return ret;
        }
    }
}

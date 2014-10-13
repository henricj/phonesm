// -----------------------------------------------------------------------
//  <copyright file="H264Bitstream.cs" company="Henric Jungheim">
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

namespace SM.Media.H264
{
    class H264Bitstream : IDisposable
    {
        int _bitsLeft;

        IEnumerator<byte> _bytes;
        byte _currentByte;
        byte _nextByte;

        public H264Bitstream(IEnumerable<byte> buffer)
        {
            _bytes = buffer.GetEnumerator();

            if (!_bytes.MoveNext())
            {
                _bytes.Dispose();
                _bytes = null;

                return;
            }

            _nextByte = _bytes.Current;

            _bitsLeft = 0;
        }

        #region IDisposable Members

        /// <summary>
        ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            using (_bytes)
            { }
        }

        #endregion

        public bool HasData
        {
            get
            {
                if (_bitsLeft > 0)
                    return true;

                return GetMoreBits();
            }
        }

        bool GetMoreBits()
        {
            if (_bitsLeft > 0)
                return true;

            if (null == _bytes)
                return false;

            _currentByte = _nextByte;

            if (!_bytes.MoveNext())
            {
                _bytes.Dispose();
                _bytes = null;

                _bitsLeft = 0;

                var zeros = 0;
                while (0 == (1 & _currentByte))
                {
                    ++zeros;
                    _currentByte >>= 1;
                }

                if (zeros > 6)
                    return false;

                _currentByte >>= 1;
                _bitsLeft = 7 - zeros;

                return true;
            }

            _bitsLeft = 8;
            _nextByte = _bytes.Current;

            return true;
        }

        public uint ReadBits(int count)
        {
            var ret = 0u;

            while (count > 0)
            {
                if (_bitsLeft < 1)
                {
                    if (!GetMoreBits())
                        throw new FormatException("Read past the end of the RBSP stream");
                }

                var v = _currentByte;

                if (8 == _bitsLeft && count >= 8)
                {
                    ret = (ret << 8) | v;

                    count -= 8;
                    _bitsLeft = 0;

                    continue;
                }

                var takeBits = Math.Min(_bitsLeft, count);

                ret <<= takeBits;

                var mask = (1u << takeBits) - 1u;

                v >>= _bitsLeft - takeBits;

                ret |= v & mask;

                _bitsLeft -= takeBits;
                count -= takeBits;
            }

            return ret;
        }
    }
}

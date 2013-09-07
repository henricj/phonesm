// -----------------------------------------------------------------------
//  <copyright file="H256MetadataParser.cs" company="Henric Jungheim">
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

namespace SM.Media.H264
{
    public class H256MetadataParser
    {
        #region Delegates

        public delegate bool ParserStateHandler(byte[] buffer, int offset, int length);

        #endregion

        static readonly byte[] ZeroBuffer = { 0, 0, 0, 0 };

        readonly Func<byte, ParserStateHandler> _resolveHandler;
        ParserStateHandler _currentParser;
        bool _expectingNalUnitType;
        int _zeroCount;

        public H256MetadataParser(Func<byte, ParserStateHandler> resolveHandler)
        {
            _resolveHandler = resolveHandler;
        }

        void CompleteNalUnit(byte[] buffer, int offset, int length)
        {
            //Debug.WriteLine("NAL Unit ({0}): {1}", length, BitConverter.ToString(buffer, offset, length));

            if (null != _currentParser)
                _currentParser(buffer, offset, length);
        }

        public void Parse(byte[] buffer, int offset, int length)
        {
            if (0 == length)
            {
                if (null != _currentParser)
                {
                    if (_zeroCount > 0 && !_expectingNalUnitType)
                        CompleteNalUnit(ZeroBuffer, 0, Math.Min(_zeroCount, 3));

                    _currentParser(null, 0, 0); // Propagate end-of-stream

                    _currentParser = null;
                }

                _zeroCount = 0;

                return;
            }

            var nalOffset = offset;

            for (var i = offset; i < length + offset; ++i)
            {
                var v = buffer[i];

                if (0 == v)
                {
                    ++_zeroCount;
                    _expectingNalUnitType = false;
                }
                else
                {
                    var previousZeroCount = _zeroCount;

                    // Do something with those zeros... the _currentParser may need them.
                    _zeroCount = 0;

                    if (_expectingNalUnitType)
                    {
                        _expectingNalUnitType = false;
                        _currentParser = _resolveHandler(v);
                        nalOffset = i;
                    }
                    else if (previousZeroCount >= 2)
                    {
                        if (v == 0x01)
                        {
                            previousZeroCount = Math.Min(previousZeroCount, 3);

                            if (nalOffset >= offset && i - previousZeroCount > nalOffset)
                                CompleteNalUnit(buffer, nalOffset, i - previousZeroCount - nalOffset);

                            // We have found a "start_code_prefix_one_3bytes"
                            _expectingNalUnitType = true;
                        }
                    }
                }
            }

            if (nalOffset < length + offset && !_expectingNalUnitType)
                CompleteNalUnit(buffer, nalOffset, length + offset - nalOffset);
        }
    }
}

// -----------------------------------------------------------------------
//  <copyright file="NalUnitParser.cs" company="Henric Jungheim">
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
    public class NalUnitParser
    {
        #region Delegates

        public delegate bool ParserStateHandler(byte[] buffer, int offset, int length, bool hasEscape);

        #endregion

        static readonly byte[] ZeroBuffer = { 0, 0, 0, 0 };

        readonly Func<byte, ParserStateHandler> _resolveHandler;
        ParserStateHandler _currentParser;
        bool _expectingNalUnitType;
        bool _hasEscape;
        int _lastCompletedOffset;
        int _nalOffset;
        int _zeroCount;

        public NalUnitParser(Func<byte, ParserStateHandler> resolveHandler)
        {
            _resolveHandler = resolveHandler;
        }

        void CompleteNalUnit(byte[] buffer, int offset, int length)
        {
            //Debug.WriteLine("NAL Unit ({0}): {1}", length, BitConverter.ToString(buffer, offset, length));

            _lastCompletedOffset = offset + length;

            if (null != _currentParser)
                _currentParser(buffer, offset, length, _hasEscape);
        }

        public void Reset()
        {
            _zeroCount = 0;
            _expectingNalUnitType = false;
            _nalOffset = -1;
            _hasEscape = false;
        }

        public int Parse(byte[] buffer, int offset, int length, bool isLast = true)
        {
            _lastCompletedOffset = 0;

            if (0 == length)
            {
                if (null != _currentParser)
                {
                    if (_zeroCount > 0 && !_expectingNalUnitType)
                        CompleteNalUnit(ZeroBuffer, 0, Math.Min(_zeroCount, 3));

                    _currentParser(null, 0, 0, false); // Propagate end-of-stream

                    _currentParser = null;
                }

                _zeroCount = 0;

                return 0;
            }

            for (var i = 0; i < length; ++i)
            {
                var v = buffer[i + offset];

                if (0 == v)
                {
                    if (++_zeroCount >= 3)
                    {
                        if (_nalOffset >= 0)
                        {
                            var nalLength = i + 1 - _zeroCount - _nalOffset;

                            if (nalLength > 0)
                                CompleteNalUnit(buffer, offset + _nalOffset, nalLength);
                        }

                        _nalOffset = -1;
                    }

                    _expectingNalUnitType = false;
                }
                else
                {
                    var previousZeroCount = _zeroCount;

                    _zeroCount = 0;

                    if (_expectingNalUnitType)
                    {
                        _expectingNalUnitType = false;
                        _currentParser = _resolveHandler(v);
                        _nalOffset = i;
                        _hasEscape = false;
                    }
                    else if (previousZeroCount >= 2)
                    {
                        if (v == 0x01)
                        {
                            if (_nalOffset >= 0)
                            {
                                var nalLength = i - _nalOffset - Math.Min(previousZeroCount, 3);

                                if (nalLength > 0)
                                    CompleteNalUnit(buffer, offset + _nalOffset, nalLength);
                            }

                            // We have found a "start_code_prefix_one_3bytes"
                            _expectingNalUnitType = true;
                            _nalOffset = -1;
                        }
                        else if (v == 0x03)
                            _hasEscape = true;
                    }
                }
            }

            if (isLast && !_expectingNalUnitType && _nalOffset >= 0)
            {
                var nalLength = length - _nalOffset;

                if (nalLength > 0)
                    CompleteNalUnit(buffer, offset + _nalOffset, nalLength);
            }

            var completed = _lastCompletedOffset - offset;

            if (isLast)
                Reset();

            if (completed > 0)
                return completed;

            return 0;
        }
    }
}

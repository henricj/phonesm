// -----------------------------------------------------------------------
//  <copyright file="Ac3Parser.cs" company="Henric Jungheim">
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
using System.Diagnostics;
using SM.Media.Audio;
using SM.TsParser;
using SM.TsParser.Utility;

namespace SM.Media.Ac3
{
    public sealed class Ac3Parser : AudioParserBase
    {
        public Ac3Parser(ITsPesPacketPool pesPacketPool, Action<IAudioFrameHeader> configurationHandler, Action<TsPesPacket> submitPacket)
            : base(new Ac3FrameHeader(), pesPacketPool, configurationHandler, submitPacket)
        { }

        public override void ProcessData(byte[] buffer, int offset, int length)
        {
            Debug.Assert(length > 0);
            Debug.Assert(offset + length <= buffer.Length);

            var endOffset = offset + length;

            // Make sure there is enough room for the frame header.  We really only need 5 bytes
            // for the header.
            EnsureBufferSpace(128);

            for (var i = offset; i < endOffset; )
            {
                var storedLength = _index - _startIndex;

                if (storedLength <= 4)
                {
                    var data = buffer[i++];

                    if (0 == storedLength)
                    {
                        if (0x0b == data)
                            _packet.Buffer[_index++] = 0x0b;
                    }
                    else if (1 == storedLength)
                    {
                        if (0x77 == data)
                            _packet.Buffer[_index++] = data;
                        else
                            _index = _startIndex;
                    }
                    else if (storedLength < 4)
                        _packet.Buffer[_index++] = data;
                    else
                    {
                        _packet.Buffer[_index++] = data;

                        // We now have an AC3 header.

                        if (!_frameHeader.Parse(_packet.Buffer, _startIndex, _index - _startIndex, !_isConfigured))
                        {
                            SkipInvalidFrameHeader();

                            continue;
                        }

                        Debug.Assert(_frameHeader.FrameLength > 7);

                        if (!_isConfigured)
                        {
                            _configurationHandler(_frameHeader);
                            _isConfigured = true;
                        }

                        // Even better: the frame header is valid.  Now we need some data...

                        EnsureBufferSpace(_frameHeader.FrameLength);
                    }
                }
                else
                {
                    // "_frameHeader" has a valid header and we have enough buffer space
                    // for the frame.

                    var remainingFrameLength = _frameHeader.FrameLength - (_index - _startIndex);
                    var remainingBuffer = endOffset - i;

                    var copyLength = Math.Min(remainingBuffer, remainingFrameLength);

                    Debug.Assert(copyLength > 0);

                    Array.Copy(buffer, i, _packet.Buffer, _index, copyLength);

                    _index += copyLength;
                    i += copyLength;

                    if (_index - _startIndex == _frameHeader.FrameLength)
                    {
                        // We have a completed AC3 frame.
                        SubmitFrame();
                    }
                }
            }
        }

        void SkipInvalidFrameHeader()
        {
            for (var i = _startIndex + 1; i < _index; ++i)
            {
                if (0xff == _packet.Buffer[i])
                {
                    Array.Copy(_packet.Buffer, i, _packet.Buffer, _startIndex, _index - i);
                    _index = i;
                    return;
                }
            }

            _index = 0;
        }
    }
}

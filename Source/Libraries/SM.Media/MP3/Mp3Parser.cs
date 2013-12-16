// -----------------------------------------------------------------------
//  <copyright file="Mp3Parser.cs" company="Henric Jungheim">
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
using System.Diagnostics;
using SM.Media.Audio;
using SM.TsParser;
using SM.TsParser.Utility;

namespace SM.Media.MP3
{
    sealed class Mp3Parser : AudioParserBase
    {
        public Mp3Parser(ITsPesPacketPool pesPacketPool, Action<IAudioFrameHeader> configurationHandler, Action<TsPesPacket> submitPacket)
            : base(new Mp3FrameHeader(), pesPacketPool, configurationHandler, submitPacket)
        { }

        public override void ProcessData(byte[] buffer, int offset, int length)
        {
            Debug.Assert(length > 0);
            Debug.Assert(offset + length <= buffer.Length);

            var endOffset = offset + length;

            // Make sure there is enough room for the frame header.  We really only need 4 bytes
            // for the header.
            EnsureBufferSpace(128);

            for (var i = offset; i < endOffset; )
            {
                var storedLength = _index - _startIndex;

                if (storedLength < 4)
                {
                    var data = buffer[i++];

                    if (0 == storedLength)
                    {
                        if (0xff == data)
                            _packet.Buffer[_index++] = 0xff;
                    }
                    else if (1 == storedLength)
                    {
                        if (0xe0 == (0xe0 & data))
                            _packet.Buffer[_index++] = data;
                        else
                            _index = _startIndex;
                    }
                    else if (2 == storedLength)
                        _packet.Buffer[_index++] = data;
                    else if (3 == storedLength)
                    {
                        _packet.Buffer[_index++] = data;

                        // We now have an MP3 header.

                        if (!_frameHeader.Parse(_packet.Buffer, _startIndex, _index - _startIndex, !_isConfigured))
                        {
                            SkipInvalidFrameHeader();

                            continue;
                        }

                        Debug.Assert(_frameHeader.FrameLength > 4);

                        if (!_isConfigured)
                        {
                            _configurationHandler(_frameHeader);

                            _isConfigured = true;
                        }

                        // Even better: the frame header is valid.  Now we need some data...

                        EnsureBufferSpace(_frameHeader.FrameLength - 4);
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
                        // We have a completed MP3 frame.
                        SubmitFrame();
                    }
                }
            }
        }

        void SkipInvalidFrameHeader()
        {
            if (0xff == _packet.Buffer[_startIndex + 1] &&
                0xe0 == (0xe0 & _packet.Buffer[_startIndex + 2]))
            {
                // _bufferEntry.Buffer[_startIndex] is already 0xff
                _packet.Buffer[_startIndex + 1] = _packet.Buffer[_startIndex + 2];
                _packet.Buffer[_startIndex + 2] = _packet.Buffer[_startIndex + 3];

                _index = _startIndex + 3;
            }
            else if (0xff == _packet.Buffer[_startIndex + 2] &&
                     0xe0 == (0xe0 & _packet.Buffer[_startIndex + 3]))
            {
                // _bufferEntry.Buffer[_startIndex] is already 0xff
                _packet.Buffer[_startIndex + 1] = _packet.Buffer[_startIndex + 3];

                _index = _startIndex + 2;
            }
            else if (0xff == _packet.Buffer[_startIndex + 3])
            {
                // _bufferEntry.Buffer[_startIndex] is already 0xff

                _index = _startIndex + 1;
            }
            else
                _index = 0;
        }
    }
}

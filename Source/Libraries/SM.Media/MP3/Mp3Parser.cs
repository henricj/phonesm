// -----------------------------------------------------------------------
//  <copyright file="Mp3Parser.cs" company="Henric Jungheim">
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

namespace SM.Media.MP3
{
    public sealed class Mp3Parser : AudioParserBase
    {
        bool _hasSeenValidFrames;
        int _skip;

        public Mp3Parser(ITsPesPacketPool pesPacketPool, Action<IAudioFrameHeader> configurationHandler, Action<TsPesPacket> submitPacket)
            : base(new Mp3FrameHeader(), pesPacketPool, configurationHandler, submitPacket)
        { }

        public override void ProcessData(byte[] buffer, int offset, int length)
        {
            Debug.Assert(length > 0);
            Debug.Assert(offset + length <= buffer.Length);

            if (0 == _skip && 0 == _index && 0 == _startIndex)
            {
                // This will not find all ID3 headers if someone is playing
                // games with the read buffer size.  However, for any reasonable
                // buffer size, the first 10 bytes of the file should wind up here
                // in one block.

                var id3Length = GetId3Length(buffer, offset, length);

                if (id3Length.HasValue)
                {
                    _skip = id3Length.Value + 10;

                    Debug.WriteLine("Mp3Parser.ProcessData() ID3 detected, length {0}", _skip);
                }
            }

            if (_skip > 0)
            {
                if (_skip >= length)
                {
                    _skip -= length;
                    return;
                }

                offset += _skip;
                length -= _skip;
                _skip = 0;
            }

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

                        if (!_frameHeader.Parse(_packet.Buffer, _startIndex, _index - _startIndex, !_isConfigured && _hasSeenValidFrames))
                        {
                            SkipInvalidFrameHeader();

                            continue;
                        }

                        Debug.Assert(_frameHeader.FrameLength > 4);

                        if (!_isConfigured && _hasSeenValidFrames)
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
                        if (_hasSeenValidFrames)
                            SubmitFrame();
                        else
                            _hasSeenValidFrames = true;

                        SubmitFrame();
                    }
                }
            }
        }

        public override void FlushBuffers()
        {
            base.FlushBuffers();

            _skip = 0;
            _hasSeenValidFrames = false;
        }

        static int? GetId3Length(byte[] buffer, int offset, int length)
        {
            if (length < 10)
                return null;

            if ('I' != buffer[offset] || 'D' != buffer[offset + 1] || '3' != buffer[offset + 2])
                return null;

            var majorVersion = buffer[offset + 3];

            if (0xff == majorVersion)
                return null;

            var minorVersion = buffer[offset + 4];

            if (0xff == minorVersion)
                return null;

            var flags = buffer[offset + 5];

            var size = 0;

            for (var i = 0; i < 4; ++i)
            {
                var b = buffer[offset + 6 + i];

                if (0 != (0x80 & b))
                    return null;

                size <<= 7;

                size |= b;
            }

            return size;
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

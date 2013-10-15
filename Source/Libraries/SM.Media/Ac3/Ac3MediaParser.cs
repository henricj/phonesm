// -----------------------------------------------------------------------
//  <copyright file="Ac3MediaParser.cs" company="Henric Jungheim">
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
using SM.TsParser;
using SM.TsParser.Utility;

namespace SM.Media.Ac3
{
    public sealed class Ac3MediaParser : IMediaParser
    {
        static readonly TsStreamType StreamType = TsStreamType.FindStreamType(0x0F);
        readonly IBufferPool _bufferPool;
        readonly Ac3Configurator _configurator = new Ac3Configurator();
        readonly Ac3FrameHeader _frameHeader = new Ac3FrameHeader();
        readonly MediaStream _mediaStream;
        readonly TsPesPacketPool _packetPool;
        readonly StreamBuffer _streamBuffer;
        BufferInstance _bufferEntry;
        int _index;
        bool _isConfigured;
        TimeSpan? _position;
        int _startIndex;

        public Ac3MediaParser(IBufferingManager bufferingManager, IBufferPool bufferPool, Action checkForSamples)
        {
            if (null == bufferingManager)
                throw new ArgumentNullException("bufferingManager");

            if (null == bufferPool)
                throw new ArgumentNullException("bufferPool");

            _bufferPool = bufferPool;

            _packetPool = new TsPesPacketPool(_bufferPool);

            _streamBuffer = new StreamBuffer(StreamType, _packetPool.FreePesPacket, bufferingManager, checkForSamples);

            _mediaStream = new MediaStream(_configurator, _streamBuffer, null);
        }

        public IMediaParserMediaStream MediaStream
        {
            get { return _mediaStream; }
        }

        #region IMediaParser Members

        public void Dispose()
        {
            Clear();
        }

        public void ProcessEndOfData()
        {
            _streamBuffer.Enqueue(null);
        }

        public void ProcessData(byte[] buffer, int offset, int length)
        {
            Debug.Assert(length > 0);
            Debug.Assert(offset + length <= buffer.Length);

            var endOffset = offset + length;

            // Make sure there is enough room for the frame header.  We really only need 9 bytes
            // for the header.
            EnsureBufferSpace(128);

            for (var i = offset; i < endOffset; )
            {
                var storedLength = _index - _startIndex;

                if (storedLength <= 9)
                {
                    var data = buffer[i++];

                    if (0 == storedLength)
                    {
                        if (0x0b == data)
                            _bufferEntry.Buffer[_index++] = 0x0b;
                    }
                    else if (1 == storedLength)
                    {
                        if (0x77 == data)
                            _bufferEntry.Buffer[_index++] = data;
                        else
                            _index = _startIndex;
                    }
                    else if (storedLength < 9)
                        _bufferEntry.Buffer[_index++] = data;
                    else
                    {
                        _bufferEntry.Buffer[_index++] = data;

                        // We now have an AC3 header.

                        if (!_frameHeader.Parse(_bufferEntry.Buffer, _startIndex, _index - _startIndex, !_isConfigured))
                        {
                            SkipInvalidFrameHeader();

                            continue;
                        }

                        Debug.Assert(_frameHeader.FrameLength > 7);

                        if (!_isConfigured)
                        {
                            _configurator.Configure(_frameHeader);
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

                    Array.Copy(buffer, i, _bufferEntry.Buffer, _index, copyLength);

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

        public void FlushBuffers()
        {
            FreeBuffer();
        }

        public bool EnableProcessing { get; set; }
        public TimeSpan StartPosition { get; set; }

        public void Initialize(Action<IProgramStreams> programstreamsHandler)
        { }

        #endregion

        void SubmitFrame()
        {
            var packet = _packetPool.AllocatePesPacket(_bufferEntry);

            packet.Index = _startIndex;
            packet.Length = _index - _startIndex;

            if (!_position.HasValue)
                _position = StartPosition;

            packet.PresentationTimestamp = _position.Value;

            _position += _frameHeader.Duration;

            //Debug.WriteLine("Ac3MediaParser.SubmitFrame: position {0} duration {1}", _position, _frameHeader.Duration);

            _streamBuffer.Enqueue(packet);

            _startIndex = _index;

            EnsureBufferSpace(128);
        }

        void FreeBuffer()
        {
            if (null != _bufferEntry)
            {
                var bufferEntry = _bufferEntry;

                _bufferEntry = null;

                _bufferPool.Free(bufferEntry);
            }

            _startIndex = 0;
            _index = 0;
        }

        void SkipInvalidFrameHeader()
        {
            for (var i = _startIndex + 1; i < _index; ++i)
            {
                if (0xff == _bufferEntry.Buffer[i])
                {
                    Array.Copy(_bufferEntry.Buffer, i, _bufferEntry.Buffer, _startIndex, _index - i);
                    _index = i;
                    return;
                }
            }

            _index = 0;
        }

        void EnsureBufferSpace(int length)
        {
            if (null == _bufferEntry)
            {
                _bufferEntry = _bufferPool.Allocate(length);
                _index = 0;
                _startIndex = 0;

                return;
            }

            if (_index + length <= _bufferEntry.Buffer.Length)
                return;

            var newBuffer = _bufferPool.Allocate(length);

            if (_index > _startIndex)
            {
                // Copy the partial frame to the new buffer.
                _index -= _startIndex;

                Array.Copy(_bufferEntry.Buffer, _startIndex, newBuffer.Buffer, 0, _index);
            }
            else
                _index = 0;

            _startIndex = 0;

            _bufferPool.Free(_bufferEntry);

            _bufferEntry = newBuffer;
        }

        void Clear()
        {
            _packetPool.Clear();
        }
    }
}

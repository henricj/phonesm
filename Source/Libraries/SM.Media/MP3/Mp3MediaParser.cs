// -----------------------------------------------------------------------
//  <copyright file="Mp3MediaParser.cs" company="Henric Jungheim">
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
using SM.Media.Buffering;
using SM.TsParser;
using SM.TsParser.Utility;

namespace SM.Media.MP3
{
    sealed class Mp3MediaParser : IMediaParser
    {
        static readonly TsStreamType StreamType = TsStreamType.FindStreamType(0x03);
        readonly Mp3Configurator _configurator = new Mp3Configurator();
        readonly MediaStream _mediaStream;
        readonly Mp3Parser _parser;
        readonly StreamBuffer _streamBuffer;

        public Mp3MediaParser(IBufferingManager bufferingManager, IBufferPool bufferPool, Action checkForSamples)
        {
            if (null == bufferingManager)
                throw new ArgumentNullException("bufferingManager");

            var pesPacketPool = new TsPesPacketPool(bufferPool);

            _streamBuffer = new StreamBuffer(StreamType, pesPacketPool.FreePesPacket, bufferingManager, checkForSamples);

            _parser = new Mp3Parser(pesPacketPool, _configurator.Configure, SubmitPacket);

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

        public TimeSpan StartPosition
        {
            get { return _parser.StartPosition; }
            set { _parser.StartPosition = value; }
        }

        public void ProcessEndOfData()
        {
            _streamBuffer.Enqueue(null);
        }

        public void ProcessData(byte[] buffer, int offset, int length)
        {
            Debug.Assert(length > 0);
            Debug.Assert(length <= buffer.Length);

            _parser.ProcessData(buffer, offset, length);
        }

        public void FlushBuffers()
        {
            _parser.FlushBuffers();
        }

        public bool EnableProcessing { get; set; }

        public void Initialize(Action<IProgramStreams> programstreamsHandler)
        { }

        #endregion

        void SubmitPacket(TsPesPacket packet)
        {
            _streamBuffer.Enqueue(packet);
        }

        void Clear()
        {
            _parser.FlushBuffers();
        }
    }
}

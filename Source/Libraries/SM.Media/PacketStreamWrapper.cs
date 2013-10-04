// -----------------------------------------------------------------------
//  <copyright file="PacketStreamWrapper.cs" company="Henric Jungheim">
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
using System.IO;
using SM.Media.Pes;

namespace SM.Media
{
    public sealed class PacketStreamWrapper
    {
        readonly PesStream _pesStream = new PesStream();
        readonly IStreamSource _source;
        readonly StreamSample _streamSample = new StreamSample();

        public PacketStreamWrapper(IStreamSource source)
        {
            if (source == null)
                throw new ArgumentNullException("source");

            _source = source;

            _streamSample.Stream = _pesStream;
        }

        public bool GetNextSample(Func<IStreamSample, bool> streamSampleHandler)
        {
            var packet = _source.GetNextSample();

            if (null == packet)
            {
                if (_source.IsEof)
                {
                    streamSampleHandler(null);

                    return true;
                }

                _streamSample.BufferingProgress = _source.BufferingProgress;

                streamSampleHandler(_streamSample);

                return false;
            }

            try
            {
                _pesStream.Packet = packet;

                _streamSample.PresentationTimestamp = _source.PresentationTimestamp;
                _streamSample.BufferingProgress = null;

                streamSampleHandler(_streamSample);

                _pesStream.Packet = null;
            }
            finally
            {
                _source.FreeSample(packet);
            }

            return true;
        }

        #region Nested type: StreamSample

        class StreamSample : IStreamSample
        {
            #region IStreamSample Members

            public TimeSpan PresentationTimestamp { get; set; }
            public Stream Stream { get; set; }
            public float? BufferingProgress { get; set; }

            #endregion
        }

        #endregion
    }
}

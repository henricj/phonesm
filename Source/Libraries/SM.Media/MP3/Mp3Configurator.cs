// -----------------------------------------------------------------------
//  <copyright file="Mp3Configurator.cs" company="Henric Jungheim">
//  Copyright (c) 2012.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
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
using SM.Media.Configuration;
using SM.Media.Mmreg;

namespace SM.Media.MP3
{
    class Mp3Configurator : IAudioConfigurationSource, IConfigurationSink
    {
        readonly Mp3FrameHeader _frameHeader = new Mp3FrameHeader();
        readonly MpegLayer3WaveFormat _waveFormat = new MpegLayer3WaveFormat();

        #region IAudioConfigurationSource Members

        public string CodecPrivateData { get; protected set; }
        public event EventHandler ConfigurationComplete;

        #endregion

        #region IConfigurationSink Members

        public bool Parse(byte[] buffer, int index, int length)
        {
            if (length < 10)
                return false;

            if (!_frameHeader.Parse(buffer, index, length, true))
                return false;

            Configure(_frameHeader);

            return true;
        }

        #endregion

        public void Configure(Mp3FrameHeader frameHeader)
        {
            _waveFormat.nChannels = frameHeader.ChannelMode == 3 ? (ushort)1 : (ushort)2;
            _waveFormat.nSamplesPerSec = (uint)frameHeader.SampleRate;
            _waveFormat.nAvgBytesPerSec = (uint)frameHeader.Bitrate / 8;
            _waveFormat.nBlockSize = (ushort)frameHeader.FrameLength;

            var cpd = _waveFormat.ToCodecPrivateData();

            CodecPrivateData = cpd;

            var h = ConfigurationComplete;

            if (null != h)
                h(this, EventArgs.Empty);
        }
    }
}

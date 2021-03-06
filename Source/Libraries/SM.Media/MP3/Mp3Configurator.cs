// -----------------------------------------------------------------------
//  <copyright file="Mp3Configurator.cs" company="Henric Jungheim">
//  Copyright (c) 2012-2015.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012-2015 Henric Jungheim <software@henric.org>
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

using SM.Media.Audio;
using SM.Media.Configuration;
using SM.Media.Content;
using SM.Media.Metadata;
using SM.Media.Mmreg;

namespace SM.Media.MP3
{
    public sealed class Mp3Configurator : ConfiguratorBase, IAudioConfigurator, IFrameParser
    {
        readonly Mp3FrameHeader _frameHeader = new Mp3FrameHeader();

        public Mp3Configurator(IMediaStreamMetadata mediaStreamMetadata, string streamDescription = null)
            : base(ContentTypes.Mp3, mediaStreamMetadata)
        {
            StreamDescription = streamDescription;
        }

        #region IAudioConfigurator Members

        public AudioFormat Format
        {
            get { return AudioFormat.Mp3; }
        }

        public int SamplingFrequency { get; private set; }
        public int Channels { get; private set; }

        public void Configure(IAudioFrameHeader frameHeader)
        {
            var mp3FrameHeader = (Mp3FrameHeader)frameHeader;

            var waveFormat = new MpegLayer3WaveFormat
            {
                nChannels = (ushort)mp3FrameHeader.Channels,
                nSamplesPerSec = (uint)frameHeader.SamplingFrequency,
                nAvgBytesPerSec = (uint)mp3FrameHeader.Bitrate / 8,
                nBlockSize = (ushort)frameHeader.FrameLength
            };

            var cpd = waveFormat.ToCodecPrivateData();

            CodecPrivateData = cpd;

            Channels = waveFormat.nChannels;
            SamplingFrequency = frameHeader.SamplingFrequency;
            Bitrate = mp3FrameHeader.Bitrate;
            Name = frameHeader.Name;

            SetConfigured();
        }

        #endregion

        #region IFrameParser Members

        public int FrameLength
        {
            get { return _frameHeader.FrameLength; }
        }

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
    }
}

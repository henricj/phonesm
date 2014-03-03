// -----------------------------------------------------------------------
//  <copyright file="AacConfigurator.cs" company="Henric Jungheim">
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
using SM.Media.Audio;
using SM.Media.Configuration;
using SM.Media.Mmreg;

namespace SM.Media.AAC
{
    public sealed class AacConfigurator : ConfiguratorBase, IAudioConfigurationSource, IFrameParser, IAudioConfigurator
    {
        readonly AacFrameHeader _frameHeader = new AacFrameHeader();

        public AacConfigurator(string streamDescription = null)
        {
            StreamDescription = streamDescription;
        }

        #region IAudioConfigurationSource Members

        public AudioFormat Format
        {
            get { return AacDecoderSettings.Parameters.UseRawAac ? AudioFormat.AacRaw : AudioFormat.AacAdts; }
        }

        public int SamplingFrequency { get; private set; }
        public int Channels { get; private set; }

        #endregion

        #region IAudioConfigurator Members

        public void Configure(IAudioFrameHeader frameHeader)
        {
            var aacFrameHeader = (AacFrameHeader)frameHeader;

            CodecPrivateData = BuildCodecPrivateData(aacFrameHeader);

            Name = frameHeader.Name;
            Channels = aacFrameHeader.ChannelConfig;
            SamplingFrequency = frameHeader.SamplingFrequency;

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
            if (!_frameHeader.Parse(buffer, index, length, true))
                return false;

            Configure(_frameHeader);

            return true;
        }

        #endregion

        static string BuildCodecPrivateData(AacFrameHeader aacFrameHeader)
        {
            var factory = AacDecoderSettings.Parameters.CodecPrivateDataFactory;

            if (null != factory)
                return factory(aacFrameHeader);

            WaveFormatEx w;

            var waveFormatEx = AacDecoderSettings.Parameters.ConfigurationFormat;

            switch (waveFormatEx)
            {
                case AacDecoderParameters.WaveFormatEx.RawAac:
                    if (!AacDecoderSettings.Parameters.UseRawAac)
                        throw new NotSupportedException("AacDecoderSettings.Parameters.UseRawAac must be enabled when using AacDecoderParameters.WaveFormatEx.RawAac");

                    w = new RawAacWaveInfo
                        {
                            nChannels = aacFrameHeader.ChannelConfig,
                            nSamplesPerSec = (uint)aacFrameHeader.SamplingFrequency,
                            nAvgBytesPerSec = (uint)(aacFrameHeader.Duration.TotalSeconds <= 0 ? 0 : aacFrameHeader.FrameLength / aacFrameHeader.Duration.TotalSeconds),
                            pbAudioSpecificConfig = aacFrameHeader.AudioSpecificConfig
                        };

                    break;
                case AacDecoderParameters.WaveFormatEx.HeAac:
                    w = new HeAacWaveInfo
                        {
                            wPayloadType = (ushort)(AacDecoderSettings.Parameters.UseRawAac ? HeAacWaveInfo.PayloadType.Raw : HeAacWaveInfo.PayloadType.ADTS),
                            nChannels = aacFrameHeader.ChannelConfig,
                            nSamplesPerSec = (uint)aacFrameHeader.SamplingFrequency,
                            pbAudioSpecificConfig = aacFrameHeader.AudioSpecificConfig
                        };

                    break;
                default:
                    throw new NotSupportedException("Unknown WaveFormatEx type: " + waveFormatEx);
            }

            return w.ToCodecPrivateData();
        }
    }
}

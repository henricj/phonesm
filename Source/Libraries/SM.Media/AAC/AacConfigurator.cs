// -----------------------------------------------------------------------
//  <copyright file="AacConfigurator.cs" company="Henric Jungheim">
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
using SM.Media.Configuration;
using SM.Media.Mmreg;

namespace SM.Media.AAC
{
    class AacConfigurator : IAudioConfigurationSource, IFrameParser
    {
        readonly AacFrameHeader _frameHeader = new AacFrameHeader();

        #region IAudioConfigurationSource Members

        public string CodecPrivateData { get; protected set; }

        public event EventHandler ConfigurationComplete;

        #endregion

        #region IFrameParser Members

        public int FrameLength
        {
            get { return _frameHeader.FrameLength; }
        }

        public bool Parse(byte[] buffer, int index, int length)
        {
            if (!_frameHeader.Parse(buffer, index, length))
                return false;

            Configure(_frameHeader);

            return true;
        }

        #endregion

        public void Configure(AacFrameHeader frameHeader)
        {
#if false
            var w = new RawAacWaveInfo
                     {
                         nChannels = frameHeader.ChannelConfig,
                         wBitsPerSample = 16,
                         nSamplesPerSec = (uint)frameHeader.SamplingFrequency,
                         ObjectType = frameHeader.Profile + 1,
                         FrequencyIndex = frameHeader.FrequencyIndex,
                         ChannelConfiguration = frameHeader.ChannelConfig
                     };
#else
            var w = new HeAacWaveInfo
                    {
                        nChannels = frameHeader.ChannelConfig,
                        wBitsPerSample = 16,
                        nSamplesPerSec = (uint)frameHeader.SamplingFrequency,
                    };
#endif

            var cpd = w.ToCodecPrivateData();

            CodecPrivateData = cpd;

            var h = ConfigurationComplete;

            if (null != h)
                h(this, EventArgs.Empty);
        }
    }
}

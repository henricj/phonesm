//-----------------------------------------------------------------------
// <copyright file="AacConfigurator.cs" company="Henric Jungheim">
// Copyright (c) 2012.
// <author>Henric Jungheim</author>
// </copyright>
//-----------------------------------------------------------------------
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

namespace SM.Media.AAC
{
    class AacConfigurator : IAudioConfigurationSource, IConfigurationSink
    {
        static readonly int[] SamplingFrequencyTable =
            {
                96000,
                88200,
                64000,
                48000,
                44100,
                32000,
                24000,
                22050,
                16000,
                12000,
                11025,
                8000,
                7350
            };

        #region IAudioConfigurationSource Members

        public string CodecPrivateData { get; protected set; }
        public event EventHandler ConfigurationComplete;

        #endregion

        // http://wiki.multimedia.cx/index.php?title=ADTS

        #region IConfigurationSink Members

        public bool Parse(byte[] buffer, int index, int length)
        {
            var lastIndex = index + length;

            if (length < 7)
                return false;

            for (; ; )
            {
                var frameSync = buffer[index++];

                if (0xff == frameSync)
                    break;

                if (index >= lastIndex - 6)
                    return false;
            }

            var h1 = buffer[index++];

            var frameSync2 = (h1 >> 4) & 0x0f;

            if (0x0f != frameSync2)
                return false;

            var mpeg4Flag = 0 == (h1 & (1 << 3));

            var layer = (h1 >> 1) & 3;

            if (0 != layer)
                return false;

            var crcFlag = 0 == (h1 & 1);

            var h2 = buffer[index++];

            var profile = (h2 >> 6) & 3;

            var samplingIndex = (h2 >> 2) & 0x0f;

            var samplingFrequency = GetSamplingFrequency(samplingIndex);

            var privateStream = (h2 >> 1) & 1;

            var h3 = buffer[index++];

            var channelConfig = ((h2 & 1) << 2) | ((h3 >> 6) & 3);

            var originality = (h3 >> 5) & 1;

            var home = (h3 >> 4) & 1;

            var copyright = (h3 >> 3) & 1;

            var copyrightStart = (h3 >> 2) & 1;

            var h4 = buffer[index++];

            var h5 = buffer[index++];

            var frameLength = ((h3 & 3) << 11) | (h4 << 3) | ((h5 >> 5) & 7);

            var h6 = buffer[index++];

            var fullness = ((h5 & 0x1f) << 6) | ((h6 >> 2) & 0x3f);

            var frames = h6 & 3;

            if (crcFlag)
            {
                if (index + 2 > lastIndex)
                    return false;

                var crcHi = buffer[index++];
                var crcLo = buffer[index++];
            }

#if False
            var w = new RawAacWaveInfo
                    {
                        nChannels = (ushort)channelConfig,
                        nSamplesPerSec = (uint)samplingFrequency,
                        ObjectType = profile + 1,
                        FrequencyIndex = samplingIndex,
                        ChannelConfiguration = channelConfig
                    };
#else
            var w = new HeAacWaveInfo
                    {
                        nChannels = (ushort)channelConfig,
                        nSamplesPerSec = (uint)samplingFrequency,
                    };
#endif

            var cpd = w.ToCodecPrivateData();

            CodecPrivateData = cpd;

            var h = ConfigurationComplete;

            if (null != h)
                h(this, EventArgs.Empty);

            return true;
        }

        #endregion

        int GetSamplingFrequency(int samplingIndex)
        {
            if (samplingIndex < 0 || samplingIndex >= SamplingFrequencyTable.Length)
                return -1;

            return SamplingFrequencyTable[samplingIndex];
        }
    }
}

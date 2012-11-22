//-----------------------------------------------------------------------
// <copyright file="Mp3Configurator.cs" company="Henric Jungheim">
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

namespace SM.Media.MP3
{
    class Mp3Configurator : IAudioConfigurationSource, IConfigurationSink
    {
        static readonly short[] V1L1
            = new short[]
              {
                  0, 32, 64, 96, 128, 160, 192, 224,
                  256, 288, 320, 352, 384, 416, 448, -1,
              };

        static readonly short[] V1L2
            = new short[]
              {
                  0, 32, 48, 56, 64, 80, 96, 112,
                  128, 160, 192, 224, 256, 320, 384, -1,
              };

        static readonly short[] V1L3
            = new short[]
              {
                  0, 32, 40, 48, 56, 64, 80, 96,
                  112, 128, 160, 192, 224, 256, 320, -1,
              };

        static readonly short[] V2L1
            = new short[]
              {
                  0, 32, 48, 56, 64, 80, 96, 112,
                  128, 144, 160, 176, 192, 224, 256, -1,
              };

        static readonly short[] V2L23
            = new short[]
              {
                  0, 8, 16, 24, 32, 40, 48, 56,
                  64, 80, 96, 112, 128, 144, 160, -1,
              };

        static readonly int[] Rates = new[] { 11025, 12000, 8000 };
        static readonly int[] VersionRateMultiplier = new[] { 0, 4, 2, 1 };
        readonly MpegLayer3WaveFormat _waveFormat = new MpegLayer3WaveFormat();

        #region IAudioConfigurationSource Members

        public string CodecPrivateData { get; protected set; }
        public event EventHandler ConfigurationComplete;

        #endregion

        #region IConfigurationSink Members

        public bool Parse(byte[] buffer, int index, int length)
        {
            var lastIndex = index + length;

            if (length < 50)
                return false;

            for (; ; )
            {
                var frameSync = buffer[index++];

                if (0xff == frameSync)
                    break;

                if (index >= lastIndex - 8)
                    return false;
            }

            var h1 = buffer[index++];

            var frameSync2 = (h1 >> 5) & 7;

            if (7 != frameSync2)
                return false;

            var versionCode = (h1 >> 3) & 3;

            var version = 1;
            if (0 == (versionCode & 1))
                version = 0 == (versionCode & 2) ? 3 : 2;

            var layer = 4 - ((h1 >> 1) & 3);

            var crcFlag = 0 == (h1 & 1);

            var h2 = buffer[index++];

            var bitrateIndex = (h2 >> 4) & 0x0f;

            var bitrate = Bitrate(version, layer, bitrateIndex);

            if (bitrate < 1)
                return false;

            var sampleRateIndex = (h2 >> 2) & 3;

            var sampleRate = SampleRate(version, sampleRateIndex);

            var paddingFlag = 0 != ((h2 >> 1) & 1);

            var privateFlag = 0 != (h2 & 1);

            var h3 = buffer[index++];

            var channelMode = (h3 >> 6) & 3;

            var modeExtension = (h3 >> 4) & 3;

            var copyright = (h3 >> 3) & 1;

            var original = (h3 >> 2) & 1;

            var emphasis = h3 & 3;

            if (crcFlag)
            {
                var crcHi = buffer[index++];
                var crcLo = buffer[index++];
            }

            var frameSize = FrameSize(layer, bitrate, sampleRate, paddingFlag);

            _waveFormat.nChannels = channelMode == 3 ? (ushort)1 : (ushort)2;
            _waveFormat.nSamplesPerSec = (uint)sampleRate;
            _waveFormat.nAvgBytesPerSec = (uint)bitrate / 8;
            _waveFormat.nBlockSize = (ushort)frameSize;

            var cpd = _waveFormat.ToCodecPrivateData();

            CodecPrivateData = cpd;

            var h = ConfigurationComplete;

            if (null != h)
                h(this, EventArgs.Empty);

            return true;
        }

        #endregion

        int Bitrate(int version, int layer, int bitrateIndex)
        {
            short[] lookup = null;

            if (version > 1)
            {
                lookup = layer == 1 ? V2L1 : V2L23;
            }
            else
            {
                switch (layer)
                {
                    case 1:
                        lookup = V1L1;
                        break;
                    case 2:
                        lookup = V1L2;
                        break;
                    case 3:
                        lookup = V1L3;
                        break;
                }
            }

            if (null == lookup)
                return -1;

            return lookup[bitrateIndex] * 1000;
        }

        int SampleRate(int version, int sampleIndex)
        {
            if (sampleIndex >= Rates.Length)
                return 0;

            var multiplier = VersionRateMultiplier[version];

            var baseRate = Rates[sampleIndex];

            return multiplier * baseRate;
        }

        // http://www.mpgedit.org/mpgedit/mpeg_format/mpeghdr.htm

        int FrameSize(int layer, int bitrate, int sampleRate, bool paddingFlag)
        {
            if (1 == layer)
            {
                const int slotSize = 4;
                var padding = paddingFlag ? slotSize : 0;

                return (12 * bitrate / sampleRate + padding) * 4;
            }
            else
            {
                const int slotSize = 1;
                var padding = paddingFlag ? slotSize : 0;

                return 144 * bitrate / sampleRate + padding;
            }
        }
    }
}

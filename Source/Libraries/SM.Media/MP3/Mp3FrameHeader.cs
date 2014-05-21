// -----------------------------------------------------------------------
//  <copyright file="Mp3FrameHeader.cs" company="Henric Jungheim">
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
using SM.Media.Utility;

namespace SM.Media.MP3
{
    sealed class Mp3FrameHeader : IAudioFrameHeader
    {
        static readonly short[] V1L1 =
        {
            0, 32, 64, 96, 128, 160, 192, 224,
            256, 288, 320, 352, 384, 416, 448, -1
        };

        static readonly short[] V1L2 =
        {
            0, 32, 48, 56, 64, 80, 96, 112,
            128, 160, 192, 224, 256, 320, 384, -1
        };

        static readonly short[] V1L3 =
        {
            0, 32, 40, 48, 56, 64, 80, 96,
            112, 128, 160, 192, 224, 256, 320, -1
        };

        static readonly short[] V2L1 =
        {
            0, 32, 48, 56, 64, 80, 96, 112,
            128, 144, 160, 176, 192, 224, 256, -1
        };

        static readonly short[] V2L23 =
        {
            0, 8, 16, 24, 32, 40, 48, 56,
            64, 80, 96, 112, 128, 144, 160, -1
        };

        static readonly short[] SamplesV1 =
        {
            -1,
            384,
            1152,
            1152
        };

        static readonly short[] SamplesV2 =
        {
            -1,
            384,
            1152,
            576
        };

        static readonly int[] Rates = { 11025, 12000, 8000 };
        static readonly int[] VersionRateMultiplier = { 0, 4, 2, 1 };

        static readonly string[] VersionName =
        {
            "MPEG Version 2.5",
            "Reserved01",
            "MPEG Version 2 (ISO/IEC 13818-3)",
            "MPEG Version 1 (ISO/IEC 11172-3)"
        };

        static readonly string[] LayerName =
        {
            "Reserved00",
            "Layer III",
            "Layer II",
            "Layer I"
        };

        public int ChannelMode { get; private set; }
        public int Bitrate { get; private set; }
        public int Channels { get; private set; }

        public int? MarkerIndex { get; private set; }

        public int? EndIndex
        {
            get
            {
                if (!MarkerIndex.HasValue)
                    return null;

                return MarkerIndex.Value + FrameLength;
            }
        }

        #region IAudioFrameHeader Members

        public int FrameLength { get; private set; }
        public int HeaderLength { get; private set; }
        public int HeaderOffset { get; private set; }
        public int SamplingFrequency { get; private set; }
        public TimeSpan Duration { get; private set; }

        public string Name { get; private set; }

        public bool Parse(byte[] buffer, int index, int length, bool verbose = false)
        {
            MarkerIndex = null;

            HeaderOffset = 0;

            if (length < 4)
                return false;

            var index0 = index;
            var lastIndex = index + length;

            // http://www.mpgedit.org/mpgedit/mpeg_format/mpeghdr.htm

            byte h1;

            var markerIndex = -1;

            for (; ; )
            {
                for (; ; )
                {
                    if (index + 4 > lastIndex)
                        return false;

                    markerIndex = index;

                    var frameSync = buffer[index++];

                    if (0xff == frameSync)
                        break;
                }

                if (index + 3 > lastIndex)
                    return false;

                h1 = buffer[index++];

                var frameSync2 = (h1 >> 5) & 7;

                if (7 == frameSync2)
                    break;
            }

            HeaderOffset = index - index0 - 2;

            if (HeaderOffset < 0)
                return false;

            MarkerIndex = markerIndex;

            var versionCode = (h1 >> 3) & 3;

            var version = 1;
            if (0 == (versionCode & 1))
                version = 0 == (versionCode & 2) ? 3 : 2;

            var layerCode = ((h1 >> 1) & 3);

            var layer = 4 - layerCode;

            var crcFlag = 0 == (h1 & 1);

            HeaderLength = crcFlag ? 6 : 4;

            var h2 = buffer[index++];

            var bitrateIndex = (h2 >> 4) & 0x0f;

            var bitRate = GetBitrate(version, layer, bitrateIndex);

            if (bitRate < 1)
                return false;

            var sampleRateIndex = (h2 >> 2) & 3;

            var sampleRate = GetSampleRate(version, sampleRateIndex);

            if (sampleRate <= 0)
                return false;

            var paddingFlag = 0 != ((h2 >> 1) & 1);

            var privateFlag = 0 != (h2 & 1);

            var h3 = buffer[index++];

            var channelMode = (h3 >> 6) & 3;

            var modeExtension = (h3 >> 4) & 3;

            var copyright = (h3 >> 3) & 1;

            var original = (h3 >> 2) & 1;

            var emphasis = h3 & 3;

            if (ChannelMode != channelMode)
            {
                ChannelMode = channelMode;
                Name = null;
            }

            var channels = channelMode == 3 ? 1 : 2;
            if (Channels != channels)
            {
                Channels = channels;
                Name = null;
            }

            if (Bitrate != bitRate)
            {
                Bitrate = bitRate;
                Name = null;
            }

            if (SamplingFrequency != sampleRate)
            {
                SamplingFrequency = sampleRate;
                Name = null;
            }

            FrameLength = GetFrameSize(version, layer, bitRate, sampleRate, paddingFlag);
            Duration = GetDuration(version, layer, sampleRate);

            if (string.IsNullOrEmpty(Name))
            {
                Name = string.Format("MP3 {0}, {1} sample {2}kHz bitrate {3}kHz {4} channels",
                    VersionName[versionCode], LayerName[layerCode], sampleRate / 1000.0, bitRate / 1000.0, Channels);
            }
#if DEBUG
            if (verbose)
            {
                Debug.WriteLine("Configuration MP3 Frame: {0}, {1} sample {2}kHz bitrate {3}kHz channel mode {4}",
                    VersionName[versionCode], LayerName[layerCode], sampleRate / 1000.0, bitRate / 1000.0, channelMode);
            }
#endif
            return true;
        }

        #endregion

        static int GetBitrate(int version, int layer, int bitrateIndex)
        {
            short[] lookup = null;

            if (version > 1)
                lookup = layer == 1 ? V2L1 : V2L23;
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

        static int GetSampleRate(int version, int sampleIndex)
        {
            if (sampleIndex >= Rates.Length)
                return -1;

            var multiplier = VersionRateMultiplier[version];

            var baseRate = Rates[sampleIndex];

            return multiplier * baseRate;
        }

        static int GetFrameSize(int version, int layer, int bitrate, int sampleRate, bool paddingFlag)
        {
            int samplesPerFrame8;

            switch (layer)
            {
                case 1:
                    samplesPerFrame8 = 12;
                    break;
                case 3:
                    samplesPerFrame8 = 1 == version ? 144 : 72;
                    break;
                default:
                    samplesPerFrame8 = 144;
                    break;
            }

            var slotSize = 1 == layer ? 4 : 1;

            var padding = paddingFlag ? slotSize : 0;

            return (samplesPerFrame8 * bitrate / sampleRate + padding) * slotSize;
        }

        TimeSpan GetDuration(int version, int layer, int sampleRate)
        {
            var samples = (1 == version) ? SamplesV1[layer] : SamplesV2[layer];

            return FullResolutionTimeSpan.FromSeconds(samples / (double)sampleRate);
        }
    }
}

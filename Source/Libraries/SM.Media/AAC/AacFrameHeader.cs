// -----------------------------------------------------------------------
//  <copyright file="AacFrameHeader.cs" company="Henric Jungheim">
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SM.Media.Audio;
using SM.Media.Utility;

namespace SM.Media.AAC
{
    public sealed class AacFrameHeader : IAudioFrameHeader
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

        static readonly Dictionary<int, string> ProfileNames =
            new Dictionary<int, string>
            {
                // http://en.wikipedia.org/wiki/MPEG-4_Part_3#MPEG-4_Audio_Object_Types
                // Note that we are using profile, not the object type (profile + 1).
                { 0, "AAC Main" },
                { 1, "AAC LC (Low Complexity)" },
                { 2, "AAC SSR (Scalable Sample Rate)" },
                { 3, "AAC LTP (Long Term Prediction)" },
                { 4, "SBR (Spectral Band Replication)" },
                { 5, "AAC Scalable" },
                { 6, "TwinVQ" },
                { 7, "CELP (Code Excited Linear Prediction)" },
                { 8, "HXVC (Harmonic Vector eXcitation Coding)" },
                { 11, "TTSI (Text-To-Speech Interface)" },
                { 12, "Main Synthesis" },
                { 13, "Wavetable Synthesis" },
                { 14, "General MIDI" },
                { 15, "Algorithmic Synthesis and Audio Effects" },
                { 16, "ER (Error Resilient) AAC LC" },
                { 18, "ER AAC LTP" },
                { 19, "ER AAC Scalable" },
                { 20, "ER TwinVQ" },
                { 21, "ER BSAC (Bit-Sliced Arithmetic Coding)" },
                { 22, "ER AAC LD (Low Delay)" },
                { 23, "ER CELP" },
                { 24, "ER HVXC" },
                { 25, "ER HILN (Harmonic and Individual Lines plus Noise)" },
                { 26, "ER Parametric" },
                { 27, "SSC (SinuSoidal Coding)" },
                { 28, "PS (Parametric Stereo)" },
                { 29, "MPEG Surround" },
                { 31, "Layer-1" },
                { 32, "Layer-2" },
                { 33, "Layer-3 (MP3)" },
                { 34, "DST (Direct Stream Transfer)" },
                { 35, "ALS (Audio Lossless)" },
                { 36, "SLS (Scalable LosslesS)" },
                { 37, "SLS non-core" },
                { 38, "ER AAC ELD (Enhanced Low Delay)" },
                { 39, "SMR (Symbolic Music Representation) Simple" },
                { 40, "SMR Main" },
                { 41, "USAC (Unified Speech and Audio Coding) (no SBR)" },
                { 42, "SAOC (Spatial Audio Object Coding)" },
                { 43, "LD MPEG Surround" },
                { 44, "USAC" },
            };

        byte[] _audioSpecificConfig;
        int _frames;

        public int Profile { get; private set; }
        public int Layer { get; private set; }
        public int FrequencyIndex { get; private set; }
        public ushort ChannelConfig { get; private set; }

        public bool CrcFlag { get; set; }

        public ICollection<byte> AudioSpecificConfig
        {
            get
            {
                if (null == _audioSpecificConfig)
                    _audioSpecificConfig = AacDecoderSettings.Parameters.AudioSpecificConfigFactory(this).ToArray();

                return _audioSpecificConfig;
            }

            set { _audioSpecificConfig = value.ToArray(); }
        }

        public int Bitrate { get; private set; }

        #region IAudioFrameHeader Members

        public int HeaderLength
        {
            get { return CrcFlag ? 9 : 7; }
        }

        public int HeaderOffset { get; private set; }

        public TimeSpan Duration
        {
            get { return FullResolutionTimeSpan.FromSeconds(_frames * 1024.0 / SamplingFrequency); }
        }

        public int SamplingFrequency { get; private set; }
        public int FrameLength { get; private set; }
        public string Name { get; private set; }

        public bool Parse(byte[] buffer, int index, int length, bool verbose = false)
        {
            // http://wiki.multimedia.cx/index.php?title=ADTS

            var index0 = index;
            var lastIndex = index + length;

            HeaderOffset = 0;

            if (length < 7)
                return false;

            byte h1;

            for (; ; )
            {
                for (; ; )
                {
                    if (index + 7 > lastIndex)
                        return false;

                    var frameSync = buffer[index++];

                    if (0xff == frameSync)
                        break;
                }

                if (index + 6 > lastIndex)
                    return false;

                h1 = buffer[index++];

                var frameSync2 = (h1 >> 4) & 0x0f;

                if (0x0f == frameSync2)
                    break;
            }

            HeaderOffset = index - index0 - 2;

            if (HeaderOffset < 0)
                return false;

            var mpeg4Flag = 0 == (h1 & (1 << 3));

            Layer = (h1 >> 1) & 3;

            if (0 != Layer)
            {
                Debug.WriteLine("AacFrameHeader.Parse() unknown layer: " + Layer);
                //return false;
            }

            CrcFlag = 0 == (h1 & 1);

            var h2 = buffer[index++];

            Profile = (h2 >> 6) & 3;

            FrequencyIndex = (h2 >> 2) & 0x0f;

            SamplingFrequency = GetSamplingFrequency(FrequencyIndex);

            if (SamplingFrequency <= 0)
                return false;

            var privateStream = (h2 >> 1) & 1;

            var h3 = buffer[index++];

            ChannelConfig = (ushort)(((h2 & 1) << 2) | ((h3 >> 6) & 3));

            var originality = (h3 >> 5) & 1;

            var home = (h3 >> 4) & 1;

            var copyright = (h3 >> 3) & 1;

            var copyrightStart = (h3 >> 2) & 1;

            var h4 = buffer[index++];

            var h5 = buffer[index++];

            FrameLength = ((h3 & 3) << 11) | (h4 << 3) | ((h5 >> 5) & 7);

            if (FrameLength < 1)
                return false;

            var h6 = buffer[index++];

            var fullness = ((h5 & 0x1f) << 6) | ((h6 >> 2) & 0x3f);

            _frames = 1 + (h6 & 3);

            if (_frames < 1)
                return false;

            if (CrcFlag)
            {
                if (index + 2 > lastIndex)
                    return false;

                var crcHi = buffer[index++];
                var crcLo = buffer[index++];
            }

            if (string.IsNullOrEmpty(Name))
                Name = string.Format("{0}, {1}kHz {2} channels", GetProfileName(), SamplingFrequency / 1000.0, ChannelConfig);

#if DEBUG
            if (verbose)
            {
                Debug.WriteLine("Configuration AAC layer {0} profile \"{1}\" channels {2} sampling {3}kHz length {4} CRC {5}",
                    Layer, Name, ChannelConfig, SamplingFrequency / 1000.0, FrameLength, CrcFlag);
            }
#endif
            return true;
        }

        #endregion

        string GetProfileName()
        {
            string name;

            if (ProfileNames.TryGetValue(Profile, out name))
                return name;

            return "Profile" + Profile;
        }

        int GetSamplingFrequency(int samplingIndex)
        {
            if (samplingIndex < 0 || samplingIndex >= SamplingFrequencyTable.Length)
                return -1;

            return SamplingFrequencyTable[samplingIndex];
        }
    }
}

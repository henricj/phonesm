// -----------------------------------------------------------------------
//  <copyright file="AacDecoderParameters.cs" company="Henric Jungheim">
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

namespace SM.Media.AAC
{
    public class AacDecoderParameters
    {
        #region WaveFormatEx enum

        public enum WaveFormatEx
        {
            RawAac,
            HeAac
        };

        #endregion

        Func<AacFrameHeader, ICollection<byte>> _audioSpecificConfigFactory;
        bool _useParser;

        public AacDecoderParameters()
        {
            ConfigurationFormat = WaveFormatEx.HeAac;
        }

        public bool UseParser
        {
            get { return _useParser || UseRawAac; }
            set { _useParser = value; }
        }

        public bool UseRawAac { get; set; }

        public WaveFormatEx ConfigurationFormat { get; set; }

        public Func<AacFrameHeader, ICollection<byte>> AudioSpecificConfigFactory
        {
            get
            {
                if (null == _audioSpecificConfigFactory)
                    return AacAudioSpecificConfig.DefaultAudioSpecificConfigFactory;

                return _audioSpecificConfigFactory;
            }
            set { _audioSpecificConfigFactory = value; }
        }

        /// <summary>
        ///     Optional CodecPrivateData factory.  If set, AudioSpecificConfigFactory will
        ///     be ignored.
        /// </summary>
        public Func<AacFrameHeader, string> CodecPrivateDataFactory { get; set; }
    }
}

// -----------------------------------------------------------------------
//  <copyright file="AacAudioSpecificConfig.cs" company="Henric Jungheim">
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

using System.Diagnostics;

namespace SM.Media.AAC
{
    public static class AacAudioSpecificConfig
    {
        /// <summary>
        ///     AAC Main isn't really used anymore.  Sometimes the encoder may
        ///     say "1" when it really should say "2" (AAC LC) or "5" (HE AAC).
        ///     See http://en.wikipedia.org/wiki/MPEG-4_Part_3#MPEG-4_Audio_Object_Types
        /// </summary>
        public static int? RemapObjectType1 { get; set; }

        public static byte[] DefaultAudioSpecificConfigFactory(AacFrameHeader aacFrameHeader)
        {
            var objectType = aacFrameHeader.Profile + 1;

            if (1 == objectType && RemapObjectType1.HasValue)
            {
                objectType = RemapObjectType1.Value;
                Debug.WriteLine("AacConfigurator.AudioSpecificConfig: Changing AAC object type from 1 to {0}.", objectType);
            }

            return new[]
                   {
                       (byte) ((objectType << 3) | ((aacFrameHeader.FrequencyIndex >> 1) & 0x07)),
                       (byte) ((aacFrameHeader.FrequencyIndex << 7) | (aacFrameHeader.ChannelConfig << 3))
                   };
        }
    }
}

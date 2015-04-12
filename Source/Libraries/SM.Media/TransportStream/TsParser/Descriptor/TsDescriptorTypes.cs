// -----------------------------------------------------------------------
//  <copyright file="TsDescriptorTypes.cs" company="Henric Jungheim">
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

using System;
using SM.Media.TransportStream.TsParser.Descriptor;

namespace SM.Media.TransportStream.TsParser
{
    public static class TsDescriptorTypes
    {
        // ISO/IEC 13818-1:2007 Table 2-45 – Program and program element descriptors
        static readonly TsDescriptorType[] DescriptorTypes =
        {
            new TsDescriptorType(0, "Reserved"),
            new TsDescriptorType(1, "Reserved"),
            new TsDescriptorType(2, "Video stream"),
            new TsDescriptorType(3, "Audio stream"),
            new TsDescriptorType(4, "Hierarchy"),
            new TsDescriptorType(5, "Registration"),
            new TsDescriptorType(6, "Data stream alignment"),
            new TsDescriptorType(7, "Target background grid"),
            new TsDescriptorType(8, "Video window"),
            new TsDescriptorType(9, "CA"),
            new TsDescriptorType(10, "ISO 639 language"),
            new TsDescriptorType(11, "System clock"),
            new TsDescriptorType(12, "Multiplex buffer utilization"),
            new TsDescriptorType(13, "Copyright"),
            new TsDescriptorType(14, "Maximum bitrate"),
            new TsDescriptorType(15, "Private data indicator"),
            new TsDescriptorType(16, "Smoothing buffer"),
            new TsDescriptorType(17, "STD"),
            new TsDescriptorType(18, "IBP"),
            new TsDescriptorType(19, "ISO/IEC 13818-6"),
            new TsDescriptorType(20, "Defined in ISO/IEC 13818-6"),
            new TsDescriptorType(21, "Defined in ISO/IEC 13818-6"),
            new TsDescriptorType(22, "Defined in ISO/IEC 13818-6"),
            new TsDescriptorType(23, "Defined in ISO/IEC 13818-6"),
            new TsDescriptorType(24, "Defined in ISO/IEC 13818-6"),
            new TsDescriptorType(25, "Defined in ISO/IEC 13818-6"),
            new TsDescriptorType(26, "Defined in ISO/IEC 13818-6"),
            new TsDescriptorType(27, "MPEG-4 video"),
            new TsDescriptorType(28, "MPEG-4 audio"),
            new TsDescriptorType(29, "IOD"),
            new TsDescriptorType(30, "SL"),
            new TsDescriptorType(31, "FMC"),
            new TsDescriptorType(32, "External ES ID"),
            new TsDescriptorType(33, "MuxCode"),
            new TsDescriptorType(34, "FmxBufferSize"),
            new TsDescriptorType(35, "Multiplexbuffer"),
            new TsDescriptorType(36, "Content labeling"),
            new TsDescriptorType(37, "Metadata pointer"),
            new TsDescriptorType(38, "Metadata"),
            new TsDescriptorType(39, "Metadata STD"),
            new TsDescriptorType(40, "AVC video"),
            new TsDescriptorType(41, "IPMP (defined in ISO/IEC 13818-11, MPEG-2 IPMP)"),
            new TsDescriptorType(42, "AVC timing and HRD"),
            new TsDescriptorType(43, "MPEG-2 AAC audio"),
            new TsDescriptorType(44, "FlexMuxTiming"),
            new TsDescriptorType(45, "ITU-T Rec. H.222.0 | ISO/IEC 13818-1 Reserved"),
            new TsDescriptorType(46, "ITU-T Rec. H.222.0 | ISO/IEC 13818-1 Reserved"),
            new TsDescriptorType(44, "ITU-T Rec. H.222.0 | ISO/IEC 13818-1 Reserved"),
            new TsDescriptorType(47, "ITU-T Rec. H.222.0 | ISO/IEC 13818-1 Reserved"),
            new TsDescriptorType(48, "ITU-T Rec. H.222.0 | ISO/IEC 13818-1 Reserved"),
            new TsDescriptorType(49, "ITU-T Rec. H.222.0 | ISO/IEC 13818-1 Reserved"),
            new TsDescriptorType(50, "ITU-T Rec. H.222.0 | ISO/IEC 13818-1 Reserved"),
            new TsDescriptorType(51, "ITU-T Rec. H.222.0 | ISO/IEC 13818-1 Reserved"),
            new TsDescriptorType(52, "ITU-T Rec. H.222.0 | ISO/IEC 13818-1 Reserved"),
            new TsDescriptorType(53, "ITU-T Rec. H.222.0 | ISO/IEC 13818-1 Reserved"),
            new TsDescriptorType(54, "ITU-T Rec. H.222.0 | ISO/IEC 13818-1 Reserved"),
            new TsDescriptorType(55, "ITU-T Rec. H.222.0 | ISO/IEC 13818-1 Reserved"),
            new TsDescriptorType(56, "ITU-T Rec. H.222.0 | ISO/IEC 13818-1 Reserved"),
            new TsDescriptorType(57, "ITU-T Rec. H.222.0 | ISO/IEC 13818-1 Reserved"),
            new TsDescriptorType(58, "ITU-T Rec. H.222.0 | ISO/IEC 13818-1 Reserved"),
            new TsDescriptorType(59, "ITU-T Rec. H.222.0 | ISO/IEC 13818-1 Reserved"),
            new TsDescriptorType(60, "ITU-T Rec. H.222.0 | ISO/IEC 13818-1 Reserved"),
            new TsDescriptorType(61, "ITU-T Rec. H.222.0 | ISO/IEC 13818-1 Reserved"),
            new TsDescriptorType(62, "ITU-T Rec. H.222.0 | ISO/IEC 13818-1 Reserved"),
            new TsDescriptorType(63, "ITU-T Rec. H.222.0 | ISO/IEC 13818-1 Reserved")
        };

        static TsDescriptorTypes()
        {
            Validate();
        }

        public static void Validate()
        {
            for (var i = 0; i < DescriptorTypes.Length; ++i)
            {
                var descriptor = DescriptorTypes[i];

                if (descriptor.Code != i)
                    throw new InvalidOperationException("Descriptor type mismatch " + (int)descriptor.Code + " != " + i);
            }
        }

        public static TsDescriptorType GetDescriptorType(byte code)
        {
            if (code < DescriptorTypes.Length)
                return DescriptorTypes[code];

            return null;
        }
    }
}

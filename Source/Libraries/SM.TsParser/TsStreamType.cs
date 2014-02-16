// -----------------------------------------------------------------------
//  <copyright file="TsStreamType.cs" company="Henric Jungheim">
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
using System.Linq;

namespace SM.TsParser
{
    public class TsStreamType : IEquatable<TsStreamType>
    {
        #region StreamContents enum

        public enum StreamContents
        {
            Unknown = 0,
            Audio,
            Video,
            Other,
            Private,
            Reserved
        }

        #endregion

        public static readonly byte H262StreamType = 0x02;
        public static readonly byte Mp3Iso11172 = 0x03;
        public static readonly byte Mp3Iso13818 = 0x04;
        public static readonly byte AacStreamType = 0x0f;
        public static readonly byte H264StreamType = 0x1b;
        public static readonly byte Ac3StreamType = 0x81;

        // Table 2-34 Stream type assignments
        // ISO/IEC 13818-1:2007/Amd.3:2009 (E)
        // Rec. ITU-T H.222.0 (2006)/Amd.3 (03/2009)
        static readonly Dictionary<byte, TsStreamType> Types =
            new[]
            {
                new TsStreamType(0x00, StreamContents.Reserved, "ITU-T|ISO/IEC Reserved"),
                new TsStreamType(0x01, StreamContents.Video, "ISO/IEC 11172-2 Video"),
                new TsStreamType(H262StreamType, StreamContents.Video,
                    "ITU-T Rec. H.262 | ISO/IEC 13818-2 Video or ISO/IEC 11172-2 constrained parameter video stream")
                {
                    FileExtension = ".h262"
                },
                new TsStreamType(Mp3Iso11172, StreamContents.Audio, "ISO/IEC 11172-3 Audio")
                {
                    FileExtension = ".mp3"
                },
                new TsStreamType(Mp3Iso13818, StreamContents.Audio, "ISO/IEC 13818-3 Audio")
                {
                    FileExtension = ".mp3"
                },
                new TsStreamType(0x05, StreamContents.Other, "ITU-T Rec. H.222.0 | ISO/IEC 13818-1 private_sections"),
                new TsStreamType(0x06, StreamContents.Other, "ITU-T Rec. H.222.0 | ISO/IEC 13818-1 PES packets containing private data"),
                new TsStreamType(0x07, StreamContents.Other, "ISO/IEC 13522 MHEG"),
                new TsStreamType(0x08, StreamContents.Other, "ITU-T Rec. H.222.0 | ISO/IEC 13818-1 Annex A DSM-CC"),
                new TsStreamType(0x09, StreamContents.Other, "ITU-T Rec. H.222.1"),
                new TsStreamType(0x0A, StreamContents.Other, "ISO/IEC 13818-6 type A"),
                new TsStreamType(0x0B, StreamContents.Other, "ISO/IEC 13818-6 type B"),
                new TsStreamType(0x0C, StreamContents.Other, "ISO/IEC 13818-6 type C"),
                new TsStreamType(0x0D, StreamContents.Other, "ISO/IEC 13818-6 type D"),
                new TsStreamType(0x0E, StreamContents.Other, "ITU-T Rec. H.222.0 | ISO/IEC 13818-1 auxiliary"),
                new TsStreamType(AacStreamType, StreamContents.Audio, "ISO/IEC 13818-7 Audio with ADTS transport syntax")
                {
                    FileExtension = ".aac"
                },
                new TsStreamType(0x10, StreamContents.Other, "ISO/IEC 14496-2 Visual"),
                new TsStreamType(0x11, StreamContents.Audio, "ISO/IEC 14496-3 Audio with the LATM transport syntax as defined in ISO/IEC 14496-3"),
                new TsStreamType(0x12, StreamContents.Other, "ISO/IEC 14496-1 SL-packetized stream or FlexMux stream carried in PES packets"),
                new TsStreamType(0x13, StreamContents.Other, "ISO/IEC 14496-1 SL-packetized stream or FlexMux stream carried in ISO/IEC 14496_sections"),
                new TsStreamType(0x14, StreamContents.Other, "ISO/IEC 13818-6 Synchronized Download Protocol"),
                new TsStreamType(0x15, StreamContents.Other, "Metadata carried in PES packets"),
                new TsStreamType(0x16, StreamContents.Other, "Metadata carried in metadata_sections"),
                new TsStreamType(0x17, StreamContents.Other, "Metadata carried in ISO/IEC 13818-6 Data Carousel"),
                new TsStreamType(0x18, StreamContents.Other, "Metadata carried in ISO/IEC 13818-6 Object Carousel"),
                new TsStreamType(0x19, StreamContents.Other, "Metadata carried in ISO/IEC 13818-6 Synchronized Download Protocol"),
                new TsStreamType(0x1A, StreamContents.Other, "IPMP stream (defined in ISO/IEC 13818-11, MPEG-2 IPMP)"),
                new TsStreamType(H264StreamType, StreamContents.Video,
                    "AVC video stream conforming to one or more profiles defined in Annex A of ITU-T Rec. H.264 | ISO/IEC 14496-10 or AVC video sub-bitstream as defined in 2.1.78")
                {
                    FileExtension = ".h264"
                },
                new TsStreamType(0x1C, StreamContents.Audio, "ISO/IEC 14496-3 Audio, without using any additional transport syntax, such as DST, ALS and SLS"),
                new TsStreamType(0x1D, StreamContents.Other, "ISO/IEC 14496-17 Text"),
                new TsStreamType(0x1E, StreamContents.Video, "Auxiliary video stream as defined in ISO/IEC 23002-3"),
                new TsStreamType(0x1F, StreamContents.Video,
                    "SVC video sub-bitstream of an AVC video stream conforming to one or more profiles defined in Annex G of ITU-T Rec. H.264 | ISO/IEC 14496-10"),
                new TsStreamType(0x7F, StreamContents.Other, "IPMP stream"),
                
                // ATSC (this should be configurable, but we hard-code it for now)
                new TsStreamType(Ac3StreamType, StreamContents.Audio, "Dolby AC-3")
                {
                    FileExtension = ".ac3"
                }
            }.ToDictionary(v => v.StreamType);

        static readonly Dictionary<byte, TsStreamType> UnknownTypes = new Dictionary<byte, TsStreamType>();

        public TsStreamType(byte streamType, StreamContents contents, string description)
        {
            StreamType = streamType;
            Contents = contents;
            Description = description;
        }

        public byte StreamType { get; private set; }
        public StreamContents Contents { get; private set; }
        public string Description { get; private set; }
        public string FileExtension { get; private set; }

        #region IEquatable<TsStreamType> Members

        public bool Equals(TsStreamType other)
        {
            if (ReferenceEquals(null, other))
                return false;

            return StreamType == other.StreamType;
        }

        #endregion

        public static TsStreamType FindStreamType(byte streamType)
        {
            TsStreamType value;

            if (Types.TryGetValue(streamType, out value))
                return value;

            lock (UnknownTypes)
            {
                if (!UnknownTypes.TryGetValue(streamType, out value))
                {
                    var contents = StreamContents.Unknown;

                    if (streamType >= 0x80)
                        contents = StreamContents.Private;
                    else if (streamType >= 0x20 && streamType <= 0x7e)
                        contents = StreamContents.Reserved;

                    value = new TsStreamType(streamType, contents, string.Format("Stream type {0:X2} ({1})", streamType, contents));

                    UnknownTypes[streamType] = value;
                }
            }

            return value;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as TsStreamType);
        }

        public override int GetHashCode()
        {
            return StreamType;
        }

        public override string ToString()
        {
            return string.Format("0x{0:x2}/{1}/{2}", StreamType, Contents, Description);
        }
    }
}

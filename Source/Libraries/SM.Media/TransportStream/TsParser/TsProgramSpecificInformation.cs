// -----------------------------------------------------------------------
//  <copyright file="TsProgramSpecificInformation.cs" company="Henric Jungheim">
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

using SM.Media.TransportStream.TsParser.Utility;

namespace SM.Media.TransportStream.TsParser
{
    public abstract class TsProgramSpecificInformation
    {
        #region TsTableId enum

        public enum TsTableId : byte
        {
            program_association_section = 0x00,
            conditional_access_section = 0x01,
            TS_program_map_section = 0x02,
            TS_description_section = 0x03,
            ISO_IEC_14496_scene_description_section = 0x04,
            ISO_IEC_14496_object_descriptor_section = 0x05,
            Metadata_section = 0x06,
            IPMP_Control_Information_section = 0x07,
        }

        #endregion

        const int MinimumPsiSize = 4;
        const int CrcSize = 4;
        const int MaximumSectionLength = 1021;
        readonly byte _tableId;

        protected TsProgramSpecificInformation(TsTableId tableId)
        {
            _tableId = (byte)tableId;
        }

        internal void Add(TsPacket packet)
        {
            if (null == packet) // Ignore end-of-stream
                return;

            var i0 = packet.BufferOffset;
            var i = i0;
            var buffer = packet.Buffer;
            var length = packet.BufferLength;

            if (length < MinimumPsiSize)
                return;

            var pointer = buffer[i++];

            i += pointer;
            if (i + MinimumPsiSize >= i0 + length)
                return;

            var tableIdOffset = i;

            var table_id = buffer[i++];

            if (_tableId != table_id)
                return;

            var section_length = (buffer[i] << 8) | buffer[i + 1];
            i += 2;

            var section_syntax_indicator = 0 != (section_length & (1 << 15));

            if (0 != (section_length & (1 << 14)))
                return;

            section_length &= 0x0fff;

            if (section_length > MaximumSectionLength)
                return;

            if (section_length + i - i0 > length)
                return;

            var checksumLength = section_length + i - tableIdOffset;

            var validChecksum = Crc32Msb.Validate(buffer, tableIdOffset, checksumLength);

            if (!validChecksum)
                return;

            var sectionIndex = i;
            var sectionLength = section_length - CrcSize;

            ParseSection(packet, sectionIndex, sectionLength);
        }

        protected abstract void ParseSection(TsPacket packet, int offset, int length);
    }
}

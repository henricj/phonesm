// -----------------------------------------------------------------------
//  <copyright file="TsTransportStreamDescriptionTable.cs" company="Henric Jungheim">
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
    public class TsTransportStreamDescriptionTable : TsProgramSpecificInformation
    {
        const int MinimumDescriptionTableLength = 5;
        readonly ITsDescriptorFactory _descriptorFactory;

        public TsTransportStreamDescriptionTable(ITsDescriptorFactory descriptorFactory)
            : base(TsTableId.TS_description_section)
        {
            if (null == descriptorFactory)
                throw new ArgumentNullException(nameof(descriptorFactory));

            _descriptorFactory = descriptorFactory;
        }

        protected override void ParseSection(TsPacket packet, int offset, int length)
        {
            if (length < MinimumDescriptionTableLength)
                return;

            var i = offset;
            var buffer = packet.Buffer;
            var sectionEnd = i + length;

            i += 2; // Skip reserved

            var version_number = buffer[i++];

            var current_next_indicator = version_number & 1;

            version_number = (byte)((version_number >> 1) & 0x1f);

            var section_number = buffer[i++];

            var last_section_number = buffer[i++];

            var descriptors = _descriptorFactory.Parse(buffer, i, sectionEnd - i);
        }
    }
}

// -----------------------------------------------------------------------
//  <copyright file="TsPacketizedElementaryStream.cs" company="Henric Jungheim">
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
using SM.TsParser.Utility;

namespace SM.TsParser
{
    public class TsPacketizedElementaryStream
    {
        const int DefaultPacketSize = 4096;
        const int SystemClockHz = 90000;
        const double PtsTo100ns = TimeSpan.TicksPerSecond / (double)SystemClockHz;
        readonly IBufferPool _bufferPool;
        readonly Action<TsPesPacket> _handler;
        readonly ITsPesPacketPool _pesPacketPool;
        readonly TsStreamType _streamType;
        BufferInstance _bufferEntry;
        int _index;
        uint _length;
        uint _pid;
        RegisterExtender _pts;
        int _startIndex;
        byte _streamId;

        public TsPacketizedElementaryStream(IBufferPool bufferPool, ITsPesPacketPool pesPacketPool, Action<TsPesPacket> packetHandler, TsStreamType streamType, uint pid)
        {
            if (null == bufferPool)
                throw new ArgumentNullException("bufferPool");
            if (null == pesPacketPool)
                throw new ArgumentNullException("pesPacketPool");

            _bufferPool = bufferPool;
            _pesPacketPool = pesPacketPool;

            _streamType = streamType;
            _pid = pid;

            _handler = packetHandler;
        }

        public void Add(TsPacket packet)
        {
            if (null == _handler)
                return; // Don't bother parsing if we can't do anything with the results.

            if (null != packet)
            {
                if (packet.IsStart)
                {
                    if (0 == _length && _index > _startIndex)
                        Flush();

                    _index = _startIndex;

                    ParseHeader(packet);
                }

                if (null != _bufferEntry)
                {
                    if (_index + packet.BufferLength > _bufferEntry.Buffer.Length)
                    {
                        var requiredLength = _index - _startIndex + packet.BufferLength;

                        var newLength = Math.Max(requiredLength, 512);

                        if (_index < _bufferEntry.Buffer.Length / 2)
                            newLength *= 2;

                        var newBuffer = _bufferPool.Allocate(newLength);

                        Array.Copy(_bufferEntry.Buffer, _startIndex, newBuffer.Buffer, 0, _index - _startIndex);

                        _bufferPool.Free(_bufferEntry);

                        _bufferEntry = newBuffer;

                        _index -= _startIndex;
                        _startIndex = 0;
                    }

                    packet.CopyTo(_bufferEntry.Buffer, _index);
                    _index += packet.BufferLength;
                }
            }

            if (null == packet || _length > 0 && _index - _startIndex == _length)
                Flush();

            if (null == packet && null != _bufferEntry)
            {
                _bufferPool.Free(_bufferEntry);
                _bufferEntry = null;
            }

            if (null == packet && null != _handler)
                _handler(null); // Propagate end-of-stream
        }

        void ParseHeader(TsPacket packet)
        {
            var length = packet.BufferLength;

            if (length < 6)
                return;

            var i0 = packet.BufferOffset;
            var i = i0;

            var buffer = packet.Buffer;

            var packet_start_code_prefix = ((uint)buffer[i] << 16) | ((uint)buffer[i + 1] << 8) | buffer[i + 2];
            i += 3;

            if (1 != packet_start_code_prefix)
                return;

            _streamId = buffer[i++];

            var packet_length = ((uint)buffer[i] << 8) | buffer[i + 1];
            i += 2;

            var bufferLength = DefaultPacketSize;

            if (packet_length > 0)
            {
                _length = packet_length + (uint)(i - i0);
                bufferLength = (int)_length;
            }
            else
                _length = 0;

            // If we still have a buffer, make sure the size is reasonable.
            if (null != _bufferEntry && _bufferEntry.Buffer.Length - _startIndex < bufferLength)
            {
                _bufferPool.Free(_bufferEntry);
                _bufferEntry = null;
            }

            if (null == _bufferEntry)
            {
                _bufferEntry = _bufferPool.Allocate(bufferLength);
                _startIndex = 0;
            }

            _index = _startIndex;
        }

        void ParseNormalPesPacket()
        {
            if (null == _handler)
                return; // Don't bother parsing if we can't do anything with the results.

            var i = 6 + _startIndex;

            var buffer = _bufferEntry.Buffer;

            var x = buffer[i++];

            if (0x80 != (x & 0xc0))
                return;

            var PES_scrambling_control = (byte)((x >> 4) & 3);
            var PES_priority = 0 != (x & (1 << 3));
            var data_alignment_indicator = 0 != (x & (1 << 2));
            var copyright = 0 != (x & (1 << 1));
            var original_or_copy = 0 != (x & 1);

            x = buffer[i++];

            var PTS_DTS_flags = (byte)((x >> 6) & 3);
            var ESCR_flag = 0 != (x & (1 << 5));
            var ES_rate_flag = 0 != (x & (1 << 4));
            var DSM_trick_mode_flag = 0 != (x & (1 << 3));
            var additional_copy_info_flag = 0 != (x & (1 << 2));
            var PES_CRC_flag = 0 != (x & (1 << 1));
            var PES_extension_flag = 0 != (x & 1);

            var PES_header_data_length = buffer[i++];

            var payloadIndex = i + PES_header_data_length;

            if (1 == PTS_DTS_flags)
                return;

            var pts = 0UL;
            ulong? dts = null;

            if (0 != PTS_DTS_flags)
            {
                x = buffer[i++];

                var prefix = (byte)(x >> 4);
                if (2 != (prefix & ~1))
                {
                    // TODO: ???
                }

                // validate marker_bit
                if (0 == (x & 1))
                    return;

                // PTS[32..30]
                pts = (ulong)(x & 0x0e) << 29;

                // PTS[29..22]
                pts |= (uint)buffer[i++] << 22;

                x = buffer[i++];

                // validate marker_bit
                if (0 == (x & 1))
                    return;

                // PTS[21..15]
                pts |= (uint)(x & 0xfe) << 14;

                // PTS[14..6]
                pts |= (uint)buffer[i++] << 7;

                x = buffer[i++];

                // validate marker_bit
                if (0 == (x & 1))
                    return;

                pts |= (uint)(x >> 1);

                if (3 == PTS_DTS_flags)
                {
                    x = buffer[i++];

                    prefix = (byte)(x >> 4);

                    if (1 != prefix)
                    {
                        // TODO: ???
                    }

                    // validate marker_bit
                    if (0 == (x & 1))
                        return;

                    // DTS[32..30]
                    dts = (ulong)(x & 0x0e) << 29;

                    // DTS[29..22]
                    dts |= (uint)buffer[i++] << 22;

                    x = buffer[i++];

                    // validate marker_bit
                    if (0 == (x & 1))
                        return;

                    // DTS[21..15]
                    dts |= (uint)(x & 0xfe) << 14;

                    // DTS[14..6]
                    dts |= (uint)buffer[i++] << 7;

                    x = buffer[i++];

                    // validate marker_bit
                    if (0 == (x & 1))
                        return;

                    dts |= (uint)(x >> 1);
                }
            }

            if (ESCR_flag)
            {
                // Skip ESCR
                i += 5;
            }

            if (ES_rate_flag)
            {
                // Skip ES rate
                i += 3;
            }

            if (DSM_trick_mode_flag)
            {
                // Skip DSM trick mode
                ++i;
            }

            if (additional_copy_info_flag)
            {
                // Skip...
                ++i;
            }

            if (PES_CRC_flag)
            {
                // Skip...
                i += 2;
            }

            if (PES_extension_flag)
            { }

            if (null == _pts)
                _pts = new RegisterExtender(pts, 33);
            else
                pts = _pts.Extend(pts);

            if (dts.HasValue)
                dts = _pts.Extend(dts.Value);

            var pes = CreatePacket(payloadIndex, _index - payloadIndex, pts, dts);

            _startIndex = _index;

            _handler(pes);
        }

        TimeSpan PtsToTimestamp(ulong pts)
        {
            return TimeSpan.FromTicks((long)Math.Round(pts * PtsTo100ns));
        }

        TsPesPacket CreatePacket(int index, int length, ulong pts, ulong? dts)
        {
            Debug.Assert(length > 0);
            Debug.Assert(index >= 0);
            Debug.Assert(index + length <= _bufferEntry.Buffer.Length);

            var pes = _pesPacketPool.AllocatePesPacket(_bufferEntry);

            pes.Index = index;
            pes.Length = length;
            pes.PresentationTimestamp = PtsToTimestamp(pts);
            pes.DecodeTimestamp = dts.HasValue ? PtsToTimestamp(dts.Value) : null as TimeSpan?;

            Debug.Assert(pes.PresentationTimestamp >= TimeSpan.Zero);

#if DEBUG
            //Debug.WriteLine("Create PES Packet({0}) Index {1} Length {2} Time {3} ({4}s) {5}", pes.PacketId, pes.Index, pes.Length, pes.PresentationTimestamp, pes.PresentationTimestamp.TotalSeconds, pes.BufferEntry);
#endif

            return pes;
        }

        void ParseDataPesPacket()
        {
            if (null == _handler)
                return;

            // TODO: What about the timestamp...?
            var pes = CreatePacket(_startIndex + 6, _index - 6 - _startIndex, 0, null);

            _startIndex = _index;

            _handler(pes);
        }

        void ParsePesPacket()
        {
            if (_index - _startIndex < 6)
                return;

            switch ((StreamId)_streamId)
            {
                case StreamId.padding_stream:
                    break;
                case StreamId.program_stream_map:
                case StreamId.private_stream_2:
                case StreamId.ECM_stream:
                case StreamId.EMM_stream:
                case StreamId.program_stream_directory:
                case StreamId.DSMCC_stream:
                case StreamId.itu_rec_h_222_1_E:
                    ParseDataPesPacket();
                    break;
                default:
                    ParseNormalPesPacket();
                    break;
            }
        }

        void Flush()
        {
            ParsePesPacket();

            _startIndex = _index;
        }

        public void Clear()
        {
            _startIndex = 0;
            _index = 0;
            _length = 0;

            if (null != _bufferEntry)
            {
                _bufferPool.Free(_bufferEntry);
                _bufferEntry = null;
            }
        }

        public void FlushBuffers()
        {
            Clear();
        }

        #region Nested type: StreamId

        /// <summary>
        ///     From ISO/IEC 18818-1:2007 Table 2-22
        /// </summary>
        enum StreamId : byte
        {
            program_stream_map = 0xbc,
            private_stream_1 = 0xbd,
            padding_stream = 0xbe,
            private_stream_2 = 0xbf,
            ECM_stream = 0xf0,
            EMM_stream = 0xf1,
            DSMCC_stream = 0xf2,
            iso_13522_stream = 0xf3,
            itu_rec_h_222_1_A = 0xf4,
            itu_rec_h_222_1_B = 0xf5,
            itu_rec_h_222_1_C = 0xf6,
            itu_rec_h_222_1_D = 0xf7,
            itu_rec_h_222_1_E = 0xf8,
            ancillary_stream = 0xf9,
            iso_14496_1_packetized_stream = 0xfa,
            iso_14496_1_FlexMux_stream = 0xfb,
            metadata_stream = 0xfc,
            extended_stream_id = 0xfd,
            reserved_data_stream = 0xfe,
            program_stream_directory = 0xff
        }

        #endregion
    }
}

// -----------------------------------------------------------------------
//  <copyright file="TsProgramMapTable.cs" company="Henric Jungheim">
//  Copyright (c) 2012.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SM.TsParser.Utility;

namespace SM.TsParser
{
    public class TsProgramMapTable
    {
        const int MinimumProgramMapSize = 16;
        readonly TsDecoder _decoder;
        readonly Dictionary<uint, ProgramMap> _newPrograms = new Dictionary<uint, ProgramMap>();
        readonly uint _pid;
        readonly List<ProgramMap> _programList = new List<ProgramMap>();
        readonly Dictionary<uint, ProgramMap> _programMap = new Dictionary<uint, ProgramMap>();
        readonly int _programNumber;
        readonly Func<int, TsStreamType, bool> _streamFilter;
        bool _foundPcrPid;
        ulong? _pcr;
        int? _pcrIndex;
        uint _pcrPid;

        public TsProgramMapTable(TsDecoder decoder, int programNumber, uint pid, Func<int, TsStreamType, bool> streamFilter)
        {
            _decoder = decoder;
            _programNumber = programNumber;
            _pid = pid;
            _streamFilter = streamFilter;
        }

        public void Add(TsPacket packet)
        {
            if (null == packet)
                return; // Ignore end-of-stream

            var i0 = packet.BufferOffset;
            var i = i0;
            var buffer = packet.Buffer;
            var length = packet.BufferLength;

            if (length < MinimumProgramMapSize + 1)
                return;

            var pointer = buffer[i++];

            i += pointer;
            if (i - i0 + MinimumProgramMapSize >= length)
                return;

            var tableIdOffset = i;

            var table_id = buffer[i++];

            if (0x02 != table_id) // Program map
                return;

            var section_length = (buffer[i] << 8) | buffer[i + 1];
            i += 2;

            var section_syntax_indicator = 0 != (section_length & (1 << 15));

            if (0 != (section_length & (1 << 14)))
                return;

            section_length &= 0x0fff;

            if (section_length + i - i0 > length)
                return;

            var mapTableLength = section_length + i - tableIdOffset;

            if (i - i0 + MinimumProgramMapSize >= length)
                return;

            var validChecksum = Crc32Msb.Validate(buffer, tableIdOffset, mapTableLength);

            if (!validChecksum)
                return;

            var program_number = (buffer[i] << 8) | buffer[i + 1];
            i += 2;

            var version_number = buffer[i++];

            var current_next_indicator = 0 != (version_number & 1);

            version_number >>= 1;
            version_number &= 0x1f;

            var section_number = buffer[i++];
            var last_section_number = buffer[i++];

            if (last_section_number < section_number)
                return;

            var PCR_PID = ((uint)buffer[i] << 8) | buffer[i + 1];
            i += 2;
            PCR_PID &= 0x1fff;

            _pcrPid = PCR_PID;

            var program_info_length = (buffer[i] << 8) | buffer[i + 1];
            i += 2;
            program_info_length &= 0x0fff;

            if (i - tableIdOffset + program_info_length >= length)
                return;

            i += program_info_length;

            // Do not include the 4 byte CRC at the end.
            var mappingEnd = tableIdOffset + mapTableLength - 4;

            while (i + 5 <= mappingEnd)
            {
                var stream_type = buffer[i++];

                var elementary_PID = ((uint)buffer[i] << 8) | buffer[i + 1];
                i += 2;

                elementary_PID &= 0x1fff;

                var ES_info_length = (buffer[i] << 8) | buffer[i + 1];
                i += 2;

                ES_info_length &= 0x0fff;

                if (i + ES_info_length > mappingEnd)
                    return;

                i += ES_info_length;

                var streamType = TsStreamType.FindStreamType(stream_type);

                var programMap = new ProgramMap { Pid = elementary_PID, StreamType = streamType };

                _newPrograms[elementary_PID] = programMap;
            }

            if (section_number == last_section_number)
                MapPrograms();

            //var crc32 = (buffer[i] << 24) | (buffer[i + 1] << 16) | (buffer[i + 2] << 8) | buffer[i + 3];
            //i += 4;
        }

        void AddPcr(TsPacket packet)
        {
            if (null == packet || !packet.Pcr.HasValue)
                return;

            _pcrIndex = packet.TsIndex;
            _pcr = packet.Pcr;
        }

        void ClearProgram(ProgramMap program)
        {
            _decoder.UnregisterHandler(program.Pid);

            var pes = program.Stream;

            if (null != pes)
                pes.Clear();

            var remove = _programMap.Remove(program.Pid);

            Debug.Assert(remove);
        }

        public void Clear()
        {
            foreach (var program in _programMap.Values.ToArray())
                ClearProgram(program);

            Debug.Assert(0 == _programMap.Count);
            _newPrograms.Clear();
        }

        void MapPrograms()
        {
            _programList.Clear();

            foreach (var program in _programMap.Values)
            {
                ProgramMap newProgramMap;
                if (_newPrograms.TryGetValue(program.Pid, out newProgramMap))
                {
                    if (newProgramMap.StreamType != program.StreamType)
                        _programList.Add(program);
                }
                else
                {
                    _programList.Add(program);
                }
            }

            if (_programList.Count > 0)
            {
                foreach (var program in _programList)
                    ClearProgram(program);

                _programList.Clear();
            }

            foreach (var program in _newPrograms.Values)
            {
                var streamRequested = _streamFilter(_programNumber, program.StreamType);

                ProgramMap mappedProgram;
                if (_programMap.TryGetValue(program.Pid, out mappedProgram))
                {
                    if (mappedProgram.StreamType == program.StreamType && streamRequested)
                        continue;

                    ClearProgram(mappedProgram);
                }

                var pid = program.Pid;

                if (streamRequested)
                {
                    var pes = new TsPacketizedElementaryStream(_decoder, program.StreamType, pid);

                    program.Stream = pes;

                    _programMap[pid] = program;

                    if (pid == _pcrPid)
                    {
                        _foundPcrPid = true;

                        _decoder.RegisterHandler(pid,
                                                 p =>
                                                 {
                                                     AddPcr(p);
                                                     pes.Add(p);
                                                 }
                            );
                    }
                    else
                        _decoder.RegisterHandler(pid, pes.Add);
                }
                else
                {
                    if (pid == _pcrPid)
                    {
                        _foundPcrPid = true;

                        _decoder.RegisterHandler(pid, AddPcr);
                    }
                }
            }

            _newPrograms.Clear();

            if (!_foundPcrPid)
            {
                _foundPcrPid = true;
                _decoder.RegisterHandler(_pcrPid, AddPcr);
            }
        }

        #region Nested type: ProgramMap

        class ProgramMap
        {
            public uint Pid;
            public TsPacketizedElementaryStream Stream;
            public TsStreamType StreamType;
        }

        #endregion
    }
}

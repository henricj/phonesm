// -----------------------------------------------------------------------
//  <copyright file="TsProgramMapTable.cs" company="Henric Jungheim">
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
using SM.TsParser.Utility;

namespace SM.TsParser
{
    public class TsProgramMapTable
    {
        const int MinimumProgramMapSize = 16;
        readonly TsDecoder _decoder;
        readonly Dictionary<uint, ProgramMap> _newProgramStreams = new Dictionary<uint, ProgramMap>();
        readonly uint _pid;
        readonly int _programNumber;
        readonly Dictionary<uint, ProgramMap> _programStreamMap = new Dictionary<uint, ProgramMap>();
        readonly Dictionary<Tuple<uint, TsStreamType>, ProgramMap> _retiredProgramStreams = new Dictionary<Tuple<uint, TsStreamType>, ProgramMap>();
        readonly Action<IProgramStreams> _streamFilter;
        readonly List<ProgramMap> _streamList = new List<ProgramMap>();
        bool _foundPcrPid;
        ulong? _pcr;
        int? _pcrIndex;
        uint _pcrPid;

        public TsProgramMapTable(TsDecoder decoder, int programNumber, uint pid, Action<IProgramStreams> streamFilter)
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

                var programMap = new ProgramMap
                                 {
                                     Pid = elementary_PID,
                                     StreamType = streamType
                                 };

                _newProgramStreams[elementary_PID] = programMap;
            }

            if (section_number == last_section_number)
                MapProgramStreams();

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

            var remove = _programStreamMap.Remove(program.Pid);

            Debug.Assert(remove);
        }

        public void Clear()
        {
            foreach (var program in _programStreamMap.Values.ToArray())
                ClearProgram(program);

            Debug.Assert(0 == _programStreamMap.Count);

            _newProgramStreams.Clear();

            _retiredProgramStreams.Clear();
        }

        public void FlushBuffers()
        {
            foreach (var program in _programStreamMap.Values)
                program.Stream.FlushBuffers();

            _newProgramStreams.Clear();
        }

        void MapProgramStreams()
        {
            _streamList.Clear();

            foreach (var program in _programStreamMap.Values)
            {
                ProgramMap newProgramMap;
                if (_newProgramStreams.TryGetValue(program.Pid, out newProgramMap))
                {
                    if (!Equals(newProgramMap.StreamType, program.StreamType))
                        _streamList.Add(program);
                }
                else
                    _streamList.Add(program);
            }

            if (_streamList.Count > 0)
            {
                foreach (var program in _streamList)
                {
                    Debug.WriteLine("*** TsProgramMapTable.MapProgramStreams(): retiring " + program);

                    ClearProgram(program);

                    _retiredProgramStreams[Tuple.Create(program.Pid, program.StreamType)] = program;
                }

                _streamList.Clear();
            }

            var programStreams = new ProgramStreams
                                 {
                                     ProgramNumber = _programNumber,
                                     Streams = _newProgramStreams.Values
                                                                 .Select(s => new ProgramStreams.ProgramStream
                                                                              {
                                                                                  Pid = s.Pid,
                                                                                  StreamType = s.StreamType
                                                                              })
                                                                 .ToArray()
                                 };

            if (null != _streamFilter)
                _streamFilter(programStreams);

            foreach (var programStream in from ps in programStreams.Streams
                                          join pm in _newProgramStreams.Values on ps.Pid equals pm.Pid
                                          select new
                                                 {
                                                     ps.BlockStream,
                                                     ProgramStream = pm
                                                 })
            {
                var streamRequested = !programStream.BlockStream;

                var pid = programStream.ProgramStream.Pid;
                var streamType = programStream.ProgramStream.StreamType;

                ProgramMap mappedProgram;
                if (_programStreamMap.TryGetValue(pid, out mappedProgram))
                {
                    if (Equals(mappedProgram.StreamType, streamType) && streamRequested)
                        continue;

                    ClearProgram(mappedProgram);
                }

                if (streamRequested)
                {
                    TsPacketizedElementaryStream pes;

                    var key = Tuple.Create(pid, streamType);

                    ProgramMap retiredProgram;
                    if (_retiredProgramStreams.TryGetValue(key, out retiredProgram))
                    {
                        Debug.WriteLine("*** TsProgramMapTable.MapProgramStreams(): remapping retired program " + retiredProgram);

                        var removed = _retiredProgramStreams.Remove(key);

                        Debug.Assert(removed, "Unable to remove program from retired");

                        pes = retiredProgram.Stream;

                        _programStreamMap[pid] = retiredProgram;
                    }
                    else
                    {
                        pes = _decoder.CreateStream(streamType, pid);

                        programStream.ProgramStream.Stream = pes;

                        _programStreamMap[pid] = programStream.ProgramStream;
                    }

                    if (pid == _pcrPid)
                    {
                        _foundPcrPid = true;

                        _decoder.RegisterHandler(pid,
                            p =>
                            {
                                AddPcr(p);
                                pes.Add(p);
                            });
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

            _newProgramStreams.Clear();

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

            public override string ToString()
            {
                return string.Format("{0}/{1}", Pid, null == StreamType ? "<unknown type>" : StreamType.Description);
            }
        }

        #endregion
    }
}

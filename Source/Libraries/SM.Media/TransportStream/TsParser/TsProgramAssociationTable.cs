// -----------------------------------------------------------------------
//  <copyright file="TsProgramAssociationTable.cs" company="Henric Jungheim">
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SM.Media.TransportStream.TsParser
{
    public interface ITsProgramAssociationTableFactory
    {
        TsProgramAssociationTable Create(ITsDecoder decoder, Func<int, bool> programFilter, Action<IProgramStreams> streamFilter);
    }

    public class TsProgramAssociationTableFactory : ITsProgramAssociationTableFactory
    {
        readonly ITsProgramMapTableFactory _programMapTableFactory;

        public TsProgramAssociationTableFactory(ITsProgramMapTableFactory programMapTableFactory)
        {
            if (null == programMapTableFactory)
                throw new ArgumentNullException(nameof(programMapTableFactory));

            _programMapTableFactory = programMapTableFactory;
        }

        #region ITsProgramAssociationTableFactory Members

        public TsProgramAssociationTable Create(ITsDecoder decoder, Func<int, bool> programFilter, Action<IProgramStreams> streamFilter)
        {
            return new TsProgramAssociationTable(decoder, _programMapTableFactory, programFilter, streamFilter);
        }

        #endregion
    }

    public class TsProgramAssociationTable : TsProgramSpecificInformation
    {
        const int MinimumProgramAssociationSize = 5;

        readonly ITsDecoder _decoder;
        readonly List<ProgramAssociation> _newPrograms = new List<ProgramAssociation>();
        readonly List<ProgramAssociation> _oldPrograms = new List<ProgramAssociation>();
        readonly Func<int, bool> _programFilter;
        readonly ITsProgramMapTableFactory _programMapTableFactory;
        readonly List<ProgramAssociation> _programs = new List<ProgramAssociation>();
        readonly Action<IProgramStreams> _streamFilter;
        bool _currentNextIndicator;
        bool _hasData;
        byte _lastSectionNumber;
        byte _sectionNumber;
        int _transportStreamId;
        uint _versionNumber;

        public TsProgramAssociationTable(ITsDecoder decoder, ITsProgramMapTableFactory programMapTableFactory,
            Func<int, bool> programFilter, Action<IProgramStreams> streamFilter)
            : base(TsTableId.program_association_section)
        {
            if (null == decoder)
                throw new ArgumentNullException(nameof(decoder));
            if (null == programMapTableFactory)
                throw new ArgumentNullException(nameof(programMapTableFactory));

            _decoder = decoder;
            _programMapTableFactory = programMapTableFactory;
            _programFilter = programFilter;
            _streamFilter = streamFilter;
        }

        protected override void ParseSection(TsPacket packet, int offset, int length)
        {
            if (length < MinimumProgramAssociationSize)
                return;

            var i = offset;
            var buffer = packet.Buffer;

            var sectionIndex = i;
            //var sectionLength = section_length + i - tableIdOffset;
            var sectionEnd = i + length;

            _transportStreamId = (buffer[i] << 8) | buffer[i + 1];
            i += 2;

            var version_number = buffer[i++];

            _currentNextIndicator = 0 != (version_number & 1);

            version_number >>= 1;
            version_number &= 0x1f;

            var section_number = buffer[i++];
            var last_section_number = buffer[i++];

            if (_lastSectionNumber > 0 && last_section_number != _lastSectionNumber)
            {
                // TODO: Report garbage data somehow...
                return;
            }

            if (last_section_number < section_number)
                return;

            if (_hasData)
            {
                if (_versionNumber != version_number)
                    return;

                // _sectionNumber overflows should be fine...  There should never be that many
                // sections (the section length would violate the spec).
                ++_sectionNumber;

                if (_sectionNumber != section_number)
                    return;

                if (_lastSectionNumber != last_section_number)
                    return;
            }
            else
            {
                if (0 != section_number)
                    return;

                _sectionNumber = 0;
                _lastSectionNumber = last_section_number;
                _versionNumber = version_number;

                _hasData = true;
            }

            while (i + 4 <= sectionEnd) // Check if there is room for one more 4 byte program
            {
                var program_number = (buffer[i] << 8) | buffer[i + 1];
                i += 2;

                var pid = ((uint)buffer[i] << 8) | buffer[i + 1];
                pid &= 0x1fff;

                i += 2;

                if (!_newPrograms.Any(p => p.Pid == pid && p.ProgramNumber == program_number))
                {
                    if (_programFilter(program_number))
                    {
                        _newPrograms.Add(new ProgramAssociation
                        {
                            ProgramNumber = program_number,
                            Pid = pid
                        });
                    }
                }
            }

            if (_sectionNumber == _lastSectionNumber && _currentNextIndicator)
                Activate();
        }

        void Activate()
        {
            // Remove old programs
            foreach (var program in _programs)
            {
                if (_newPrograms.Contains(program))
                    continue;

                _decoder.UnregisterHandler(program.Pid);
                _oldPrograms.Add(program);
            }

            foreach (var program in _oldPrograms)
                CloseProgram(program);

            _oldPrograms.Clear();

            // Add new programs
            foreach (var program in _newPrograms)
            {
                if (0 == program.ProgramNumber)
                    continue; // Ignore network information tables

                if (!_programs.Contains(program))
                {
                    var tsProgramMapTable = _programMapTableFactory.Create(_decoder, program.ProgramNumber, program.Pid, _streamFilter);

                    program.MapTable = tsProgramMapTable;

                    _decoder.RegisterHandler(program.Pid, tsProgramMapTable.Add);

                    _programs.Add(program);
                }
            }

            _newPrograms.Clear();
        }

        void CloseProgram(ProgramAssociation program)
        {
            var removed = _programs.Remove(program);

            Debug.Assert(removed);

            var mapTable = program.MapTable;

            if (null != mapTable)
                mapTable.Clear();
        }

        public void Clear()
        {
            foreach (var program in _programs.ToArray())
                CloseProgram(program);

            Debug.Assert(0 == _programs.Count);

            _newPrograms.Clear();
            _oldPrograms.Clear();
        }

        public void FlushBuffers()
        {
            foreach (var program in _programs)
                program.FlushBuffers();

            _newPrograms.Clear();
        }

        #region Nested type: ProgramAssociation

        class ProgramAssociation : IEquatable<ProgramAssociation>
        {
            public TsProgramMapTable MapTable;
            public uint Pid;
            public int ProgramNumber;

            #region IEquatable<ProgramAssociation> Members

            public bool Equals(ProgramAssociation other)
            {
                if (ReferenceEquals(this, other))
                    return true;

                return Pid == other.Pid && ProgramNumber == other.ProgramNumber;
            }

            #endregion

            public override bool Equals(object obj)
            {
                return Equals(obj as ProgramAssociation);
            }

            public override int GetHashCode()
            {
                return 5 * Pid.GetHashCode() + 65537 * ProgramNumber.GetHashCode();
            }

            public void FlushBuffers()
            {
                MapTable.FlushBuffers();
            }
        }

        #endregion
    }
}

// -----------------------------------------------------------------------
//  <copyright file="TsPacket.cs" company="Henric Jungheim">
//  Copyright (c) 2012, 2013.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012, 2013 Henric Jungheim <software@henric.org>
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
using System.Text;

namespace SM.TsParser
{
    public class TsPacket
    {
        const double Pcr27MHzTicksScale = TimeSpan.TicksPerSecond / 27000000.0;
        public const int PacketSize = 188;
        public const int SyncByte = 0x47;

        static readonly Dictionary<uint, string> PacketTypes =
            new Dictionary<uint, string>
            {
                { 0x0000, "Program Association Table" },
                { 0x0001, "Conditional Access Table" },
                { 0x0002, "Transport Stream Description Table" },
                { 0x0003, "IPMP Control Information Table" },
                { 0x1fff, "Null packet" }
            };

        static readonly string[] AdaptationValues =
        {
            "Reserved",
            "Payload Only",
            "Adaptation Only", "Adaptation and Payload"
        };

        static readonly string[] ScramblingControlValues =
        {
            "Not scrambled",
            "User defined 1",
            "User defined 2",
            "User defined 3"
        };

        int _adaptationFieldControl;
        byte _adaptationFlags;
        int _adaptationLength;
        byte[] _buffer;
        //ulong _opcr;
        int _payloadIndex;
        int _payloadLength;
        bool _transportErrorIndicator;
        bool _transportPriority;
        int _transportScramblingControl;
        //bool _transportPrivateData;
        //bool _adaptationFieldExtension;

        public uint Pid { get; private set; }

        public int PayloadLength
        {
            get { return _payloadLength; }
        }

        public bool IsStart { get; private set; }

        public bool IsSkip { get; private set; }

        public int ContinuityCount { get; private set; }

        public bool IsDiscontinuos { get; private set; }

        public ulong? Pcr { get; private set; }

        /// <summary>
        ///     The byte count from the start of the transport stream where this packet starts.
        ///     ISO/IEC 13818-1 calls this "i".
        /// </summary>
        public int TsIndex { get; private set; }

        internal byte[] Buffer
        {
            get { return _buffer; }
        }

        internal int BufferOffset
        {
            get { return _payloadIndex; }
        }

        internal int BufferLength
        {
            get { return _payloadLength; }
        }

        public void CopyTo(byte[] buffer, int index)
        {
            Array.Copy(_buffer, _payloadIndex, buffer, index, _payloadLength);
        }

        ulong ReadTime(byte[] buffer, int index)
        {
            // Is there some sort of sane reason for handling the time
            // this way?

            // 33 bits of time / 300
            // 9 reserved bits
            // 9 bits of time % 300

            // Get the first 32 bits
            ulong time = ((uint)buffer[index] << 24)
                         | ((uint)buffer[index + 1] << 16)
                         | ((uint)buffer[index + 2] << 8)
                         | buffer[index + 3];

            time <<= 1;

            var ext = ((uint)buffer[index + 4] << 8) | buffer[index + 5];

            // Get the last bit
            if (0 != (ext & (1 << 15)))
                time |= 1;

            time = time * 300 + (ext & 0x1ff);

            return time;
        }

        public bool Parse(int tsIndex, byte[] buffer, int index)
        {
            TsIndex = tsIndex;

            _buffer = buffer;

            var i = index;

            IsSkip = false;

            if (SyncByte != buffer[i++])
            {
                IsSkip = true;
                return false;
            }

            // PID

            Pid = (uint)buffer[i++] << 8;
            Pid |= buffer[i++];

            _transportErrorIndicator = 0 != (Pid & (1 << 15));
            IsStart = 0 != (Pid & (1 << 14));
            _transportPriority = 0 != (Pid & (1 << 13));

            Pid &= 0x1fff;

            if (0x1fff == Pid)
            {
                IsSkip = true;
                return true;
            }

            if (_transportErrorIndicator)
            {
                IsSkip = true;
                return true;
            }

            var continuity_counter = buffer[i++];

            _transportScramblingControl = (continuity_counter >> 6) & 0x03;
            _adaptationFieldControl = (continuity_counter >> 4) & 0x03;

            // ISO/IEC 13818-1:2007 2.4.3.3
            if (0 == _adaptationFieldControl)
                IsSkip = true;

            ContinuityCount = continuity_counter & 0x0f;

            _payloadIndex = i;
            _payloadLength = PacketSize - (i - index);

            IsDiscontinuos = false;
            //_transportPrivateData = false;
            //_adaptationFieldExtension = false;
            Pcr = null;

            if (0 != (_adaptationFieldControl & 0x2))
            {
                _adaptationLength = buffer[i++];

                ++_payloadIndex;
                --_payloadLength;

                if (_adaptationLength > 0)
                {
                    var adaptationLength = _adaptationLength;

                    if (_payloadLength < _adaptationLength)
                        return false;

                    _payloadIndex += _adaptationLength;
                    _payloadLength -= _adaptationLength;

                    _adaptationFlags = buffer[i++];
                    --adaptationLength;

                    IsDiscontinuos = 0 != (_adaptationFlags & (1 << 7));
                    //_transportPrivateData = 0 != (_adaptationFlags & (1 << 1));
                    //_adaptationFieldExtension = 0 != (_adaptationFlags & (1 << 0));

                    // PCR
                    if (0 != (_adaptationFlags & (1 << 4)))
                    {
                        if (adaptationLength < 6)
                            return false;

                        Pcr = ReadTime(buffer, i);

                        i += 6;
                        adaptationLength -= 6;
                    }

                    // OPCR
                    //if (0 != (_adaptationFlags & (1 << 3)))
                    //{
                    //    if (adaptationLength < 6)
                    //        return false;

                    //    _opcr = ReadTime(buffer, i);

                    //    i += 6;
                    //    adaptationLength -= 6;
                    //}
                }
            }
            else
                _adaptationLength = 0;

            return true;
        }

        public override string ToString()
        {
            string packetType;
            if (!PacketTypes.TryGetValue(Pid, out packetType))
            {
                if (Pid >= 0x0003 && Pid <= 0x000f)
                    packetType = String.Format("Reserved{0:X4}", Pid);
                else
                    packetType = String.Format("PID{0:X4}", Pid);
            }

            var sb = new StringBuilder();

            sb.AppendFormat("'{0}'{1}{2} Count={3} ({4}, {5})",
                packetType, IsStart ? " Start" : null, _transportPriority ? " Priority" : null,
                ContinuityCount, AdaptationValues[_adaptationFieldControl], ScramblingControlValues[_transportScramblingControl]);

            if (0 != (_adaptationFieldControl & 0x2) && _adaptationLength > 0)
            {
                sb.AppendLine();
                sb.AppendFormat("   Adaptation Length={0} Flags: ", _adaptationLength);

                if (0 != (_adaptationFlags & (1 << 7)))
                    sb.Append(" Discontinuity");

                if (0 != (_adaptationFlags & (1 << 6)))
                    sb.Append(" RandomAccess");

                if (0 != (_adaptationFlags & (1 << 5)))
                    sb.Append(" ElementaryStreamPriority");

                if (0 != (_adaptationFlags & (1 << 4)))
                    sb.Append(" PCR");

                //if (0 != (_adaptationFlags & (1 << 3)))
                //    sb.Append(" OPCR");

                if (0 != (_adaptationFlags & (1 << 2)))
                    sb.Append(" SplicingPoint");

                if (0 != (_adaptationFlags & (1 << 1)))
                    sb.Append(" Private");

                if (0 != (_adaptationFlags & (1 << 0)))
                    sb.Append(" Ext");

                if (0 != (_adaptationFlags & (1 << 4)))
                {
                    sb.AppendLine();
                    sb.AppendFormat("   PCR {0} ({1})", Pcr, TimeSpan.FromTicks((long)(Pcr * Pcr27MHzTicksScale)));
                }

                //if (0 != (_adaptationFlags & (1 << 3)))
                //{
                //    sb.AppendLine();
                //    sb.AppendFormat("   OPCR {0}", _opcr);
                //}
            }

            return sb.ToString();
        }
    }
}

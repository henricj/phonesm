// -----------------------------------------------------------------------
//  <copyright file="TsPesPacket.cs" company="Henric Jungheim">
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
using System.Threading;
using SM.TsParser.Utility;

namespace SM.TsParser
{
    public class TsPesPacket
    {
        internal BufferInstance BufferEntry;

        public byte[] Buffer
        {
            get { return BufferEntry.Buffer; }
        }

        public int Index;
        public int Length;
        public TimeSpan PresentationTimestamp;
        public TimeSpan? DecodeTimestamp;
        public TimeSpan? Duration;

        public void Clear()
        {
            Index = Length = 0;
            PresentationTimestamp = TimeSpan.Zero;
            DecodeTimestamp = null;
            Duration = null;
        }

#if DEBUG
        static int _packetCount;
        public readonly int PacketId = Interlocked.Increment(ref _packetCount);
#endif

        public override string ToString()
        {
#if DEBUG
            return string.Format("Packet({0}) index {1} length {2} duration {3} timestamp {4}/{5} buffer {6}",
                PacketId, Index, Length, Duration, PresentationTimestamp, DecodeTimestamp, BufferEntry);
#else
            return string.Format("Packet index {0} length {1} duration {2} timestamp {3}/{4} buffer {5}",
                Index, Length, Duration, PresentationTimestamp, DecodeTimestamp, BufferEntry);
#endif
        }
    }
}

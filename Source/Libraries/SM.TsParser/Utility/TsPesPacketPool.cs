// -----------------------------------------------------------------------
//  <copyright file="TsPesPacketPool.cs" company="Henric Jungheim">
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

namespace SM.TsParser.Utility
{
    public sealed class TsPesPacketPool : ITsPesPacketPool
    {
        readonly IBufferPool _bufferPool;
        readonly ObjectPool<TsPesPacket> _packetPool = new ObjectPool<TsPesPacket>();

        public TsPesPacketPool(IBufferPool bufferPool)
        {
            if (null == bufferPool)
                throw new ArgumentNullException("bufferPool");

            _bufferPool = bufferPool;
        }

        #region ITsPesPacketPool Members

        public void Dispose()
        {
            using (_packetPool)
            { }
        }

        public TsPesPacket AllocatePesPacket(int minSize)
        {
            if (minSize < 1)
                throw new ArgumentOutOfRangeException("minSize", "minSize must be positive: " + minSize);

            var bufferEntry = _bufferPool.Allocate(minSize);

            var packet = AllocatePacketWithOwnedBuffer(bufferEntry);

#if DEBUG
            //Debug.WriteLine("Allocate PES Packet({0}) Index {1} Length {2} Time {3} MinSize {4} {5}", packet.PacketId, packet.Index, packet.Length, packet.PresentationTimestamp, minSize, packet.BufferEntry);
#endif

            return packet;
        }

        public TsPesPacket AllocatePesPacket(BufferInstance bufferEntry)
        {
            if (null == bufferEntry)
                throw new ArgumentNullException("bufferEntry");

            bufferEntry.Reference();

            var packet = AllocatePacketWithOwnedBuffer(bufferEntry);

#if DEBUG
            //Debug.WriteLine("Allocate PES Packet({0}) Index {1} Length {2} Time {3} {4}", packet.PacketId, packet.Index, packet.Length, packet.PresentationTimestamp, packet.BufferEntry);
#endif

            return packet;
        }

        public TsPesPacket CopyPesPacket(TsPesPacket packet, int index, int length)
        {
            if (packet == null)
                throw new ArgumentNullException("packet");

            if (index < 0 || index < packet.Index)
                throw new ArgumentOutOfRangeException("index");

            if (length < 0 || index + length > packet.Index + packet.Length)
                throw new ArgumentOutOfRangeException("length");

#if DEBUG
            //Debug.WriteLine("Copy from PES Packet({0}) Index {1} Length {2} Time {3} {4}", packet.PacketId, packet.Index, packet.Length, packet.PresentationTimestamp, packet.BufferEntry);
#endif

            Debug.Assert(packet.Index >= 0);
            Debug.Assert(packet.Index + packet.Length <= packet.Buffer.Length);

            var clone = AllocatePesPacket(packet.BufferEntry);

            clone.Index = index;
            clone.Length = length;

#if DEBUG
            //Debug.WriteLine("Copy to PES Packet({0}) Index {1} Length {2} Time {3} {4}", clone.PacketId, clone.Index, clone.Length, clone.PresentationTimestamp, clone.BufferEntry);
#endif

            return clone;
        }

        public void FreePesPacket(TsPesPacket packet)
        {
            if (null == packet)
                throw new ArgumentNullException("packet");
#if DEBUG
            //Debug.WriteLine("Free PES Packet({0}) Index {1} Length {2} Time {3} {4}", packet.PacketId, packet.Index, packet.Length, packet.PresentationTimestamp, packet.BufferEntry);
#endif

            var buffer = packet.BufferEntry;

            if (null != buffer)
            {
#if DEBUG
                for (var i = packet.Index; i < packet.Index + packet.Length; ++i)
                    packet.Buffer[i] = 0xcc;
#endif
                packet.BufferEntry = null;

                _bufferPool.Free(buffer);
            }

#if DEBUG
            packet.Index = int.MinValue;
            packet.Length = int.MinValue;
            packet.PresentationTimestamp = TimeSpan.MaxValue;
            packet.DecodeTimestamp = TimeSpan.MaxValue;
#endif

            _packetPool.Free(packet);
        }

        #endregion

        TsPesPacket AllocatePacketWithOwnedBuffer(BufferInstance bufferEntry)
        {
            var packet = _packetPool.Allocate();

            packet.BufferEntry = bufferEntry;

            packet.Clear();

            return packet;
        }

        public void Clear()
        {
            _packetPool.Clear();
        }
    }
}

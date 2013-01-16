// -----------------------------------------------------------------------
//  <copyright file="TsPesPacketPool.cs" company="Henric Jungheim">
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

namespace SM.TsParser.Utility
{
    public sealed class TsPesPacketPool : IDisposable
    {
        readonly Action<BufferInstance> _freeBuffer;
        readonly ObjectPool<TsPesPacket> _packetPool = new ObjectPool<TsPesPacket>();

        public TsPesPacketPool(Action<BufferInstance> freeBuffer)
        {
            if (null == freeBuffer)
                throw new ArgumentNullException("freeBuffer");

            _freeBuffer = freeBuffer;
        }

        #region IDisposable Members

        public void Dispose()
        {
            Clear();
        }

        #endregion

        public TsPesPacket AllocatePesPacket(BufferInstance bufferEntry)
        {
            var packet = _packetPool.Allocate();

            bufferEntry.Reference();

            packet.BufferEntry = bufferEntry;

            return packet;
        }

        public void FreePesPacket(TsPesPacket packet)
        {
#if DEBUG
            //Debug.WriteLine("Free PES Packet({0}) Index {1} Length {2} Time {3} {4}", packet.PacketId, packet.Index, packet.Length, packet.Timestamp, packet.BufferEntry);
#endif

            var buffer = packet.BufferEntry;

            if (null != buffer)
            {
#if DEBUG
                for (var i = packet.Index; i < packet.Index + packet.Length; ++i)
                    packet.Buffer[i] = 0xcc;
#endif
                packet.BufferEntry = null;

                _freeBuffer(buffer);
            }

#if DEBUG
            packet.Index = int.MinValue;
            packet.Length = int.MinValue;
            packet.Timestamp = TimeSpan.MaxValue;
#endif

            _packetPool.Free(packet);
        }

        public void Clear()
        {
            _packetPool.Clear();
        }
    }
}

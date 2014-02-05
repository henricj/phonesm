// -----------------------------------------------------------------------
//  <copyright file="TsDecoder.cs" company="Henric Jungheim">
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
using SM.TsParser.Utility;

namespace SM.TsParser
{
    public sealed class TsDecoder : IDisposable
    {
        readonly IBufferPool _bufferPool;
        readonly byte[] _destinationArray;
        readonly Dictionary<uint, Action<TsPacket>> _packetHandlers = new Dictionary<uint, Action<TsPacket>>();
        readonly int _packetSize;
        readonly Func<uint, TsStreamType, Action<TsPesPacket>> _pesHandlerFactory;
        readonly TsPacket _tsPacket = new TsPacket();
        readonly TsPesPacketPool _tsPesPacketPool;
        int _destinationLength;
        volatile bool _enableProcessing = true;
        TsProgramAssociationTable _programAssociationTable;
        int _tsIndex;

        public TsDecoder(IBufferPool bufferPool, Func<uint, TsStreamType, Action<TsPesPacket>> pesHandlerFactory, int packetSize = -1)
        {
            _bufferPool = bufferPool;

            _tsPesPacketPool = new TsPesPacketPool(_bufferPool);

            _pesHandlerFactory = pesHandlerFactory;

            _packetSize = packetSize < 100 ? TsPacket.PacketSize : packetSize;

            _destinationArray = new byte[_packetSize * 174];
        }

        public bool EnableProcessing
        {
            get { return _enableProcessing; }
            set { _enableProcessing = value; }
        }

        public Action<TsPacket> PacketMonitor { get; set; }

        public ITsPesPacketPool PesPacketPool
        {
            get { return _tsPesPacketPool; }
        }

        #region IDisposable Members

        public void Dispose()
        {
            Clear();

            using (_tsPesPacketPool)
            { }
        }

        #endregion

        internal void RegisterHandler(uint pid, Action<TsPacket> handler)
        {
            _packetHandlers[pid] = handler;
        }

        internal void UnregisterHandler(uint pid)
        {
            _packetHandlers.Remove(pid);
        }

        public Action<TsPesPacket> CreatePesHandler(uint pid, TsStreamType streamType)
        {
            if (null == _pesHandlerFactory)
                return null;

            return _pesHandlerFactory(pid, streamType);
        }

        internal BufferInstance AllocateBuffer(int bufferSize)
        {
            return _bufferPool.Allocate(bufferSize);
        }

        internal void FreeBuffer(BufferInstance buffer)
        {
            _bufferPool.Free(buffer);
        }

        public void Initialize(Action<IProgramStreams> programStreamsHandler = null)
        {
            Clear();

            // Bootstrap with the program association handler
            _programAssociationTable = new TsProgramAssociationTable(this, program => true, programStreamsHandler);

            _packetHandlers[0x0000] = _programAssociationTable.Add;

            _tsIndex = 0;
        }

        void Clear()
        {
            if (null != _programAssociationTable)
            {
                _programAssociationTable.Clear();
                _programAssociationTable = null;
            }

            _packetHandlers.Clear();
            _destinationLength = 0;
        }

        public void FlushBuffers()
        {
            _programAssociationTable.FlushBuffers();
            _destinationLength = 0;
        }

        bool ParseBuffer()
        {
            var i = 0;
            while (_destinationLength >= _packetSize)
            {
                ParsePacket(_destinationArray, i);

                i += _packetSize;
                _destinationLength -= _packetSize;
            }

            if (_destinationLength > 0)
                Array.Copy(_destinationArray, i, _destinationArray, 0, _destinationLength);

            return 0 == _destinationLength;
        }

        public void ParseEnd()
        {
            Parse(null, 0, 0);

            foreach (var handler in _packetHandlers.Values)
                handler(null);
        }

        public void Parse(byte[] buffer, int offset, int length)
        {
            if (!EnableProcessing)
                return;

            // First, finish off anything currently buffered.  Takes
            // new bytes to help finish off any pending data.
            if (_destinationLength > 0)
            {
                var remainder = _destinationLength % _packetSize;

                if (remainder > 0)
                {
                    var copyLength = Math.Min(_packetSize - remainder, length);

                    if (copyLength > 0)
                    {
                        Array.Copy(buffer, offset, _destinationArray, _destinationLength, copyLength);

                        offset += copyLength;
                        length -= copyLength;

                        _destinationLength += copyLength;
                    }
                }

                if (!ParseBuffer())
                {
                    Debug.Assert(0 == length);

                    return; // We still have pending data, so we can't continue.
                }
            }

            // Run through as much as we can of the provided buffer

            var i = offset;

            while (EnableProcessing && i <= offset + length - _packetSize)
            {
                if (TsPacket.SyncByte != buffer[i] || !ParsePacket(buffer, i))
                {
                    ++i;
                    continue;
                }

                i += _packetSize;
            }

            for (; i < offset + length && TsPacket.SyncByte != buffer[i]; ++i)
                ;

            _destinationLength = length - (i - offset);

            // Store any remainder
            if (_destinationLength > 0)
                Array.Copy(buffer, i, _destinationArray, 0, _destinationLength);
        }

        bool ParsePacket(byte[] buffer, int offset)
        {
            if (!_tsPacket.Parse(_tsIndex, buffer, offset))
                return false;

            _tsIndex += _packetSize;

            if (_tsPacket.IsSkip)
                return true;

            Action<TsPacket> handler;
            if (_packetHandlers.TryGetValue(_tsPacket.Pid, out handler))
                handler(_tsPacket);

            var pm = PacketMonitor;

            if (null != pm)
                pm(_tsPacket);

            return true;
        }
    }
}

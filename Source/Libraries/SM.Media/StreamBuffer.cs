// -----------------------------------------------------------------------
//  <copyright file="StreamBuffer.cs" company="Henric Jungheim">
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
using System.Diagnostics;
using System.Threading;
using SM.TsParser;

namespace SM.Media
{
    public sealed class StreamBuffer : IStreamSource, IDisposable, IManagedBuffer
    {
        readonly Queue<TsPesPacket> _packets = new Queue<TsPesPacket>();
        readonly object _packetsLock = new object();
        readonly Action _checkForSamples;
        readonly TsStreamType _streamType;
        readonly Action<TsPesPacket> _freePesPacket;
        readonly IBufferingManager _bufferingManager;
        readonly IBufferingQueue _bufferingQueue;
        int _isDisposed;
        bool _isDone;

#if DEBUG
        static int _streamBufferCounter;
        readonly int _streamBufferId = Interlocked.Increment(ref _streamBufferCounter);
#endif

        public TimeSpan TimestampOffset { get; set; }

        public StreamBuffer(TsStreamType streamType, Action<TsPesPacket> freePesPacket, IBufferingManager bufferingManager, Action checkForSamples)
        {
            if (streamType == null)
                throw new ArgumentNullException("streamType");

            if (freePesPacket == null)
                throw new ArgumentNullException("freePesPacket");

            _streamType = streamType;
            _freePesPacket = freePesPacket;
            _bufferingManager = bufferingManager;
            _checkForSamples = checkForSamples;

            if (null != bufferingManager)
                _bufferingQueue = bufferingManager.CreateQueue(this);
        }

        #region IDisposable Members

        public void Dispose()
        {
            var wasDisposed = Interlocked.Exchange(ref _isDisposed, 1);

            if (0 != wasDisposed)
                return;

            lock (_packetsLock)
            {
                while (_packets.Count > 0)
                {
                    var packet = _packets.Dequeue();

                    if (null == packet)
                        continue;

                    _freePesPacket(packet);
                }
            }
        }

        #endregion

        void ThrowIfDisposed()
        {
            if (0 != _isDisposed)
                throw new ObjectDisposedException(GetType().Name);
        }

        #region IStreamSource Members

        public TimeSpan PresentationTimestamp { get; private set; }
        public bool IsEof { get; private set; }

        public TsPesPacket GetNextSample()
        {
            //Debug.WriteLine("StreamBuffer.GetNextSample() " + _streamType.Contents);

            ThrowIfDisposed();

            if (IsEof)
                return null;

            TsPesPacket packet = null;

            try
            {
                lock (_packetsLock)
                {
                    if (null != _bufferingManager && _bufferingManager.IsBuffering)
                        return null;

                    if (_packets.Count < 1)
                    {
                        // Keep returning null packets if we are done.
                        if (!_isDone)
                        {
                            ReportExhaustion();

                            return null;
                        }
                    }
                    else
                        packet = _packets.Dequeue();
                }

                if (null == packet)
                {
                    IsEof = true;

                    //Debug.WriteLine("StreamBuffer {0}/{1} forwarding null sample", _streamBufferId, _streamType.Contents);

                    // Propagate end-of-stream
                    return null;
                }

                ReportDequeue(packet.Length, packet.PresentationTimestamp);

                PresentationTimestamp = packet.PresentationTimestamp - TimestampOffset;

#if DEBUG
                //Debug.WriteLine("StreamBuffer {0}/{1} forwarding sample {2}", _streamBufferId, _streamType.Contents, PresentationTimestamp);
#endif
                var localPacket = packet;

                packet = null;

                return localPacket;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("GetNextSample: " + ex.Message);
            }
            finally
            {
                if (null != packet)
                    _freePesPacket(packet);

#if DEBUG
                ThrowIfDisposed();
#endif
            }

            return null;
        }

        public void FreeSample(TsPesPacket packet)
        {
            _freePesPacket(packet);
        }

        public bool HasSample
        {
            get
            {
                lock (_packetsLock)
                {
                    return _packets.Count > 0;
                }
            }
        }

        public float? BufferingProgress
        {
            get
            {
                if (null == _bufferingManager)
                    return null;

                return (float)_bufferingManager.BufferingProgress;
            }
        }

        #endregion

        void ReportEnqueue(int packetCount, TimeSpan timestamp)
        {
            var rb = _bufferingQueue;

            if (null == rb)
                return;

            rb.ReportEnqueue(packetCount, timestamp);
        }

        void ReportDequeue(int packetCount, TimeSpan timestamp)
        {
            var rb = _bufferingQueue;

            if (null == rb)
                return;

            rb.ReportDequeue(packetCount, timestamp);
        }

        void ReportExhaustion()
        {
            var rb = _bufferingQueue;

            if (null == rb)
                return;

            rb.ReportExhaustion();
        }

        void ReportFlush()
        {
            var rb = _bufferingQueue;

            if (null == rb)
                return;

            rb.ReportFlush();
        }

        void ReportDone()
        {
            var rb = _bufferingQueue;

            if (null == rb)
                return;

            rb.ReportDone();
        }

        public void Enqueue(TsPesPacket packet)
        {
            ThrowIfDisposed();

            lock (_packetsLock)
            {
                _packets.Enqueue(packet);

                if (null != packet)
                    ReportEnqueue(packet.Length, packet.PresentationTimestamp);
                else
                {
                    _isDone = true;
                    ReportDone();
                }
            }

            CheckGetNextSample();

#if DEBUG
            ThrowIfDisposed();
#endif
        }

        void CheckGetNextSample()
        {
            var checkForSamples = _checkForSamples;

            if (null != checkForSamples)
                checkForSamples();
        }

        void IManagedBuffer.Flush()
        {
            TsPesPacket[] packets = null;

            lock (_packetsLock)
            {
                if (_packets.Count > 0)
                {
                    packets = _packets.ToArray();

                    _packets.Clear();
                }

                ReportFlush();
            }

            if (null == packets)
                return;

            foreach (var packet in packets)
            {
                if (null == packet)
                    continue;

                _freePesPacket(packet);
            }
        }

        public TsStreamType StreamType
        {
            get { return _streamType; }
        }
    }
}

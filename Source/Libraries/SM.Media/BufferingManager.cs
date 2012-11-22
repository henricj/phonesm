//-----------------------------------------------------------------------
// <copyright file="BufferingManager.cs" company="Henric Jungheim">
// Copyright (c) 2012.
// <author>Henric Jungheim</author>
// </copyright>
//-----------------------------------------------------------------------
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
using System.Threading;

namespace SM.Media
{
    class BufferingManager : IBufferingManager
    {
        static readonly TimeSpan BufferEnableThreshold = TimeSpan.FromSeconds(2);
        static readonly TimeSpan BufferThreshold = TimeSpan.FromSeconds(4);
        static readonly TimeSpan BufferDisableThreshold = TimeSpan.FromSeconds(8);
        static readonly TimeSpan BufferStatusUpdatePeriod = TimeSpan.FromMilliseconds(250);
        readonly IQueueThrottling _queueThrottling;
        readonly object _lock = new object();
        readonly List<Queue> _queues = new List<Queue>();
        readonly Action<double> _reportBuffering;
        bool _blockReads;
        DateTime _bufferStatusTimeUtc = DateTime.MinValue;
        volatile int _isBuffering;
        TimeSpan? _playbackPosition;

        public BufferingManager(IQueueThrottling queueThrottling, Action<double> reportBuffering)
        {
            _queueThrottling = queueThrottling;
            _reportBuffering = reportBuffering;
        }

        void Report(Action<int, TimeSpan> update, int size, TimeSpan timestamp)
        {
            var wasBlock = _blockReads;

            lock (_lock)
            {
                update(size, timestamp);

                UnlockedReport();

                //Debug.WriteLine("Report Sample({0}, {1}) blockReads {2} => {3} ({4})", size, timestamp, wasBlock, _blockReads, DateTimeOffset.Now);
            }
        }

        void UnlockedReport()
        {
            var shouldBlock = UpdateState();

            if (shouldBlock != _blockReads)
            {
                _blockReads = shouldBlock;

                HandleStateChange();
            }
        }

        void ReportExhaustion(Action update)
        {
            Debug.WriteLine("BufferingManager.ReportExhaustion(...)");

            lock (_lock)
            {
                update();

                UnlockedStartBuffering();

                if (!_blockReads)
                    return;

                _blockReads = false;

                HandleStateChange();
            }
        }

        void ReportDone(Action update)
        {
            lock (_lock)
            {
                update();

                UnlockedReport();
            }
        }

        void UnlockedStartBuffering()
        {
#pragma warning disable 0420
            var wasBuffering = Interlocked.Exchange(ref _isBuffering, 1);
#pragma warning restore 0420

            if (0 != wasBuffering)
                return;

            if (null != _reportBuffering)
                _reportBuffering(0);
        }

        bool UpdateState()
        {
            var newest = TimeSpan.MaxValue;
            var oldest = TimeSpan.MinValue;
            var lowest = int.MaxValue;

            var validData = false;

            for (var i = 0; i < _queues.Count; ++i)
            {
                var queue = _queues[i];

                if (!queue.IsValid)
                    continue;

                validData = true;

                var count = queue.PacketCount;

                if (count < lowest)
                {
                    if (count < 2)
                    {
                        // We are ending or out of data, so don't suspend anything.
                        return false;
                    }

                    lowest = count;
                }

                var newTime = queue.Newest;

                if (newTime < newest)
                    newest = newTime;

                var oldTime = queue.Oldest;

                if (oldTime > oldest)
                    oldest = oldTime;
            }

            if (_playbackPosition.HasValue)
                oldest = _playbackPosition.Value;

            var timestampDifference = validData ? newest - oldest : TimeSpan.MaxValue;

            if (0 != _isBuffering)
                UpdateBuffering(timestampDifference);

            if (!validData)
                return false;

            if (timestampDifference < BufferEnableThreshold)
                return false;

            if (timestampDifference > BufferDisableThreshold)
                return true;

            return false;
        }

        void UpdateBuffering(TimeSpan timestampDifference)
        {
            if (timestampDifference >= BufferThreshold)
            {
#pragma warning disable 0420
                Interlocked.Exchange(ref _isBuffering, 0);
#pragma warning restore 0420

                Debug.WriteLine("BufferingManager.UpdateBuffering done buffering");

                if (null != _reportBuffering)
                    _reportBuffering(1);
            }
            else
            {
                var now = DateTime.UtcNow;

                var elapsed = now - _bufferStatusTimeUtc;

                if (elapsed >= BufferStatusUpdatePeriod)
                {
                    _bufferStatusTimeUtc = now;

                    var bufferingStatus = timestampDifference.Ticks / (double)BufferThreshold.Ticks;

                    Debug.WriteLine("BufferingManager.UpdateBuffering: {0}%", bufferingStatus * 100);

                    if (null != _reportBuffering)
                        _reportBuffering(bufferingStatus);
                }
            }
        }

        void HandleStateChange()
        {
            var er = _queueThrottling;

            if (null == er)
                return;

            if (_blockReads)
                er.Pause();
            else
                er.Resume();
        }

        #region IBufferingManager Members

        public IBufferingQueue CreateQueue()
        {
            var queue = new Queue(this);

            lock (_lock)
            {
                _queues.Add(queue);
            }

            return queue;
        }

        public void ReportPosition(TimeSpan playbackPosition)
        {
            lock (_lock)
            {
                _playbackPosition = playbackPosition;

                UnlockedReport();
            }
        }

        public TimeSpan BufferPosition
        {
            get
            {
                var latestPosition = TimeSpan.Zero;

                lock (_lock)
                {
                    foreach (var queue in _queues)
                    {
                        if (!queue.IsValid)
                            continue;

                        var position = queue.Oldest;

                        if (position > latestPosition)
                            latestPosition = position;
                    }
                }

                return latestPosition;
            }
        }

        public TimeSpan BufferDuration
        {
            get
            {
                var oldest = TimeSpan.MaxValue;
                var newest = TimeSpan.MinValue;

                lock (_lock)
                {
                    foreach (var queue in _queues)
                    {
                        if (!queue.IsValid)
                            continue;

                        var position = queue.Oldest;

                        if (position < oldest)
                            oldest = position;

                        position = queue.Newest;

                        if (position > newest)
                            newest = position;
                    }
                }

                if (newest > TimeSpan.MinValue && oldest < TimeSpan.MaxValue)
                    return newest - oldest;

                return TimeSpan.Zero;
            }
        }

        public bool IsBuffering
        {
            get { return 0 != _isBuffering; }
        }

        #endregion

        class Queue : IBufferingQueue
        {
            readonly BufferingManager _bufferingManager;
            int _bufferSize;
            bool _firstPacket;
            TimeSpan _newestPacket;
            TimeSpan _oldestPacket;
            int _packetCount;
            bool _isDone;

            public Queue(BufferingManager bufferingManager)
            {
                _bufferingManager = bufferingManager;
            }

            public bool IsValid
            {
                get { return _firstPacket && !_isDone; }
            }

            public TimeSpan Newest
            {
                get { return _newestPacket; }
            }

            public TimeSpan Oldest
            {
                get { return _oldestPacket; }
            }

            public int PacketCount
            {
                get { return _packetCount; }
            }

            public int Size
            {
                get { return _bufferSize; }
            }

            public void ReportEnqueue(int size, TimeSpan timestamp)
            {
                _bufferingManager.Report(Enqueue, size, timestamp);
            }

            public void ReportDequeue(int size, TimeSpan timestamp)
            {
                _bufferingManager.Report(Dequeue, size, timestamp);
            }

            public void ReportExhastion()
            {
                _bufferingManager.ReportExhaustion(Exhaused);
            }

            public void ReportDone()
            {
                _bufferingManager.ReportDone(Done);
            }

            void Done()
            {
                _isDone = true;
            }

            void Exhaused()
            {
                _packetCount = 0;
                _bufferSize = 0;
            }

            void Enqueue(int size, TimeSpan timestamp)
            {
                ++_packetCount;
                _bufferSize += size;

                _newestPacket = timestamp;

                if (!_firstPacket)
                {
                    _oldestPacket = timestamp;
                    _firstPacket = true;
                }
            }

            void Dequeue(int size, TimeSpan timestamp)
            {
                --_packetCount;
                _bufferSize -= size;

                _oldestPacket = timestamp;

                if (!_firstPacket)
                {
                    _newestPacket = timestamp;
                    _firstPacket = true;
                }
            }
        }
    }
}

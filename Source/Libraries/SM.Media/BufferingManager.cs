// -----------------------------------------------------------------------
//  <copyright file="BufferingManager.cs" company="Henric Jungheim">
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
using System.Threading;

namespace SM.Media
{
    class BufferingManager : IBufferingManager
    {
        const int BufferSizeMaximum = 300 * 1024;
        const int BufferSizeStopBuffering = 150 * 1024;
        const int BufferSizeStartBuffering = 50 * 1024;
        static readonly TimeSpan BufferDurationEnableThreshold = TimeSpan.FromSeconds(2);
        static readonly TimeSpan BufferDurationThreshold = TimeSpan.FromSeconds(4);
        static readonly TimeSpan BufferDurationDisableThreshold = TimeSpan.FromSeconds(8);
        static readonly TimeSpan BufferStatusUpdatePeriod = TimeSpan.FromMilliseconds(250);
        readonly object _lock = new object();
        readonly IQueueThrottling _queueThrottling;
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

                        if (position < latestPosition)
                            latestPosition = position;
                    }
                }

                Debug.Assert(latestPosition >= TimeSpan.Zero);

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
                {
                    Debug.Assert(newest >= oldest);

                    return newest - oldest;
                }

                return TimeSpan.Zero;
            }
        }

        public bool IsBuffering
        {
            get { return 0 != _isBuffering; }
        }

        #endregion

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

                UnlockedReport();
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
            var newest = TimeSpan.MinValue;
            var oldest = TimeSpan.MaxValue;
            var lowestCount = int.MaxValue;
            var highestCount = int.MinValue;
            var totalBuffered = 0;

            var validData = false;

            for (var i = 0; i < _queues.Count; ++i)
            {
                var queue = _queues[i];

                if (!queue.IsValid)
                    continue;

                totalBuffered += queue.Size;

                validData = true;

                var count = queue.PacketCount;

                if (count < lowestCount)
                    lowestCount = count;

                if (count > highestCount)
                    highestCount = count;

                var newTime = queue.Newest;

                if (newTime > newest)
                    newest = newTime;

                var oldTime = queue.Oldest;

                if (oldTime < oldest)
                    oldest = oldTime;
            }

            if (_playbackPosition.HasValue)
            {
                var time = _playbackPosition.Value;

                if (time < newest && time > oldest)
                    oldest = time;
            }

            var timestampDifference = validData ? newest - oldest : TimeSpan.MaxValue;

            Debug.Assert(timestampDifference >= TimeSpan.Zero);

            if (0 != _isBuffering)
            {
                UpdateBuffering(timestampDifference, totalBuffered);

                if (0 != _isBuffering)
                    return false;
            }
            else
            {
                //if (validData && 0 == lowestCount && timestampDifference < BufferDurationEnableThreshold && totalBuffered < BufferSizeStartBuffering)
                if (validData && 0 == highestCount)
                {
                    Debug.WriteLine("BufferingManager.UpdateState start buffering: {0} duration, {1} size, {2} memory", timestampDifference, totalBuffered, GC.GetTotalMemory(false));

                    UnlockedStartBuffering();

                    return false;
                }
            }

            if (!validData)
                return false;

            if (totalBuffered > BufferSizeMaximum)
                return true;

            if (timestampDifference < BufferDurationEnableThreshold)
                return false;

            if (timestampDifference > BufferDurationDisableThreshold)
                return true;

            return false;
        }

        void UpdateBuffering(TimeSpan timestampDifference, int bufferSize)
        {
            if ((timestampDifference >= BufferDurationThreshold && bufferSize >= BufferSizeStopBuffering) || bufferSize >= BufferSizeMaximum)
            {
#pragma warning disable 0420
                Interlocked.Exchange(ref _isBuffering, 0);
#pragma warning restore 0420

                Debug.WriteLine("BufferingManager.UpdateBuffering done buffering: {0} duration, {1} size, {2} memory", timestampDifference, bufferSize, GC.GetTotalMemory(false));

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

                    var bufferingStatus1 = Math.Max(0, timestampDifference.Ticks / (double)BufferDurationThreshold.Ticks);
                    var bufferingStatus2 = bufferSize / (double)BufferSizeStopBuffering;
                    var bufferingStatus3 = bufferSize / (double)BufferSizeMaximum;

                    var bufferingStatus = Math.Max(Math.Min(bufferingStatus1, bufferingStatus2), bufferingStatus3);

                    Debug.WriteLine("BufferingManager.UpdateBuffering: {0}%, {1} duration, {2} size, {3} memory", bufferingStatus * 100, timestampDifference, bufferSize, GC.GetTotalMemory(false));

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

        #region Nested type: Queue

        class Queue : IBufferingQueue
        {
            readonly BufferingManager _bufferingManager;
            int _bufferSize;
            bool _firstPacket;
            bool _isDone;
            TimeSpan _newestPacket;
            TimeSpan _oldestPacket;
            int _packetCount;

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

            #region IBufferingQueue Members

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

            #endregion

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

        #endregion
    }
}

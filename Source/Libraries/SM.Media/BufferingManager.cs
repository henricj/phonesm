// -----------------------------------------------------------------------
//  <copyright file="BufferingManager.cs" company="Henric Jungheim">
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
using SM.Media.Utility;
using SM.TsParser;

namespace SM.Media
{
    class BufferingManager : IBufferingManager
    {
        const int BufferSizeMaximum = 8192 * 1024;
        const int BufferSizeStopBuffering = 350 * 1024;
        static readonly TimeSpan SeekEndTolerance = TimeSpan.FromMilliseconds(256);
        static readonly TimeSpan SeekBeginTolerance = TimeSpan.FromSeconds(6);
        static readonly TimeSpan BufferDurationEnableThreshold = TimeSpan.FromSeconds(7);
        static readonly TimeSpan BufferDurationThreshold = TimeSpan.FromSeconds(9);
        static readonly TimeSpan BufferDurationDisableThreshold = TimeSpan.FromSeconds(25);
        static readonly TimeSpan BufferStatusUpdatePeriod = TimeSpan.FromMilliseconds(250);
        readonly object _lock = new object();
        readonly IQueueThrottling _queueThrottling;
        readonly List<BufferingQueue> _queues = new List<BufferingQueue>();
        readonly SignalTask _reportingTask;
        bool _blockReads;
        DateTime _bufferStatusTimeUtc = DateTime.MinValue;
        double _bufferingProgress;
        volatile int _isBuffering = 1;
        int _totalBufferedStart;

        public BufferingManager(IQueueThrottling queueThrottling, Action bufferingChange)
        {
            if (null == queueThrottling)
                throw new ArgumentNullException("queueThrottling");
            if (bufferingChange == null)
                throw new ArgumentNullException("bufferingChange");

            _queueThrottling = queueThrottling;

            _reportingTask = new SignalTask(bufferingChange, CancellationToken.None);
        }

        public TimeSpan BufferPosition
        {
            get
            {
                var latestPosition = TimeSpan.MaxValue;

                lock (_lock)
                {
                    foreach (var queue in _queues)
                    {
                        if (!queue.IsValid)
                            continue;

                        var position = queue.Oldest.Value;

                        if (position < latestPosition)
                            latestPosition = position;
                    }
                }

                Debug.Assert(latestPosition >= TimeSpan.Zero, "latestPosition: " + latestPosition);

                if (TimeSpan.MaxValue == latestPosition)
                    return TimeSpan.Zero;

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

                        var position = queue.Oldest.Value;

                        if (position < oldest)
                            oldest = position;

                        position = queue.Newest.Value;

                        if (position > newest)
                            newest = position;
                    }
                }

                if (newest > TimeSpan.MinValue && oldest < TimeSpan.MaxValue)
                {
                    Debug.Assert(newest >= oldest, string.Format("newest {0} >= oldest {1}", newest, oldest));

                    return newest - oldest;
                }

                return TimeSpan.Zero;
            }
        }

        #region IBufferingManager Members

        public IBufferingQueue CreateQueue(IManagedBuffer managedBuffer)
        {
            if (null == managedBuffer)
                throw new ArgumentNullException("managedBuffer");

            var queue = new BufferingQueue(this, managedBuffer);

            lock (_lock)
            {
                _queues.Add(queue);
            }

            return queue;
        }

        public void Flush()
        {
            BufferingQueue[] queues;

            lock (_lock)
            {
                queues = _queues.ToArray();
            }

            foreach (var queue in queues)
                queue.Flush();
        }

        public bool IsSeekAlreadyBuffered(TimeSpan position)
        {
            lock (_lock)
            {
                foreach (var queue in _queues)
                {
                    // We'll ignore the "IsValid" flag for now.  The Oldest/Newest
                    // will either be the last known values or TimeSpan.Zero.  In
                    // either case, they should work for checking the supplied
                    // position.
                    //if (!queue.IsValid)
                    //    return TimeSpan.Zero == position;

                    if (position < queue.Oldest - SeekBeginTolerance)
                        return false;

                    if (position > queue.Newest + SeekEndTolerance)
                        return false;
                }
            }

            return true;
        }

        public bool IsBuffering
        {
            get { return 0 != _isBuffering; }
        }

        public double BufferingProgress
        {
            get
            {
                lock (_lock)
                {
                    return _bufferingProgress;
                }
            }
        }

        #endregion

        void Report(Action<int, TimeSpan> update, int size, TimeSpan timestamp)
        {
            //var wasBlock = _blockReads;

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
            lock (_lock)
            {
                update();

                UnlockedReport();
            }
        }

        void ReportFlush(Action update)
        {
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

            ReportBuffering(0);
        }

        bool UpdateState()
        {
            var newest = TimeSpan.MinValue;
            var oldest = TimeSpan.MaxValue;
            var lowestCount = int.MaxValue;
            var highestCount = int.MinValue;
            var totalBuffered = 0;

            var validData = false;
            var allDone = true;
            var isExhausted = false;
            var allExhausted = true;

            foreach (var queue in _queues)
            {
                if (!queue.IsDone)
                    allDone = false;

                if (queue.IsExhausted)
                    isExhausted = true;
                else
                    allExhausted = false;

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
                    newest = newTime.Value;

                var oldTime = queue.Oldest;

                if (oldTime < oldest)
                    oldest = oldTime.Value;
            }

            var timestampDifference = validData ? newest - oldest : TimeSpan.MaxValue;

            // The presentation order is not always the same as the decoding order.  Fudge things
            // in the assert so we can still catch grievous errors.

            Debug.Assert(timestampDifference == TimeSpan.MaxValue || timestampDifference + TimeSpan.FromMilliseconds(500) >= TimeSpan.Zero,
                string.Format("Invalid timestamp difference: {0} (newest {1} oldest {2} low count {3} high count {4} valid data {5})",
                    timestampDifference, newest, oldest, lowestCount, highestCount, validData));

            if (timestampDifference <= TimeSpan.Zero)
            {
                timestampDifference = TimeSpan.Zero;
                validData = false;
            }

            if (0 != _isBuffering)
            {
                if (allDone)
                {
#pragma warning disable 0420
                    Interlocked.Exchange(ref _isBuffering, 0);
#pragma warning restore 0420

                    Debug.WriteLine("BufferingManager.UpdateState done buffering (eof): {0} duration, {1} size, {2} memory",
                        validData ? timestampDifference.ToString() : "none", validData ? totalBuffered.ToString() : "none", GC.GetTotalMemory(false));

                    ReportBuffering(1);
                }
                else if (validData)
                    UpdateBuffering(timestampDifference, Math.Max(0, totalBuffered - _totalBufferedStart));

                if (0 != _isBuffering)
                    return false;
            }
            else
            {
                //if (!allDone && isExhausted && (!validData || 0 == highestCount))
                //if (!allDone && allExhausted && validData)
                if (!allDone && isExhausted)
                {
                    Debug.WriteLine("BufferingManager.UpdateState start buffering: {0} duration, {1} size, {2} memory", timestampDifference, totalBuffered, GC.GetTotalMemory(false));

                    _totalBufferedStart = totalBuffered;

                    UnlockedStartBuffering();

                    return false;
                }
            }

            if (!validData)
                return false;

            if (totalBuffered > BufferSizeMaximum)
                return true;

            if (isExhausted)
                return false;

            if (timestampDifference < BufferDurationEnableThreshold)
                return false;

            if (timestampDifference > BufferDurationDisableThreshold)
                return true;

            return false;
        }

        void UpdateBuffering(TimeSpan timestampDifference, int bufferSize)
        {
            if ((timestampDifference >= BufferDurationThreshold && bufferSize >= BufferSizeStopBuffering) || bufferSize >= BufferSizeMaximum || timestampDifference > BufferDurationDisableThreshold)
            {
#pragma warning disable 0420
                Interlocked.Exchange(ref _isBuffering, 0);
#pragma warning restore 0420

                Debug.WriteLine("BufferingManager.UpdateBuffering done buffering: {0} duration, {1} size, {2} memory", timestampDifference, bufferSize, GC.GetTotalMemory(false));

                ReportBuffering(1);
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
                    var bufferingStatus4 = Math.Max(0, timestampDifference.Ticks / (double)BufferDurationDisableThreshold.Ticks);

                    var bufferingStatus = Math.Max(Math.Max(Math.Min(bufferingStatus1, bufferingStatus2), bufferingStatus3), bufferingStatus4);

                    Debug.WriteLine("BufferingManager.UpdateBuffering: {0:F2}%, {1} duration, {2} size, {3} memory", bufferingStatus * 100, timestampDifference, bufferSize, GC.GetTotalMemory(false));

                    ReportBuffering(bufferingStatus);
                }
            }
        }

        void ReportBuffering(double bufferingProgress)
        {
            _bufferingProgress = bufferingProgress;

            _reportingTask.Fire();
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

        #region Nested type: BufferingQueue

        class BufferingQueue : IBufferingQueue
        {
            readonly BufferingManager _bufferingManager;
            readonly IManagedBuffer _managedBuffer;
            readonly TsStreamType _streamType;
            int _bufferSize;
            bool _isDone;
            TimeSpan? _newestPacket;
            TimeSpan? _oldestPacket;
            int _packetCount;

            public BufferingQueue(BufferingManager bufferingManager, IManagedBuffer managedBuffer)
            {
                if (bufferingManager == null)
                    throw new ArgumentNullException("bufferingManager");
                if (managedBuffer == null)
                    throw new ArgumentNullException("managedBuffer");

                _bufferingManager = bufferingManager;
                _managedBuffer = managedBuffer;
                _streamType = managedBuffer.StreamType;
            }

            public bool IsDone
            {
                get { return _isDone; }
            }

            public bool IsValid
            {
                get { return _packetCount > 0 && _newestPacket.HasValue && _oldestPacket.HasValue; }
            }

            public TimeSpan? Newest
            {
                get { return _newestPacket - _managedBuffer.TimestampOffset; }
            }

            public TimeSpan? Oldest
            {
                get { return _oldestPacket - _managedBuffer.TimestampOffset; }
            }

            public int PacketCount
            {
                get { return _packetCount; }
            }

            public int Size
            {
                get { return _bufferSize; }
            }

            public bool IsExhausted { get; private set; }

            #region IBufferingQueue Members

            public void ReportEnqueue(int size, TimeSpan timestamp)
            {
                if (IsExhausted)
                {
                    Debug.WriteLine("BufferingQueue.ReportEnqueue(): IsExhausted=false " + _streamType.Contents);
                    IsExhausted = false;
                }

                _bufferingManager.Report(Enqueue, size, timestamp);
            }

            public void ReportDequeue(int size, TimeSpan timestamp)
            {
                _bufferingManager.Report(Dequeue, size, timestamp);
            }

            public void ReportExhaustion()
            {
                if (!IsExhausted)
                {
                    Debug.WriteLine("BufferingQueue.ReportExhaustion(): " + _streamType.Contents);
                    IsExhausted = true;
                }

                _bufferingManager.ReportExhaustion(Exhausted);
            }

            public void ReportFlush()
            {
                Debug.WriteLine("BufferingQueue.ReportFlush(): " + _streamType.Contents);

                _bufferingManager.ReportFlush(Exhausted);
            }

            public void ReportDone()
            {
                Debug.WriteLine("BufferingQueue.ReportDone(): " + _streamType.Contents);

                _bufferingManager.ReportDone(Done);
            }

            #endregion

            public void Flush()
            {
                _managedBuffer.Flush();
            }

            void Done()
            {
                _isDone = true;
            }

            void Exhausted()
            {
                _packetCount = 0;
                _bufferSize = 0;
                _newestPacket = _oldestPacket = null;
            }

            void Enqueue(int size, TimeSpan timestamp)
            {
                ++_packetCount;
                _bufferSize += size;

                _newestPacket = timestamp;

                if (!_oldestPacket.HasValue)
                    _oldestPacket = timestamp;
            }

            void Dequeue(int size, TimeSpan timestamp)
            {
                --_packetCount;
                _bufferSize -= size;

                _oldestPacket = timestamp;

                if (!_newestPacket.HasValue)
                    _newestPacket = timestamp;
            }
        }

        #endregion
    }
}

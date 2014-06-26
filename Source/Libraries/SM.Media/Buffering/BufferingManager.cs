// -----------------------------------------------------------------------
//  <copyright file="BufferingManager.cs" company="Henric Jungheim">
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
using System.Threading;
using SM.Media.Utility;
using SM.TsParser;
using SM.TsParser.Utility;

namespace SM.Media.Buffering
{
    public sealed class BufferingManager : IBufferingManager
    {
        static readonly TimeSpan SeekEndTolerance = TimeSpan.FromMilliseconds(256);
        static readonly TimeSpan SeekBeginTolerance = TimeSpan.FromSeconds(6);
        static readonly TimeSpan BufferStatusUpdatePeriod = TimeSpan.FromMilliseconds(250);
        readonly IBufferingPolicy _bufferingPolicy;
        readonly CancellationTokenSource _disposeCancellationTokenSource = new CancellationTokenSource();
        readonly object _lock = new object();
        readonly ITsPesPacketPool _packetPool;
        readonly List<IBufferingQueue> _queues = new List<IBufferingQueue>();
        readonly SignalTask _refreshTask;
        readonly List<BufferStatus> _statuses = new List<BufferStatus>();
        bool _blockReads;
        DateTime _bufferStatusTimeUtc = DateTime.MinValue;
        float _bufferingProgress;
        bool _isBuffering = true;
        int _isDisposed;
        bool _isEof;
        bool _isStarting = true;
        IQueueThrottling _queueThrottling;
        SignalTask _reportingTask;
        int _totalBufferedStart;

        public BufferingManager(IBufferingPolicy bufferingPolicy, ITsPesPacketPool packetPool)
        {
            if (null == bufferingPolicy)
                throw new ArgumentNullException("bufferingPolicy");
            if (null == packetPool)
                throw new ArgumentNullException("packetPool");

            _bufferingPolicy = bufferingPolicy;
            _packetPool = packetPool;

            _refreshTask = new SignalTask(() =>
                                          {
                                              RefreshHandler();

                                              return TplTaskExtensions.CompletedTask;
                                          }, _disposeCancellationTokenSource.Token);
        }

        #region IBufferingManager Members

        public void Initialize(IQueueThrottling queueThrottling, Action reportBufferingChange)
        {
            if (null == queueThrottling)
                throw new ArgumentNullException("queueThrottling");
            if (reportBufferingChange == null)
                throw new ArgumentNullException("reportBufferingChange");

            ThrowIfDisposed();

            _queueThrottling = queueThrottling;

            _reportingTask = new SignalTask(() =>
                                            {
                                                reportBufferingChange();

                                                return TplTaskExtensions.CompletedTask;
                                            }, _disposeCancellationTokenSource.Token);
        }

        public IStreamBuffer CreateStreamBuffer(TsStreamType streamType)
        {
            ThrowIfDisposed();

            var buffer = new StreamBuffer(streamType, _packetPool.FreePesPacket, this);

            lock (_lock)
            {
                _queues.Add(buffer);

                ResizeStatuses();
            }

            return buffer;
        }

        public void Flush()
        {
            Debug.WriteLine("BufferingManager.Flush()");

            ThrowIfDisposed();

            bool hasQueues;

            IBufferingQueue[] queues;

            lock (_lock)
            {
                queues = _queues.ToArray();

                _isStarting = true;
                _isBuffering = true;
                _isEof = false;

                hasQueues = _queues.Count > 0;
            }

            foreach (var queue in queues)
                queue.Flush();

            ReportBuffering(0);

            if (hasQueues)
                _refreshTask.Fire();
        }

        public bool IsSeekAlreadyBuffered(TimeSpan position)
        {
            Debug.WriteLine("BufferingManager.IsSeekAlreadyBuffered({0})", position);

            ThrowIfDisposed();

            lock (_lock)
            {
                UnlockedUpdateQueueStatus();

                foreach (var queue in _statuses)
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
            get { return _isBuffering; }
        }

        public float? BufferingProgress
        {
            get
            {
                lock (_lock)
                {
                    if (!IsBuffering)
                        return null;

                    Debug.Assert(_bufferingProgress >= 0 && _bufferingProgress <= 1, "BufferingProgress out of range: " + _bufferingProgress);

                    return _bufferingProgress;
                }
            }
        }

        public void Dispose()
        {
            if (0 != Interlocked.Exchange(ref _isDisposed, 1))
                return;

            _disposeCancellationTokenSource.Cancel();

            using (_refreshTask)
            { }

            using (_reportingTask)
            { }
            _reportingTask = null;

            _disposeCancellationTokenSource.Dispose();

            lock (_lock)
            {
                _queues.Clear();
            }

            _queueThrottling = null;
        }

        public void Refresh()
        {
            ThrowIfDisposed();

            _refreshTask.Fire();
        }

        public void ReportExhaustion()
        {
            Debug.WriteLine("BufferingManager.ReportExhaustion()");

            lock (_lock)
            {
                if (_isEof || _isBuffering)
                    return;
            }

            RefreshHandler();
        }

        public void ReportEndOfData()
        {
            Debug.WriteLine("BufferingManager.ReportEndOfData()");

            bool wasEof;

            lock (_lock)
            {
                wasEof = _isEof;

                _isEof = true;

                UnlockedUpdateQueueStatus();

                UnlockedReport();
            }

            _refreshTask.Fire();

            if (!wasEof && null != _reportingTask)
                _reportingTask.Fire();
        }

        #endregion

        void ResizeStatuses()
        {
            while (_statuses.Count > _queues.Count)
                _statuses.RemoveAt(_statuses.Count - 1);
            while (_statuses.Count < _queues.Count)
                _statuses.Add(new BufferStatus());
        }

        void RefreshHandler()
        {
            lock (_lock)
            {
                UnlockedUpdateQueueStatus();

                UnlockedReport();
            }
        }

        void UnlockedUpdateQueueStatus()
        {
            for (var i = 0; i < _queues.Count; ++i)
                _queues[i].UpdateBufferStatus(_statuses[i]);
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

        void UnlockedStartBuffering()
        {
            var wasBuffering = _isBuffering;

            _isBuffering = true;

            if (!wasBuffering)
                return;

            ReportBuffering(0);
        }

        bool UpdateState()
        {
            //Debug.WriteLine("BufferingManager.UpdateState()");

            if (_statuses.Count <= 0)
                return false;

            var newest = TimeSpan.MinValue;
            var oldest = TimeSpan.MaxValue;
            var minDuration = TimeSpan.MaxValue;
            var lowestCount = int.MaxValue;
            var highestCount = int.MinValue;
            var totalBuffered = 0;

            var validData = false;
            var allDone = _isEof;
            var isExhausted = false;
            var allExhausted = true;

            foreach (var status in _statuses)
            {
                if (!status.IsDone)
                    allDone = false;

                if (!status.IsMedia)
                    continue;

                if (0 == status.PacketCount)
                    isExhausted = true;
                else
                    allExhausted = false;

                if (!status.IsValid)
                {
                    if (minDuration > TimeSpan.Zero)
                        minDuration = TimeSpan.Zero;

                    continue;
                }

                totalBuffered += status.Size;

                validData = true;

                var count = status.PacketCount;

                if (count < lowestCount)
                    lowestCount = count;

                if (count > highestCount)
                    highestCount = count;

                var newTime = status.Newest;

                if (newTime > newest)
                    newest = newTime.Value;

                var oldTime = status.Oldest;

                if (oldTime < oldest)
                    oldest = oldTime.Value;

                var duration = (newTime - oldTime) ?? TimeSpan.Zero;

                if (duration < TimeSpan.Zero)
                    duration = TimeSpan.Zero;

                if (duration < minDuration)
                    minDuration = duration;
            }

            if (allDone)
                _isEof = true;

            var timestampDifference = validData ? minDuration : TimeSpan.MaxValue;
            //var timestampDifference = validData ? newest - oldest : TimeSpan.MaxValue;

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

            if (_isBuffering)
            {
                if (allDone)
                {
                    _isBuffering = false;

                    Debug.WriteLine("BufferingManager.UpdateState done buffering (eof): duration {0} size {1} starting {2} memory {3:F} MiB",
                        validData ? timestampDifference.ToString() : "none",
                        validData ? totalBuffered.ToString() : "none",
                        _isStarting,
                        GC.GetTotalMemory(false).BytesToMiB());

                    DumpQueues();

                    ReportBuffering(1);
                }
                else if (validData)
                    UpdateBuffering(timestampDifference, totalBuffered);

                if (!_isBuffering)
                    return false;
            }
            else
            {
                //if (!allDone && isExhausted && (!validData || 0 == highestCount))
                //if (!allDone && allExhausted && validData)
                if (!allDone && isExhausted)
                {
                    Debug.WriteLine("BufferingManager.UpdateState start buffering: duration {0} size {1} starting {2} memory {3:F} MiB",
                        timestampDifference, totalBuffered, _isStarting, GC.GetTotalMemory(false).BytesToMiB());

                    DumpQueues();

                    _totalBufferedStart = totalBuffered;

                    UnlockedStartBuffering();

                    return false;
                }
            }

            if (!validData)
                return false;

            var shouldBlock = _bufferingPolicy.ShouldBlockReads(_blockReads, timestampDifference, totalBuffered, isExhausted, allExhausted);

#if DEBUG
            if (shouldBlock != _blockReads)
            {
                Debug.WriteLine("BufferingManager.UpdateState read blocking -> {0} duration {1} size {2} memory {3:F} MiB",
                    shouldBlock,
                    validData ? timestampDifference.ToString() : "none",
                    validData ? totalBuffered.ToString() : "none",
                    GC.GetTotalMemory(false).BytesToMiB());

                DumpQueues();
            }
#endif

            return shouldBlock;
        }

        [Conditional("DEBUG")]
        void DumpQueues()
        {
            foreach (var queue in _queues)
                Debug.WriteLine("  " + queue);
        }

        void UpdateBuffering(TimeSpan timestampDifference, int bufferSize)
        {
            if (_bufferingPolicy.IsDoneBuffering(timestampDifference, bufferSize, _totalBufferedStart, _isStarting))
            {
                _isBuffering = false;

                Debug.WriteLine("BufferingManager.UpdateBuffering done buffering: duration {0} size {1} starting {2} memory {3:F} MiB",
                    timestampDifference, bufferSize, _isStarting, GC.GetTotalMemory(false).BytesToMiB());

                DumpQueues();

                _isStarting = false;

                ReportBuffering(1);
            }
            else
            {
                var now = DateTime.UtcNow;

                var elapsed = now - _bufferStatusTimeUtc;

                if (elapsed >= BufferStatusUpdatePeriod)
                {
                    _bufferStatusTimeUtc = now;

                    var bufferingStatus = _bufferingPolicy.GetProgress(timestampDifference, bufferSize, _totalBufferedStart, _isStarting);

                    Debug.WriteLine("BufferingManager.UpdateBuffering: {0:F2}% duration {1} size {2} starting {3} memory {4:F} MiB",
                        bufferingStatus * 100, timestampDifference, bufferSize, _isStarting, GC.GetTotalMemory(false).BytesToMiB());

                    ReportBuffering(bufferingStatus);
                }
            }
        }

        void ReportBuffering(float bufferingProgress)
        {
            _bufferingProgress = bufferingProgress;

            if (null != _reportingTask)
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

        void ThrowIfDisposed()
        {
            if (0 != _isDisposed)
                throw new ObjectDisposedException(GetType().Name);
        }
    }
}

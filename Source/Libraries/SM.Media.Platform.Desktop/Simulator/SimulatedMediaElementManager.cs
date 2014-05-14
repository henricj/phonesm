// -----------------------------------------------------------------------
//  <copyright file="SimulatedMediaElementManager.cs" company="Henric Jungheim">
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SM.Media.Utility;
using SM.TsParser;

namespace SM.Media.Simulator
{
    public sealed class SimulatedMediaElementManager : ISimulatedMediaElement, IDisposable
    {
        readonly AsyncFifoWorker _asyncFifoWorker = new AsyncFifoWorker();
        readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        readonly object _lock = new object();
        // _mediaStreamFsm must not be readonly.  Member functions would then operate on *copies* of the value
        // rather than this field (since it is a value type).
        readonly MediaStreamFsm _mediaStreamFsm = new MediaStreamFsm();
        readonly RandomNumbers _random = new RandomNumbers();
        readonly Dictionary<int, SampleState> _streams = new Dictionary<int, SampleState>();
        ISimulatedMediaStreamSource _mediaStreamSource;
        int _streamCount;

        public SimulatedMediaElementManager()
        {
            _mediaStreamFsm.Reset();
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (!_cancellationTokenSource.IsCancellationRequested)
                _cancellationTokenSource.Cancel();

            using (_asyncFifoWorker)
            { }

            _cancellationTokenSource.Dispose();
        }

        #endregion

        #region ISimulatedMediaElement Members

        public void ReportOpenMediaCompleted(int streamCount)
        {
            Debug.WriteLine("SimulatedMediaElementManager.ReportOpenMediaCompleted()");

            _streamCount = streamCount;

            _asyncFifoWorker.Post(PlayMediaAsync, "SimulatedMediaElementManager.ReportOpenMediaCompleted() PlayMediaAsync", _cancellationTokenSource.Token);
        }

        public void ReportSeekCompleted(long ticks)
        {
            Debug.WriteLine("SimulatedMediaElementManager.ReportSeekCompleted(): " + TimeSpan.FromTicks(ticks));
        }

        public void ReportGetSampleProgress(float progress)
        {
            Debug.WriteLine("SimulatedMediaElementManager.ReportGetSampleProgress(): " + progress);
        }

        public void ReportGetSampleCompleted(int streamType, IStreamSource streamSource, TsPesPacket packet)
        {
            if (null == packet)
            {
                Debug.WriteLine("SimulatedMediaElementManager.ReportGetSampleCompleted({0}) null packet", streamType);

                return;
            }

            Debug.WriteLine("SimulatedMediaElementManager.ReportGetSampleCompleted({0}) at {1}/{2}", streamType, packet.PresentationTimestamp, packet.DecodeTimestamp);

            var timestamp = packet.PresentationTimestamp;
            var oldestTimestamp = TimeSpan.MaxValue;
            var oldestIndex = -1;

            lock (_lock)
            {
                SampleState sampleState;

                if (!_streams.TryGetValue(streamType, out sampleState))
                {
                    sampleState = new SampleState
                                  {
                                      IsPending = false,
                                      Timestamp = timestamp
                                  };

                    _streams[streamType] = sampleState;
                }
                else
                {
                    sampleState.IsPending = false;
                    sampleState.Timestamp = timestamp;
                }

                foreach (var kv in _streams)
                {
                    var sampleTimestamp = kv.Value.Timestamp;

                    if (sampleTimestamp >= oldestTimestamp)
                        continue;

                    oldestTimestamp = sampleTimestamp;
                    oldestIndex = kv.Key;
                }

                if (oldestIndex >= 0)
                {
                    if (streamType != oldestIndex)
                        sampleState = _streams[oldestIndex];

                    if (sampleState.IsPending)
                        oldestIndex = -1;
                    else
                        sampleState.IsPending = true;
                }
            }

            if (oldestIndex >= 0)
            {
                var t = Task.Run(async () =>
                                       {
                                           await Task.Delay((int)(10 * (1 + _random.GetRandomNumber()))).ConfigureAwait(false);

                                           var mediaStreamSource = _mediaStreamSource;

                                           if (null != mediaStreamSource)
                                               mediaStreamSource.GetSampleAsync(oldestIndex);
                                       });

                TaskCollector.Default.Add(t, "SimulatedMediaElementManager.ReportGetSampleCompleted");
            }
        }

        void ISimulatedMediaElement.ErrorOccurred(string message)
        {
            Debug.WriteLine("SimulatedMediaElement.ErrorOccurred({0})", (object)message);

            var task = Task.Run((Func<Task>)CloseAsync);

            TaskCollector.Default.Add(task, "SimulatedMediaElement.ErrorOccurred");
        }

        #endregion

        public void SetSource(ISimulatedMediaStreamSource source)
        {
            Debug.WriteLine("SimulatedMediaElementManager.SetSourceAsync()");

            source.ValidateEvent(MediaStreamFsm.MediaEvent.MediaStreamSourceAssigned);

            _mediaStreamSource = source;

            _asyncFifoWorker.Post(OpenMediaAsync, "SimulatedMediaElementManager.SetSourceAsync() OpenMediaAsync", _cancellationTokenSource.Token);
        }

        public Task CloseAsync()
        {
            Debug.WriteLine("SimulatedMediaElementManager.CloseAsync()");

            if (null != _mediaStreamSource)
                _mediaStreamSource.Dispose();

            _mediaStreamSource = null;

            return TplTaskExtensions.CompletedTask;
        }

        public Task Dispatch(Action action)
        {
            action();

            return TplTaskExtensions.CompletedTask;
        }

        async Task OpenMediaAsync()
        {
            Debug.WriteLine("SimulatedMediaElementManager.OpenMediaAsync()");

            await Task.Delay(100).ConfigureAwait(false);

            _mediaStreamSource.OpenMediaAsync();
        }

        async Task PlayMediaAsync()
        {
            Debug.WriteLine("SimulatedMediaElementManager.PlayMediaAsync()");

            var random = _random.GetRandomNumbers(2 + _streamCount);

            await Task.Delay((int)(50 * (1 + random[0]))).ConfigureAwait(false);

            if (null == _mediaStreamSource)
                return;

            var taskActions = new List<Func<Task>>();

            Func<Task> t =
                async () =>
                {
                    await Task.Delay((int)(30 * (1 + random[1]))).ConfigureAwait(false);

                    _mediaStreamSource.SeekAsync(0);
                };

            taskActions.Add(t);

            for (var i = 0; i < _streamCount; ++i)
            {
                var streamType = i;

                t = async () =>
                          {
                              await Task.Delay((int)(30 * (1 + random[2 + streamType]))).ConfigureAwait(false);

                              _mediaStreamSource.GetSampleAsync(streamType);
                          };

                taskActions.Add(t);
            }

            _random.Shuffle(taskActions);

            await Task.WhenAll(taskActions.Select(Task.Run)).ConfigureAwait(false);
        }

        async Task PlayAsync()
        {
            await Task.Delay((int)(_random.GetRandomNumber() * 250 + 100));
        }

        public void Play()
        {
            _asyncFifoWorker.Post(PlayAsync, "SimulatedMediaElementManager.Play() PlayAsync", _cancellationTokenSource.Token);
        }

        #region Nested type: SampleState

        class SampleState
        {
            public TimeSpan Timestamp { get; set; }
            public bool IsPending { get; set; }
        }

        #endregion
    }
}

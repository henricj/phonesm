// -----------------------------------------------------------------------
//  <copyright file="SimulatedMediaElementManager.cs" company="Henric Jungheim">
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
using System.Linq;
using System.Threading.Tasks;
using SM.Media;
using SM.Media.Utility;

namespace SimulatedPlayer
{
    sealed class SimulatedMediaElementManager : IMediaElementManager, ISimulatedMediaElement, IDisposable
    {
        readonly CommandWorker _commandWorker = new CommandWorker();
        readonly object _lock = new object();
        readonly RandomNumbers _random = new RandomNumbers();
        readonly Dictionary<int, SampleState> _streams = new Dictionary<int, SampleState>();
        MediaStreamFsm _mediaStreamFsm = new MediaStreamFsm();
        ISimulatedMediaStreamSource _mediaStreamSource;

        public SimulatedMediaElementManager()
        {
            _mediaStreamFsm.Reset();
        }

        #region IDisposable Members

        public void Dispose()
        {
            using (_commandWorker)
            { }
        }

        #endregion

        #region IMediaElementManager Members

        Task IMediaElementManager.Dispatch(Action action)
        {
            return Dispatch(action);
        }

        public void ValidateEvent(MediaStreamFsm.MediaEvent mediaEvent)
        {
            _mediaStreamFsm.ValidateEvent(mediaEvent);
        }

        Task IMediaElementManager.Close()
        {
            return Close();
        }

        Task IMediaElementManager.SetSource(IMediaStreamSource source)
        {
            return SetSource(source);
        }

        #endregion

        #region ISimulatedMediaElement Members

        public void ReportOpenMediaCompleted()
        {
            _commandWorker.SendCommand(new CommandWorker.Command(PlayMedia));
        }

        public void ReportSeekCompleted(long ticks)
        { }

        public void ReportGetSampleProgress(double progress)
        { }

        public void ReportGetSampleCompleted(int streamType, IStreamSample sample)
        {
            if (null == sample)
                return;

            var timestamp = sample.Timestamp;
            var oldestTimestamp = TimeSpan.MaxValue;
            var oldestIndex = -1;

            lock (_lock)
            {
                SampleState sampleState;

                if (!_streams.TryGetValue(streamType, out sampleState))
                {
                    sampleState = new SampleState { IsPending = false, Timestamp = timestamp };

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
                Task.Run(async () =>
                               {
                                   await Task.Delay((int)(10 * (1 + _random.GetRandomNumber())));

                                   var mediaStreamSource = _mediaStreamSource;

                                   if (null != mediaStreamSource)
                                       mediaStreamSource.GetSampleAsync(oldestIndex);
                               });
        }

        #endregion

        public Task SetSource(IMediaStreamSource source)
        {
            ValidateEvent(MediaStreamFsm.MediaEvent.MediaStreamSourceAssigned);

            _mediaStreamSource = (ISimulatedMediaStreamSource)source;

            _commandWorker.SendCommand(new CommandWorker.Command(OpenMedia));

            return TplTaskExtensions.CompletedTask;
        }

        public Task Close()
        {
            if (null != _mediaStreamSource)
            {
                _mediaStreamSource.Dispose();
            }

            _mediaStreamSource = null;

            return TplTaskExtensions.CompletedTask;
        }

        public Task Dispatch(Action action)
        {
            action();

            return TplTaskExtensions.CompletedTask;
        }

        async Task OpenMedia()
        {
            await Task.Delay(100);

            _mediaStreamSource.OpenMediaAsync();
        }

        async Task PlayMedia()
        {
            var random = _random.GetRandomNumbers(4);

            await Task.Delay((int)(50 * (1 + random[3])));

            var taskActions = new List<Func<Task>>();

            Func<Task> t =
                async () =>
                {
                    await Task.Delay((int)(30 * (1 + random[0])));

                    _mediaStreamSource.SeekAsync(0);
                };

            taskActions.Add(t);

            t = async () =>
                      {
                          await Task.Delay((int)(30 * (1 + random[0])));

                          _mediaStreamSource.GetSampleAsync(0);
                      };

            taskActions.Add(t);

            t = async () =>
                      {
                          await Task.Delay((int)(30 * (1 + random[0])));

                          _mediaStreamSource.GetSampleAsync(1);
                      };

            taskActions.Add(t);

            _random.Shuffle(taskActions);

            await Task.WhenAll(taskActions.Select(Task.Run));
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

﻿// -----------------------------------------------------------------------
//  <copyright file="SimulatedMediaStreamSource.cs" company="Henric Jungheim">
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
using System.Threading.Tasks;
using SM.Media;
using SM.Media.Utility;

namespace SimulatedPlayer
{
    class SimulatedMediaStreamSource : ISimulatedMediaStreamSource
    {
        readonly TaskCommandWorker _commandWorker = new TaskCommandWorker();
        readonly object _lock = new object();
        readonly ISimulatedMediaElement _mediaElement;
        readonly List<IStreamSource> _mediaStreams = new List<IStreamSource>();
        readonly List<WorkCommand> _pendingGets = new List<WorkCommand>();
        readonly object _stateLock = new object();
        readonly List<PacketStreamWrapper> _streamWrappers = new List<PacketStreamWrapper>();
        bool _isClosed;
        MediaStreamFsm _mediaStreamFsm = new MediaStreamFsm();
        int _pendingRequests;
        State _state;

        public SimulatedMediaStreamSource(ISimulatedMediaElement mediaElement)
        {
            _mediaElement = mediaElement;

            _mediaStreamFsm.Reset();
        }

        #region ISimulatedMediaStreamSource Members

        public void Dispose()
        {
            using (_commandWorker)
            { }

            foreach (var wrapper in _streamWrappers)
                wrapper.Dispose();
        }

        public IMediaManager MediaManager { get; set; }

        Task IMediaStreamSource.CloseAsync()
        {
            return CloseAsync();
        }

        public void Configure(MediaConfiguration configuration)
        {
            lock (_lock)
            {
                if (null != configuration.VideoConfiguration)
                {
                    _mediaStreams.Add(configuration.VideoStream);
                    _streamWrappers.Add(new PacketStreamWrapper(configuration.VideoStream));

                    var streamType = _mediaStreams.Count - 1;
                }

                if (null != configuration.AudioConfiguration)
                {
                    _mediaStreams.Add(configuration.AudioStream);
                    _streamWrappers.Add(new PacketStreamWrapper(configuration.AudioStream));

                    var streamType = _mediaStreams.Count - 1;
                }
            }

            Debug.WriteLine("SimulatedMediaStreamSource: ReportOpenMediaCompleted ({0} streams)", _mediaStreams.Count);

            ValidateEvent(MediaStreamFsm.MediaEvent.CallingReportOpenMediaCompleted);

            _mediaElement.ReportOpenMediaCompleted();
        }

        public void ReportError(string message)
        {
            Debug.WriteLine("SimulatedMediaStreamSource.ReportError({0})", message);

            _mediaElement.ErrorOccurred(message);
        }

        public TimeSpan? SeekTarget { get; set; }

        public void OpenMediaAsync()
        {
            Debug.WriteLine("SimulatedMediaStreamSource.OpenMediaAsync()");
            ValidateEvent(MediaStreamFsm.MediaEvent.OpenMediaAsyncCalled);

            var mediaManager = MediaManager;

            if (null == mediaManager)
                throw new InvalidOperationException("MediaManager has not been initialized");

            mediaManager.OpenMedia();
        }

        public void SeekAsync(long seekToTime)
        {
            var seekTimestamp = TimeSpan.FromTicks(seekToTime);

            Debug.WriteLine("SimulatedMediaStreamSource.SeekAsync({0})", seekTimestamp);
            ValidateEvent(MediaStreamFsm.MediaEvent.SeekAsyncCalled);

            var mediaManager = MediaManager;

            if (null == mediaManager)
                throw new InvalidOperationException("MediaManager has not been initialized");

            _commandWorker.SendCommand(new WorkCommand(
                async () =>
                {
                    if (_isClosed)
                        return;

                    var position = await mediaManager.SeekMediaAsync(seekTimestamp).ConfigureAwait(false);

                    if (_isClosed)
                        return;

                    ValidateEvent(MediaStreamFsm.MediaEvent.CallingReportSeekCompleted);
                    _mediaElement.ReportSeekCompleted(position.Ticks);

                    WorkCommand[] pendingGets;

                    lock (_stateLock)
                    {
                        pendingGets = _pendingGets.ToArray();

                        _pendingGets.Clear();

                        _state = State.Play;
                    }

                    foreach (var getCmd in pendingGets)
                        _commandWorker.SendCommand(getCmd);
                }));
        }

        public void GetSampleAsync(int streamType)
        {
            var command = new WorkCommand(
                () =>
                {
                    if (streamType < 0)
                        return null;

                    IStreamSource streamSource = null;
                    PacketStreamWrapper wrapper = null;

                    lock (_lock)
                    {
                        if (streamType >= _mediaStreams.Count)
                            return null;

                        streamSource = _mediaStreams[streamType];
                        wrapper = _streamWrappers[streamType];
                    }

                    if (null == streamSource)
                    {
                        _mediaElement.ReportGetSampleProgress(0);
                        return null;
                    }

                    var completed = wrapper.GetNextSample(sample => StreamSampleHandler(streamType, sample));

                    if (!completed)
                    {
                        var current = _pendingRequests;

                        for (; ; )
                        {
                            var newFlags = current | (1 << streamType);

                            if (newFlags == current)
                                break;

                            var existing = Interlocked.CompareExchange(ref _pendingRequests, newFlags, current);

                            if (existing == current)
                                break;

                            current = existing;
                        }
                    }

                    return null;
                });

            lock (_stateLock)
            {
                var state = _state;

                //Debug.WriteLine("SimulatedMediaStreamSource.GetSampleAsync({0}) state {1}", streamType, state);

                ValidateEvent(MediaStreamFsm.MediaEvent.GetSampleAsyncCalled);

                if (State.Play != state)
                {
                    Debug.WriteLine("SimulatedMediaStreamSource defer Get({0})", streamType);
                    _pendingGets.Add(command);
                }
                else
                    _commandWorker.SendCommand(command);
            }
        }

        public void CloseMedia()
        {
            Debug.WriteLine("SimulatedMediaStreamSource.CloseMedia()");
            ValidateEvent(MediaStreamFsm.MediaEvent.CloseMediaCalled);

            lock (_stateLock)
            {
                _isClosed = true;

                _state = State.Closed;
            }

            var mediaManager = MediaManager;

            if (null == mediaManager)
                throw new InvalidOperationException("MediaManager has not been initialized");

            mediaManager.CloseMedia();
        }

        public void CheckForSamples()
        {
            var requested = Interlocked.Exchange(ref _pendingRequests, 0);

            for (var i = 0; 0 != requested; ++i, requested >>= 1)
            {
                if (0 == (requested & 1))
                    continue;

                GetSampleAsync(i);
            }
        }

        public void ValidateEvent(MediaStreamFsm.MediaEvent mediaEvent)
        {
            _mediaStreamFsm.ValidateEvent(mediaEvent);
        }

        #endregion

        bool StreamSampleHandler(int streamType, IStreamSample sample)
        {
            ValidateEvent(MediaStreamFsm.MediaEvent.CallingReportSampleCompleted);
            _mediaElement.ReportGetSampleCompleted(streamType, sample);

            return true;
        }

        public Task CloseAsync()
        {
            return TplTaskExtensions.CompletedTask;
        }

        #region Nested type: State

        enum State
        {
            Idle,
            Open,
            Seek,
            Play,
            Closed,
            WaitForClose
        }

        #endregion
    }
}

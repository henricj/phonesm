// -----------------------------------------------------------------------
//  <copyright file="SimulatedMediaStreamSource.cs" company="Henric Jungheim">
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
using System.Threading.Tasks;
using SM.Media.MediaParser;
using SM.Media.Utility;
using SM.TsParser;

namespace SM.Media.Simulator
{
    public class SimulatedMediaStreamSource : ISimulatedMediaStreamSource
    {
        readonly AsyncFifoWorker _asyncFifoWorker = new AsyncFifoWorker();
        readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        readonly object _lock = new object();
        readonly ISimulatedMediaElement _mediaElement;
        readonly List<IStreamSource> _mediaStreams = new List<IStreamSource>();
        readonly object _stateLock = new object();
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
            if (!_cancellationTokenSource.IsCancellationRequested)
                _cancellationTokenSource.Cancel();

            using (_asyncFifoWorker)
            { }

            _cancellationTokenSource.Dispose();
        }

        public IMediaManager MediaManager { get; set; }

        Task IMediaStreamSource.CloseAsync()
        {
            return CloseAsync();
        }

        public void Configure(IMediaConfiguration configuration)
        {
            lock (_lock)
            {
                if (null != configuration.Video)
                {
                    _mediaStreams.Add(configuration.Video.StreamSource);

                    var streamType = _mediaStreams.Count - 1;
                }

                if (null != configuration.Audio)
                {
                    _mediaStreams.Add(configuration.Audio.StreamSource);

                    var streamType = _mediaStreams.Count - 1;
                }
            }

            Debug.WriteLine("SimulatedMediaStreamSource: ReportOpenMediaCompleted ({0} streams)", _mediaStreams.Count);

            ValidateEvent(MediaStreamFsm.MediaEvent.CallingReportOpenMediaCompleted);

            _mediaElement.ReportOpenMediaCompleted(_mediaStreams.Count);
        }

        public void ReportError(string message)
        {
            Debug.WriteLine("SimulatedMediaStreamSource.ReportError(): " + message);

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

            _asyncFifoWorker.Post(
                async () =>
                {
                    if (_isClosed)
                        return;

                    try
                    {
                        var position = await mediaManager.SeekMediaAsync(seekTimestamp).ConfigureAwait(false);

                        if (_isClosed)
                            return;

                        Debug.WriteLine("SimulatedMediaStreamSource.SeekAsync({0}) actual {1}", seekTimestamp, position);

                        ValidateEvent(MediaStreamFsm.MediaEvent.CallingReportSeekCompleted);
                        _mediaElement.ReportSeekCompleted(position.Ticks);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("SimulatedMediaStreamSource.SeekAsync() failed: " + ex.Message);
                    }

                    lock (_stateLock)
                    {
                        _state = State.Play;
                    }

                    if (0 != _pendingRequests)
                        _asyncFifoWorker.Post(HandleSamples, "SimulatedMediaStreamSource.SeekAsync() HandleSamples", _cancellationTokenSource.Token);
                }, "SimulatedMediaStreamSource.SeekAsync() SeekMediaAsync", _cancellationTokenSource.Token);
        }

        public void GetSampleAsync(int streamType)
        {
            //Debug.WriteLine("SimulatedMediaStreamSource.GetSampleAsync({0})", streamType);

            ValidateEvent(MediaStreamFsm.MediaEvent.GetSampleAsyncCalled);

            RequestGet(streamType);

            _asyncFifoWorker.Post(HandleSamples, "SimulatedMediaStreamSource.GetSampleAsync() HandleSamples", _cancellationTokenSource.Token);
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
            Debug.WriteLine("SimulatedMediaStreamSource.CheckForSamples: pending {0}", _pendingRequests);

            if (0 != _pendingRequests)
                _asyncFifoWorker.Post(HandleSamples, "SimulatedMediaStreamSource.CheckForSamples() HandleSamples", _cancellationTokenSource.Token);
        }

        public void ValidateEvent(MediaStreamFsm.MediaEvent mediaEvent)
        {
            _mediaStreamFsm.ValidateEvent(mediaEvent);
        }

        #endregion

        void HandleSample(int streamType)
        {
            lock (_stateLock)
            {
                var state = _state;

                if (State.Play != state)
                {
                    Debug.WriteLine("SimulatedMediaStreamSource defer Get({0})", streamType);
                    RequestGet(streamType);

                    return;
                }
            }

            if (streamType < 0)
                return;

            IStreamSource streamSource = null;

            lock (_lock)
            {
                if (streamType >= _mediaStreams.Count)
                    return;

                streamSource = _mediaStreams[streamType];
            }

            if (null == streamSource)
            {
                _mediaElement.ReportGetSampleProgress(0);
                return;
            }

            var packet = streamSource.GetNextSample();

            try
            {
                if (null != packet || streamSource.IsEof)
                    StreamSampleHandler(streamType, streamSource, packet);
            }
            finally
            {
                if (null != packet)
                    streamSource.FreeSample(packet);
            }

            var completed = null != packet;

            if (!completed)
                RequestGet(streamType);
        }

        void RequestGet(int streamType)
        {
            if (streamType >= _mediaStreams.Count)
            {
                Debug.WriteLine("SimulatedMediaStreamSource.RequestGet() requesting unknown stream " + streamType);
                return;
            }

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

        Task HandleSamples()
        {
            var requested = Interlocked.Exchange(ref _pendingRequests, 0);

            for (var i = 0; 0 != requested; ++i, requested >>= 1)
            {
                if (0 == (requested & 1))
                    continue;

                HandleSample(i);
            }

            return TplTaskExtensions.CompletedTask;
        }

        bool StreamSampleHandler(int streamType, IStreamSource streamSource, TsPesPacket packet)
        {
            ValidateEvent(MediaStreamFsm.MediaEvent.CallingReportSampleCompleted);
            _mediaElement.ReportGetSampleCompleted(streamType, streamSource, packet);

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

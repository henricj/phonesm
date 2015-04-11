// -----------------------------------------------------------------------
//  <copyright file="WinRtMediaStreamConfigurator.cs" company="Henric Jungheim">
//  Copyright (c) 2012-2015.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012-2015 Henric Jungheim <software@henric.org>
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
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using SM.Media.Configuration;
using SM.Media.MediaManager;
using SM.Media.MediaParser;
using SM.Media.Utility;

namespace SM.Media
{
    public sealed class WinRtMediaStreamConfigurator : IMediaStreamConfigurator
    {
#if DEBUG
        MediaStreamFsm _mediaStreamFsm = new MediaStreamFsm();
#endif
        static readonly TimeSpan ResyncThreshold = TimeSpan.FromSeconds(5);
        static readonly WinRtStreamState[] NoStreamStates = new WinRtStreamState[0];
        readonly object _stateLock = new object();
        readonly object _streamConfigurationLock = new object();
        int _isDisposed;
        TimeSpan? _seekTarget;
        TimeSpan? _duration;
        MediaStreamSourceStartingRequestDeferral _onStartingDeferral;
        MediaStreamSourceStartingRequest _onStartingRequest;
        MediaStreamSource _mediaStreamSource;
        TaskCompletionSource<IMediaSource> _mediaStreamCompletionSource = new TaskCompletionSource<IMediaSource>();
        TaskCompletionSource<object> _playingCompletionSource;
        CancellationTokenRegistration? _playingCancellationTokenRegistration;
        WinRtStreamState[] _streamStates = NoStreamStates;
        bool _startingCalled;

        bool IsDisposed
        {
            get { return 0 != _isDisposed; }
        }

        bool IsBuffering
        {
            get { return _streamStates.Any(s => s.IsBuffering); }
        }

        public WinRtMediaStreamConfigurator()
        {
#if DEBUG
            _mediaStreamFsm.Reset();
#endif
        }

        #region IMediaStreamSource Members

        public TimeSpan? SeekTarget
        {
            get { lock (_stateLock) return _seekTarget; }
            set { lock (_stateLock) _seekTarget = value; }
        }

        public IMediaManager MediaManager { get; set; }

        /// <summary>
        ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (0 != Interlocked.Exchange(ref _isDisposed, 1))
                return;

            Debug.WriteLine("WinRtMediaStreamConfigurator.Dispose()");
            ValidateEvent(MediaStreamFsm.MediaEvent.DisposeCalled);

            if (null != _mediaStreamCompletionSource)
                _mediaStreamCompletionSource.TrySetCanceled();

            TaskCompletionSource<object> playingCompletionSource;

            lock (_stateLock)
            {
                playingCompletionSource = _playingCompletionSource;
                _playingCompletionSource = null;
            }

            if (null != playingCompletionSource)
                playingCompletionSource.TrySetCanceled();

            DisposePlayingCancellationRegistration();
        }

        public Task PlayAsync(IMediaConfiguration configuration, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            var streamStates = new List<WinRtStreamState>();

            WinRtStreamState videoStreamState = null;
            WinRtStreamState audioStreamState = null;

            if (null != configuration.Audio)
            {
                var descriptor = CreateAudioDescriptor((IAudioConfigurationSource)configuration.Audio.ConfigurationSource);

                var contentType = configuration.Audio.ConfigurationSource.ContentType;

                var state = new WinRtStreamState("Audio", contentType, configuration.Audio.StreamSource, descriptor);

                audioStreamState = state;

                streamStates.Add(state);
            }

            if (null != configuration.Video)
            {
                var descriptor = CreateVideoDescriptor((IVideoConfigurationSource)configuration.Video.ConfigurationSource);

                var contentType = configuration.Video.ConfigurationSource.ContentType;

                var state = new WinRtStreamState("Video", contentType, configuration.Video.StreamSource, descriptor);

                videoStreamState = state;

                streamStates.Add(state);
            }

            if (null != configuration.AlternateStreams && configuration.AlternateStreams.Count > 0)
            {
                var audioCount = 1;

                foreach (var stream in configuration.AlternateStreams)
                {
                    var audioConfigurationSource = stream.ConfigurationSource as IAudioConfigurationSource;

                    if (null == audioConfigurationSource)
                        continue;

                    var descriptor = CreateAudioDescriptor(audioConfigurationSource);

                    var contentType = audioConfigurationSource.ContentType;

                    ++audioCount;

                    var audioStream = new WinRtStreamState("Audio " + audioCount, contentType, stream.StreamSource, descriptor);

                    streamStates.Add(audioStream);
                }
            }

            var allStreams = streamStates.ToArray();

            lock (_streamConfigurationLock)
            {
                _streamStates = allStreams;

                _duration = configuration.Duration;

                // Note that the lock is released without awaiting the Task returned by
                // ConfigureAsync().
                return ConfigureAsync(videoStreamState, audioStreamState, cancellationToken);
            }
        }

        public void ReportError(string message)
        {
            Debug.WriteLine("WinRtMediaStreamConfigurator.ReportError(): " + message);
        }

        public Task CloseAsync()
        {
            Debug.WriteLine("WinRtMediaStreamConfigurator.CloseAsync()");

            ThrowIfDisposed();

            foreach (var state in _streamStates)
                state.Close();

            CancelMediaStreamCompletion();

            bool startingCalled;
            TaskCompletionSource<object> playingCompletionSource = null;

            lock (_stateLock)
            {
                playingCompletionSource = _playingCompletionSource;

                if (null == playingCompletionSource)
                    return TplTaskExtensions.CompletedTask;

                startingCalled = _startingCalled;
            }

            if (!startingCalled)
                playingCompletionSource.TrySetCanceled();

            return playingCompletionSource.Task;
        }

        void CancelPending()
        {
            Debug.WriteLine("WinRtMediaStreamConfigurator.CancelPending()");

            foreach (var state in _streamStates)
                state.Cancel();
        }

        public void ValidateEvent(MediaStreamFsm.MediaEvent mediaEvent)
        {
#if DEBUG
            _mediaStreamFsm.ValidateEvent(mediaEvent);
#endif
        }

        public void CheckForSamples()
        {
            //Debug.WriteLine("WinRtMediaStreamConfigurator.CheckForSamples()");

            if (null != _onStartingDeferral)
                CompleteOnStarting();

            foreach (var state in _streamStates)
                state.CheckForSamples();
        }

        #endregion

        void ThrowIfDisposed()
        {
            if (!IsDisposed)
                return;

            throw new ObjectDisposedException(GetType().Name);
        }

        VideoEncodingProperties GetVideoEncodingProperties(IVideoConfigurationSource configurationSource)
        {
            switch (configurationSource.VideoFourCc)
            {
                case "H264":
                    return VideoEncodingProperties.CreateH264();
                case "MP2V":
                    return VideoEncodingProperties.CreateMpeg2();
                default:
                    return null;
            }
        }

        IMediaStreamDescriptor CreateVideoDescriptor(IVideoConfigurationSource configurationSource)
        {
            var encodingProperties = GetVideoEncodingProperties(configurationSource);

            if (null == encodingProperties)
                throw new ArgumentOutOfRangeException();

            if (configurationSource.Height.HasValue)
                encodingProperties.Height = (uint)configurationSource.Height.Value;

            if (configurationSource.Width.HasValue)
                encodingProperties.Width = (uint)configurationSource.Width.Value;

            if (configurationSource.FrameRateNumerator.HasValue && configurationSource.FrameRateDenominator.HasValue)
            {
                encodingProperties.FrameRate.Numerator = (uint)configurationSource.FrameRateNumerator.Value;
                encodingProperties.FrameRate.Denominator = (uint)configurationSource.FrameRateDenominator.Value;
            }

            var descriptor = new VideoStreamDescriptor(encodingProperties);

            if (string.IsNullOrEmpty(descriptor.Name))
                descriptor.Name = configurationSource.Name;

            return descriptor;
        }

        IMediaStreamDescriptor CreateAudioDescriptor(IAudioConfigurationSource configurationSource)
        {
            Func<uint, uint, uint, AudioEncodingProperties> propertyFactory;

            switch (configurationSource.Format)
            {
                case AudioFormat.Mp3:
                    propertyFactory = AudioEncodingProperties.CreateMp3;
                    break;
                case AudioFormat.AacRaw:
                    propertyFactory = AudioEncodingProperties.CreateAac;
                    break;
                case AudioFormat.AacAdts:
                    propertyFactory = AudioEncodingProperties.CreateAacAdts;
                    break;
                case AudioFormat.Ac3:
                    var encodingProperties = new AudioEncodingProperties
                    {
                        Subtype = "Ac3",
                        SampleRate = (uint)configurationSource.SamplingFrequency
                    };

                    if (configurationSource.Bitrate.HasValue)
                        encodingProperties.Bitrate = (uint)configurationSource.Bitrate.Value;

                    var ac3Descriptor = new AudioStreamDescriptor(encodingProperties);

                    DumpAudioStreamDescriptor(ac3Descriptor);

                    return ac3Descriptor;
                case AudioFormat.Unknown:
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var audioEncodingProperties = propertyFactory((uint)configurationSource.SamplingFrequency,
                (uint)configurationSource.Channels, (uint?)configurationSource.Bitrate ?? 128000u);

            var descriptor = new AudioStreamDescriptor(audioEncodingProperties);

            if (string.IsNullOrEmpty(descriptor.Name))
                descriptor.Name = configurationSource.Name;

            DumpAudioStreamDescriptor(descriptor);

            return descriptor;
        }

        static void DumpAudioStreamDescriptor(AudioStreamDescriptor descriptor)
        {
            var p = descriptor.EncodingProperties;

            Debug.WriteLine("WinRtMediaStreamConfigurator.DumpAudioStreamDescriptor() {0} sample rate {1} channels {2} bitrate {3}",
                p.Subtype, p.SampleRate, p.ChannelCount, p.Bitrate);
        }

        Task ConfigureAsync(WinRtStreamState videoStreamState, WinRtStreamState audioStreamState, CancellationToken cancellationToken)
        {
            Debug.WriteLine("WinRtMediaStreamConfigurator.ConfigureAsync()");

            ThrowIfDisposed();

            var playingTaskCompletionSource = new TaskCompletionSource<object>();

            if (0 != _isDisposed)
                playingTaskCompletionSource.TrySetException(new ObjectDisposedException(GetType().Name));

            if (cancellationToken.IsCancellationRequested)
                playingTaskCompletionSource.TrySetCanceled();

            lock (_stateLock)
            {
                if (null != _playingCompletionSource)
                    throw new InvalidOperationException("Playback is already in progress");

                _playingCompletionSource = playingTaskCompletionSource;

                if (playingTaskCompletionSource.Task.IsCompleted)
                    return playingTaskCompletionSource.Task;
            }

            var mss = CreateMediaStreamSource(videoStreamState, audioStreamState);

            _mediaStreamSource = mss;

            if (_mediaStreamCompletionSource.TrySetResult(mss))
            {
                _playingCancellationTokenRegistration = cancellationToken.Register(() => mss.NotifyError(MediaStreamSourceErrorStatus.Other));

                return playingTaskCompletionSource.Task;
            }

            DeregisterCallbacks(mss);

            _mediaStreamSource = null;

            playingTaskCompletionSource.TrySetCanceled();

            return playingTaskCompletionSource.Task;
        }

        MediaStreamSource CreateMediaStreamSource(WinRtStreamState videoStreamState, WinRtStreamState audioStreamState)
        {
            MediaStreamSource mediaStreamSource;

            if (null != videoStreamState && null != audioStreamState)
                mediaStreamSource = new MediaStreamSource(videoStreamState.Descriptor, audioStreamState.Descriptor);
            else if (null != videoStreamState)
                mediaStreamSource = new MediaStreamSource(videoStreamState.Descriptor);
            else if (null != audioStreamState)
                mediaStreamSource = new MediaStreamSource(audioStreamState.Descriptor);
            else
                throw new InvalidOperationException("No streams configured");

            mediaStreamSource.CanSeek = _duration.HasValue;

            if (_duration.HasValue)
                mediaStreamSource.Duration = _duration.Value;

            foreach (var state in _streamStates)
            {
                if (ReferenceEquals(audioStreamState, state) || ReferenceEquals(videoStreamState, state))
                    continue;

                mediaStreamSource.AddStreamDescriptor(state.Descriptor);
            }

            RegisterCallbacks(mediaStreamSource);

            return mediaStreamSource;
        }

        void RegisterCallbacks(MediaStreamSource mediaStreamSource)
        {
            mediaStreamSource.Starting += MediaStreamSourceOnStarting;
            mediaStreamSource.SampleRequested += MediaStreamSourceOnSampleRequested;
            mediaStreamSource.Closed += MediaStreamSourceOnClosed;

            mediaStreamSource.Paused += MediaStreamSourceOnPaused;
            mediaStreamSource.SwitchStreamsRequested += MediaStreamSourceOnSwitchStreamsRequested;
        }

        void DeregisterCallbacks(MediaStreamSource mediaStreamSource)
        {
            mediaStreamSource.SwitchStreamsRequested -= MediaStreamSourceOnSwitchStreamsRequested;
            mediaStreamSource.Paused -= MediaStreamSourceOnPaused;

            mediaStreamSource.Closed -= MediaStreamSourceOnClosed;
            mediaStreamSource.SampleRequested -= MediaStreamSourceOnSampleRequested;
            mediaStreamSource.Starting -= MediaStreamSourceOnStarting;
        }

        void MediaStreamSourceOnSwitchStreamsRequested(MediaStreamSource sender, MediaStreamSourceSwitchStreamsRequestedEventArgs args)
        {
            Debug.WriteLine("WinRtMediaStreamConfigurator.MediaStreamSourceOnSwitchStreamsRequested()");

            NotifyOnError(sender);
        }

        void MediaStreamSourceOnPaused(MediaStreamSource sender, object args)
        {
            Debug.WriteLine("WinRtMediaStreamConfigurator.MediaStreamSourceOnPaused() pts " + GetCurrentTimestamp());

            NotifyOnError(sender);
        }

        void MediaStreamSourceOnClosed(MediaStreamSource sender, MediaStreamSourceClosedEventArgs args)
        {
            var reason = args.Request.Reason;

            Debug.WriteLine("WinRtMediaStreamConfigurator.MediaStreamSourceOnClosed() reason: " + reason);

            DeregisterCallbacks(sender);

            var startDeferral = Interlocked.Exchange(ref _onStartingDeferral, null);

            if (null != startDeferral)
            {
                Debug.WriteLine("WinRtMediaStreamConfigurator.MediaStreamSourceOnClosed() start was still pending");
                startDeferral.Complete();
            }

            CancelPending();

            _mediaStreamSource = null;

            CancelMediaStreamCompletion();

            CompletePlayingTask(reason);
        }

        void CompletePlayingTask(MediaStreamSourceClosedReason reason)
        {
            Debug.WriteLine("WinRtMediaStreamConfigurator.CompletePlayingTask() reason: " + reason);

            TaskCompletionSource<object> playingCompletionSource;

            lock (_stateLock)
            {
                playingCompletionSource = _playingCompletionSource;
            }

            if (null == playingCompletionSource)
            {
                Debug.WriteLine("**** WinRtMediaStreamConfigurator.MediaStreamSourceOnClosed() no playing task");
                return;
            }

            DisposePlayingCancellationRegistration();

            WinRtStreamState[] streamStates;

            lock (_streamConfigurationLock)
            {
                streamStates = _streamStates;
                _streamStates = NoStreamStates;
            }

            if (null != streamStates)
            {
                foreach (var state in streamStates)
                    state.Close();
            }

            switch (reason)
            {
                case MediaStreamSourceClosedReason.Done:
                    playingCompletionSource.TrySetResult(null);
                    break;
                case MediaStreamSourceClosedReason.UnsupportedEncodingFormat:
                    playingCompletionSource.TrySetException(new NotSupportedException("Playback failed: " + reason));
                    break;
                case MediaStreamSourceClosedReason.AppReportedError:
                    playingCompletionSource.TrySetCanceled();
                    break;
                default:
                    playingCompletionSource.TrySetException(new Exception("Playback failed: " + reason));
                    break;
            }
        }

        void CancelMediaStreamCompletion()
        {
            if (null != _mediaStreamCompletionSource && _mediaStreamCompletionSource.Task.IsCompleted)
                _mediaStreamCompletionSource.TrySetCanceled();
        }

        void DisposePlayingCancellationRegistration()
        {
            var registration = _playingCancellationTokenRegistration;

            if (!registration.HasValue)
                return;

            _playingCancellationTokenRegistration = null;

            registration.Value.Dispose();
        }

        void MediaStreamSourceOnSampleRequested(MediaStreamSource sender, MediaStreamSourceSampleRequestedEventArgs args)
        {
            //Debug.WriteLine("WinRtMediaStreamConfigurator.MediaStreamSourceOnSampleRequested()");

            // We let "NotifyOnError()" tell the MediaStreamSource if it is time to quit,
            // but we keep processing samples until the MediaStreamSource tells us otherwise.
            NotifyOnError(sender);

            var request = args.Request;

            var state = _streamStates.FirstOrDefault(s => ReferenceEquals(s.Descriptor, request.StreamDescriptor));

            if (null == state)
            {
                Debug.WriteLine("WinRtMediaStreamConfigurator.MediaStreamSourceOnSampleRequested() unknown stream: " + request.StreamDescriptor.Name);
                return;
            }

            state.SampleRequested(request);
        }

        async void MediaStreamSourceOnStarting(MediaStreamSource sender, MediaStreamSourceStartingEventArgs args)
        {
            var request = args.Request;
            var startPosition = request.StartPosition;

            Debug.WriteLine("WinRtMediaStreamConfigurator.MediaStreamSourceOnStarting({0})", startPosition);

            lock (_stateLock)
            {
                _startingCalled = true;
            }

            CancelPending();

            if (NotifyOnError(sender))
                return;

            if (!startPosition.HasValue)
            {
                if (IsBuffering)
                {
                    Debug.WriteLine("WinRtMediaStreamConfigurator.MediaStreamSourceOnStarting() deferring");

                    _onStartingRequest = args.Request;
                    _onStartingDeferral = args.Request.GetDeferral();

                    return;
                }

                try
                {
                    var pts = ResyncPresentationTimestamp();

                    if (!pts.HasValue)
                    {
                        Debug.WriteLine("WinRtMediaStreamConfigurator.MediaStreamSourceOnStarting() deferring after resync");

                        _onStartingRequest = args.Request;
                        _onStartingDeferral = args.Request.GetDeferral();

                        return;
                    }

                    Debug.WriteLine("WinRtMediaStreamConfigurator.MediaStreamSourceOnStarting() actual " + pts);

                    request.SetActualStartPosition(pts.Value);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("WinRtMediaStreamConfigurator.MediaStreamSourceOnStarting() failed: " + ex.Message);

                    sender.NotifyError(MediaStreamSourceErrorStatus.Other);
                }

                return;
            }

            MediaStreamSourceStartingRequestDeferral deferral = null;

            try
            {
                deferral = request.GetDeferral();

                var actual = await MediaManager.SeekMediaAsync(startPosition.Value).ConfigureAwait(false);

                Debug.WriteLine("WinRtMediaStreamConfigurator.MediaStreamSourceOnStarting() actual seek " + actual);

                request.SetActualStartPosition(actual);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Unable to seek: " + ex.Message);

                sender.NotifyError(MediaStreamSourceErrorStatus.FailedToOpenFile);
            }
            finally
            {
                if (null != deferral)
                    deferral.Complete();
            }
        }

        bool NotifyOnError(MediaStreamSource sender)
        {
            var error = false;

            if (!ReferenceEquals(sender, _mediaStreamSource))
            {
                Debug.WriteLine("WinRtMediaStreamConfigurator.FailOnError() unknown sender");
                sender.NotifyError(MediaStreamSourceErrorStatus.Other);

                error = true;
            }

            if (IsDisposed)
            {
                Debug.WriteLine("WinRtMediaStreamConfigurator.FailOnError() is disposed");
                sender.NotifyError(MediaStreamSourceErrorStatus.Other);

                error = true;
            }

            return error;
        }

        void CompleteOnStarting()
        {
            Debug.WriteLine("WinRtMediaStreamConfigurator.CompleteOnStarting()");

            if (null == _onStartingDeferral)
                return;

            if (IsBuffering)
                return;

            MediaStreamSourceStartingRequestDeferral deferral = null;

            try
            {
                var pts = ResyncPresentationTimestamp();

                if (!pts.HasValue)
                {
                    Debug.WriteLine("WinRtMediaStreamConfigurator.CompleteOnStarting() no timestamp after resync");

                    return;
                }

                deferral = Interlocked.Exchange(ref _onStartingDeferral, null);

                if (null == deferral)
                    return;

                Debug.WriteLine("WinRtMediaStreamConfigurator.CompleteOnStarting() pts " + pts);

                if (null != _onStartingRequest)
                {
                    _onStartingRequest.SetActualStartPosition(pts.Value);
                    _onStartingRequest = null;
                }
                else
                {
                    Debug.WriteLine("WinRtMediaStreamConfigurator.CompleteOnStarting() missing request");
                    throw new InvalidOperationException("WinRtMediaStreamSource.CompleteOnStarting() missing request");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("WinRtMediaStreamConfigurator.CompleteOnStarting() failed: " + ex.Message);

                _mediaStreamSource.NotifyError(MediaStreamSourceErrorStatus.FailedToOpenFile);
            }
            finally
            {
                if (null != deferral)
                    deferral.Complete();
            }
        }

        TimeSpan? ResyncPresentationTimestamp()
        {
            var earliest = TimeSpan.MaxValue;
            var latest = TimeSpan.MinValue;

            var count = 0;

            foreach (var state in _streamStates)
            {
                if (!state.Descriptor.IsSelected)
                    continue;

                ++count;

                var pts = state.PresentationTimestamp;

                if (!pts.HasValue)
                {
                    count = 0;

                    break;
                }

                if (pts.Value < earliest)
                    earliest = pts.Value;

                if (pts.Value > latest)
                    latest = pts.Value;
            }

            if (count > 1)
            {
                var difference = latest - earliest;

                if (difference >= ResyncThreshold)
                {
                    foreach (var state in _streamStates)
                    {
                        if (!state.Descriptor.IsSelected)
                            continue;

                        if (!state.PresentationTimestamp.HasValue)
                            continue;

                        var pts = state.PresentationTimestamp.Value;

                        if (latest - pts <= ResyncThreshold)
                            continue;

                        var discarded = state.DiscardPacketsBefore(latest);

                        if (discarded)
                            Debug.WriteLine("WinRtMediaStreamConfigurator.ResyncPresentationTimestamp() discarded '" + state.Name + "' samples before " + latest);
                    }
                }
            }

            return GetCurrentTimestamp();
        }

        /// <summary>
        ///     Return the earliest timestamp in all the active streams.  If a valid
        ///     stream does not have a timestamp, then return null.
        /// </summary>
        /// <returns></returns>
        TimeSpan? GetCurrentTimestamp()
        {
            TimeSpan? earliest = null;

            foreach (var state in _streamStates)
            {
                if (!state.Descriptor.IsSelected)
                    continue;

                var pts = state.PresentationTimestamp;

                if (!pts.HasValue)
                    return null;

                if (!earliest.HasValue || pts.Value < earliest.Value)
                    earliest = pts.Value;
            }

            return earliest;
        }

        public void Initialize()
        {
            Debug.WriteLine("WinRtMediaStreamConfigurator.Initialize()");

            ThrowIfDisposed();

            CancelMediaStreamCompletion();

            _mediaStreamCompletionSource = new TaskCompletionSource<IMediaSource>();

            TaskCompletionSource<object> playingCompletionSource;

            lock (_stateLock)
            {
                _startingCalled = false;

                playingCompletionSource = _playingCompletionSource;

                if (null != playingCompletionSource)
                    _playingCompletionSource = null;
            }

            if (null != playingCompletionSource && !playingCompletionSource.Task.IsCompleted)
            {
                Debug.WriteLine("WinRtMediaStreamConfigurator.Initialize() playing completion source already exists");

                playingCompletionSource.TrySetCanceled();
            }

            DisposePlayingCancellationRegistration();
        }

        public async Task<TMediaStreamSource> CreateMediaStreamSourceAsync<TMediaStreamSource>(CancellationToken cancellationToken)
            where TMediaStreamSource : class
        {
            Debug.WriteLine("WinRtMediaStreamConfigurator.CreateMediaStreamSourceAsync()");

            ThrowIfDisposed();

            var mscs = Volatile.Read(ref _mediaStreamCompletionSource);

            if (null == mscs)
                throw new InvalidOperationException("Null media stream completion source");

            cancellationToken.ThrowIfCancellationRequested();

            using (cancellationToken.Register(() => mscs.TrySetCanceled()))
            {
                var mss = await mscs.Task.ConfigureAwait(false);

                var ret = mss as TMediaStreamSource;

                if (null == ret)
                    throw new InvalidCastException(string.Format("Cannot convert {0} to {1}", mss.GetType().FullName, typeof(TMediaStreamSource).FullName));

                return ret;
            }
        }
    }
}

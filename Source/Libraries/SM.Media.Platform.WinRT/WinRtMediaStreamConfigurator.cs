// -----------------------------------------------------------------------
//  <copyright file="WinRtMediaStreamConfigurator.cs" company="Henric Jungheim">
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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using SM.Media.Configuration;
using SM.Media.MediaParser;
using SM.Media.Utility;

namespace SM.Media
{
    public sealed class WinRtMediaStreamConfigurator : IMediaStreamConfigurator
    {
        TaskCompletionSource<object> _closedTaskCompletionSource;

#if DEBUG
        MediaStreamFsm _mediaStreamFsm = new MediaStreamFsm();
#endif
        readonly object _stateLock = new object();
        readonly object _streamConfigurationLock = new object();
        int _isDisposed;
        TimeSpan? _seekTarget;
        TimeSpan? _duration;
        WinRtStreamState _videoStreamState;
        WinRtStreamState _audioStreamState;
        MediaStreamSourceStartingRequestDeferral _onStartingDeferral;
        MediaStreamSourceStartingRequest _onStartingRequest;
        static readonly TimeSpan ResyncThreshold = TimeSpan.FromSeconds(7);
        MediaStreamSource _mediaStreamSource;
        readonly TaskCompletionSource<IMediaSource> _mediaSourceCompletionSource = new TaskCompletionSource<IMediaSource>();

        bool IsDisposed
        {
            get { return 0 != _isDisposed; }
        }

        bool IsBuffering
        {
            get { return (null != _videoStreamState && _videoStreamState.IsBuffering) || (null != _audioStreamState && _audioStreamState.IsBuffering); }
        }

        public WinRtMediaStreamConfigurator()
        {
#if DEBUG
            _mediaStreamFsm.Reset();
#endif
        }

        #region IMediaStreamSource Members

        public IMediaManager MediaManager { get; set; }

        public TimeSpan? SeekTarget
        {
            get { lock (_stateLock) return _seekTarget; }
            set { lock (_stateLock) _seekTarget = value; }
        }

        /// <summary>
        ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (0 != Interlocked.Exchange(ref _isDisposed, 1))
                return;

            Debug.WriteLine("WinRtMediaStreamConfigurator.Dispose()");
            ValidateEvent(MediaStreamFsm.MediaEvent.DisposeCalled);

            if (null != _closedTaskCompletionSource)
            {
                _closedTaskCompletionSource.TrySetCanceled();
                _closedTaskCompletionSource = null;
            }
        }

        public void Configure(IMediaConfiguration configuration)
        {
            if (null != configuration.Audio)
            {
                var descriptor = CreateAudioDescriptor((IAudioConfigurationSource)configuration.Audio.ConfigurationSource);

                _audioStreamState = new WinRtStreamState("Audio", configuration.Audio.StreamSource, descriptor);
            }

            if (null != configuration.Video)
            {
                var descriptor = CreateVideoDescriptor((IVideoConfigurationSource)configuration.Video.ConfigurationSource);

                _videoStreamState = new WinRtStreamState("Video", configuration.Video.StreamSource, descriptor);
            }

            lock (_streamConfigurationLock)
            {
                _duration = configuration.Duration;

                CompleteConfigure();
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

            if (null != _videoStreamState)
                _videoStreamState.Close();

            if (null != _audioStreamState)
                _audioStreamState.Close();

            var tcs = _closedTaskCompletionSource;

            if (null == tcs)
                return TplTaskExtensions.CompletedTask;

            return tcs.Task;
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

            if (null != _videoStreamState)
                _videoStreamState.CheckForSamples();

            if (null != _audioStreamState)
                _audioStreamState.CheckForSamples();
        }

        #endregion

        void CancelPending()
        {
            Debug.WriteLine("WinRtMediaStreamConfigurator.CancelPending()");

            if (null != _videoStreamState)
                _videoStreamState.Cancel();

            if (null != _audioStreamState)
                _audioStreamState.Cancel();
        }

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
                (uint)configurationSource.Channels, (uint?)configurationSource.Bitrate ?? 0u);

            var descriptor = new AudioStreamDescriptor(audioEncodingProperties);

            if (string.IsNullOrEmpty(descriptor.Name))
                descriptor.Name = configurationSource.Name;

            DumpAudioStreamDescriptor(descriptor);

            return descriptor;
        }

        static void DumpAudioStreamDescriptor(AudioStreamDescriptor descriptor)
        {
            var p = descriptor.EncodingProperties;

            Debug.WriteLine("WinRtMediaStreamConfigurator.CreateAudioDescriptor() {0} sample rate {1} channels {2} bitrate {3}",
                p.Subtype, p.SampleRate, p.ChannelCount, p.Bitrate);
        }

        void CompleteConfigure()
        {
            Debug.WriteLine("WinRtMediaStreamConfigurator.CompleteConfigure()");

            var mss = CreateMediaStreamSource();

            _mediaStreamSource = mss;

            if (_mediaSourceCompletionSource.TrySetResult(mss))
                return;

            DeregisterCallbacks(mss);

            _mediaStreamSource = null;
        }

        MediaStreamSource CreateMediaStreamSource()
        {
            MediaStreamSource mediaStreamSource;

            if (null != _videoStreamState && null != _audioStreamState)
                mediaStreamSource = new MediaStreamSource(_videoStreamState.Descriptor, _audioStreamState.Descriptor);
            else if (null != _videoStreamState)
                mediaStreamSource = new MediaStreamSource(_videoStreamState.Descriptor);
            else if (null != _audioStreamState)
                mediaStreamSource = new MediaStreamSource(_audioStreamState.Descriptor);
            else
                throw new InvalidOperationException("No streams configured");

            mediaStreamSource.CanSeek = _duration.HasValue;

            if (_duration.HasValue)
                mediaStreamSource.Duration = _duration.Value;

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

            throw new NotSupportedException();
        }

        void MediaStreamSourceOnPaused(MediaStreamSource sender, object args)
        {
            Debug.WriteLine("WinRtMediaStreamConfigurator.MediaStreamSourceOnPaused()");

            NotifyOnError(sender);
        }

        void MediaStreamSourceOnClosed(MediaStreamSource sender, MediaStreamSourceClosedEventArgs args)
        {
            Debug.WriteLine("WinRtMediaStreamConfigurator.MediaStreamSourceOnClosed() reason: " + args.Request.Reason);

            DeregisterCallbacks(sender);

            var startDeferral = Interlocked.Exchange(ref _onStartingDeferral, null);

            if (null != startDeferral)
            {
                Debug.WriteLine("WinRtMediaStreamConfigurator.MediaStreamSourceOnClosed() start was still pending");
                startDeferral.Complete();
            }

            if (null == _closedTaskCompletionSource)
                Debug.WriteLine("WinRtMediaStreamConfigurator.MediaStreamSourceOnClosed() unexpected call to close");
            else
            {
                if (!_closedTaskCompletionSource.TrySetResult(string.Empty))
                    Debug.WriteLine("WinRtMediaStreamConfigurator.MediaStreamSourceOnClosed() close already completed");
            }

            var mediaManager = MediaManager;

            if (null == mediaManager)
            {
                Debug.WriteLine("MediaManager has not been initialized");
                return;
            }

            mediaManager.CloseMedia();
        }

        void MediaStreamSourceOnSampleRequested(MediaStreamSource sender, MediaStreamSourceSampleRequestedEventArgs args)
        {
            //Debug.WriteLine("WinRtMediaStreamConfigurator.MediaStreamSourceOnSampleRequested()");

            // We let "FailIfDisposed()" tell the MediaStreamSource if it
            // is time to quit, but we keep processing samples until the MediaStreamSource
            // tells us otherwise.
            NotifyOnError(sender);

            var request = args.Request;

            if (null != _videoStreamState && request.StreamDescriptor == _videoStreamState.Descriptor)
                _videoStreamState.SampleRequested(request);
            else if (null != _audioStreamState && request.StreamDescriptor == _audioStreamState.Descriptor)
                _audioStreamState.SampleRequested(request);
        }

        async void MediaStreamSourceOnStarting(MediaStreamSource sender, MediaStreamSourceStartingEventArgs args)
        {
            var request = args.Request;
            var startPosition = request.StartPosition;

            Debug.WriteLine("WinRtMediaStreamConfigurator.MediaStreamSourceOnStarting({0})", startPosition);

            if (NotifyOnError(sender))
                return;

            if (null != _closedTaskCompletionSource && _closedTaskCompletionSource.Task.IsCompleted)
            {
                _closedTaskCompletionSource.TrySetCanceled();
                _closedTaskCompletionSource = null;
            }

            if (null == _closedTaskCompletionSource)
                _closedTaskCompletionSource = new TaskCompletionSource<object>();

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
                    var pts = ResyncPresentationTimestamp() ?? TimeSpan.Zero;

                    Debug.WriteLine("WinRtMediaStreamConfigurator.MediaStreamSourceOnStarting() actual " + pts);

                    request.SetActualStartPosition(pts);

                    CheckForSamples();
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

                CheckForSamples();
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

            if (IsBuffering)
                return;

            var deferral = Interlocked.Exchange(ref _onStartingDeferral, null);

            if (null == deferral)
                return;

            try
            {
                var pts = ResyncPresentationTimestamp() ?? TimeSpan.Zero;

                Debug.WriteLine("WinRtMediaStreamConfigurator.CompleteOnStarting() pts " + pts);

                if (null != _onStartingRequest)
                {
                    _onStartingRequest.SetActualStartPosition(pts);
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
                deferral.Complete();

                _mediaStreamSource = null;
            }
        }

        TimeSpan? ResyncPresentationTimestamp()
        {
            TimeSpan? videoTimestamp = null;
            TimeSpan? audioTimestamp = null;

            if (null != _videoStreamState)
                videoTimestamp = _videoStreamState.PresentationTimestamp;

            if (null != _audioStreamState)
                audioTimestamp = _audioStreamState.PresentationTimestamp;

            if (videoTimestamp.HasValue && audioTimestamp.HasValue)
            {
                var difference = audioTimestamp.Value - videoTimestamp.Value;

                if (difference > ResyncThreshold)
                {
                    _videoStreamState.DiscardPacketsBefore(audioTimestamp.Value);
                    videoTimestamp = _videoStreamState.PresentationTimestamp;
                }
                else if (difference < -ResyncThreshold)
                {
                    _audioStreamState.DiscardPacketsBefore(videoTimestamp.Value);
                    audioTimestamp = _audioStreamState.PresentationTimestamp;
                }
            }

            if (audioTimestamp.HasValue)
            {
                if (!videoTimestamp.HasValue)
                    return audioTimestamp;

                if (audioTimestamp.Value < videoTimestamp.Value)
                    videoTimestamp = audioTimestamp.Value;
            }

            return videoTimestamp;
        }

        public async Task<TMediaStreamSource> CreateMediaStreamSourceAsync<TMediaStreamSource>(CancellationToken cancellationToken)
            where TMediaStreamSource : class
        {
            var mediaManager = MediaManager;

            if (null == mediaManager)
                throw new InvalidOperationException("MediaManger is null");

            mediaManager.OpenMedia();

            using (cancellationToken.Register(() => _mediaSourceCompletionSource.TrySetCanceled()))
            {
                var mss = await _mediaSourceCompletionSource.Task.ConfigureAwait(false);

                var ret = mss as TMediaStreamSource;

                if (null == ret)
                    throw new InvalidCastException(string.Format("Cannot convert {0} to {1}", mss.GetType().FullName, typeof(TMediaStreamSource).FullName));

                return ret;
            }
        }
    }
}

// -----------------------------------------------------------------------
//  <copyright file="WinRtMediaStreamSource.cs" company="Henric Jungheim">
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
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using SM.Media.Configuration;
using SM.Media.MediaParser;
using SM.Media.Utility;
using SM.TsParser;
using Buffer = Windows.Storage.Streams.Buffer;

namespace SM.Media
{
    public sealed class WinRtMediaStreamSource : IMediaStreamSource
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
        Action<IMediaSource> _mssHandler;

        StreamState _videoStreamState;
        StreamState _audioStreamState;
        MediaStreamSourceStartingRequestDeferral _onStartingDeferral;
        MediaStreamSourceStartingRequest _onStartingRequest;
        static readonly TimeSpan ResyncThreshold = TimeSpan.FromSeconds(7);
        private MediaStreamSource _onStartingSender;

        public WinRtMediaStreamSource()
        {
#if DEBUG
            _mediaStreamFsm.Reset();
#endif
        }

        bool IsDisposed
        {
            get { return 0 != _isDisposed; }
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

            Debug.WriteLine("WinRtMediaStreamSource.Dispose()");
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

                _audioStreamState = new StreamState("Audio", configuration.Audio.StreamSource, descriptor);
            }

            if (null != configuration.Video)
            {
                var descriptor = CreateVideoDescriptor((IVideoConfigurationSource)configuration.Video.ConfigurationSource);

                _videoStreamState = new StreamState("Video", configuration.Video.StreamSource, descriptor);
            }

            lock (_streamConfigurationLock)
            {
                _duration = configuration.Duration;

                CompleteConfigure();
            }
        }

        public void ReportError(string message)
        {
            Debug.WriteLine("WinRt.MediaStreamSource.ReportError(): " + message);
        }

        public Task CloseAsync()
        {
            Debug.WriteLine("WinRtMediaStreamSource.CloseAsync()");

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
            //Debug.WriteLine("WinRtMediaStreamSource.CheckForSamples()");

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
            Debug.WriteLine("WinRtMediaStreamSource.CancelPending()");

            if (null != _videoStreamState)
                _videoStreamState.Cancel();

            if (null != _audioStreamState)
                _audioStreamState.Cancel();
        }

        void ThrowIfDisposed()
        {
            if (0 == _isDisposed)
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

            Debug.WriteLine("WinRtMediaStreamSource.CreateAudioDescriptor() {0} sample rate {1} channels {2} bitrate {3}",
                p.Subtype, p.SampleRate, p.ChannelCount, p.Bitrate);
        }

        void CompleteConfigure()
        {
            Debug.WriteLine("WinRtMediaStreamSource.CompleteConfigure()");

            var mss = CreateMediaStreamSource();

            var mssHandler = _mssHandler;

            if (null != mssHandler)
            {
                _mssHandler = null;
                mssHandler(mss);
            }
            else
                Debug.WriteLine("WinRtMediaStreamSource.CompleteConfigure() no handler found");
        }

        public void RegisterMediaStreamSourceHandler(Action<IMediaSource> mssHandler)
        {
            Debug.WriteLine("WintRtMediaStreamSource.RegisterMediaStreamSourceHandler()");

            _mssHandler = mssHandler;

            if (null != MediaManager)
                MediaManager.OpenMedia();
        }

        MediaStreamSource CreateMediaStreamSource()
        {
            MediaStreamSource mediaStreamSource = null;

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

            mediaStreamSource.Starting += MediaStreamSourceOnStarting;
            mediaStreamSource.SampleRequested += MediaStreamSourceOnSampleRequested;
            mediaStreamSource.Closed += MediaStreamSourceOnClosed;

            mediaStreamSource.Paused += MediaStreamSourceOnPaused;
            mediaStreamSource.SwitchStreamsRequested += MediaStreamSourceOnSwitchStreamsRequested;

            return mediaStreamSource;
        }

        void MediaStreamSourceOnSwitchStreamsRequested(MediaStreamSource sender, MediaStreamSourceSwitchStreamsRequestedEventArgs args)
        {
            Debug.WriteLine("WinRtMediaStreamSource.MediaStreamSourceOnSwitchStreamsRequested()");

            ThrowIfDisposed();

            throw new NotSupportedException();
        }

        void MediaStreamSourceOnPaused(MediaStreamSource sender, object args)
        {
            Debug.WriteLine("WinRtMediaStreamSource.MediaStreamSourceOnPaused()");

            ThrowIfDisposed();
        }

        void MediaStreamSourceOnClosed(MediaStreamSource sender, MediaStreamSourceClosedEventArgs args)
        {
            Debug.WriteLine("WinRtMediaStreamSource.MediaStreamSourceOnClosed() reason: " + args.Request.Reason);

            ThrowIfDisposed();

            sender.Starting -= MediaStreamSourceOnStarting;
            sender.SampleRequested -= MediaStreamSourceOnSampleRequested;
            sender.Closed -= MediaStreamSourceOnClosed;

            var startDeferral = Interlocked.Exchange(ref _onStartingDeferral, null);

            if (null != startDeferral)
            {
                Debug.WriteLine("WinRtMediaStreamSource.MediaStreamSourceOnClosed() start was still pending");
                startDeferral.Complete();
            }

            if (null == _closedTaskCompletionSource)
                Debug.WriteLine("WinRtMediaStreamSource.MediaStreamSourceOnClosed() unexpected call to close");
            else
            {
                if (!_closedTaskCompletionSource.TrySetResult(string.Empty))
                    Debug.WriteLine("WinRtMediaStreamSource.MediaStreamSourceOnClosed() close already completed");
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
            //Debug.WriteLine("WinRtMediaStreamSource.MediaStreamSourceOnSampleRequested()");

            ThrowIfDisposed();

            var request = args.Request;

            if (null != _videoStreamState && request.StreamDescriptor == _videoStreamState.Descriptor)
                _videoStreamState.SampleRequested(request);
            else if (null != _audioStreamState && request.StreamDescriptor == _audioStreamState.Descriptor)
                _audioStreamState.SampleRequested(request);
        }

        async void MediaStreamSourceOnStarting(MediaStreamSource sender, MediaStreamSourceStartingEventArgs args)
        {
            ThrowIfDisposed();

            if (null != _closedTaskCompletionSource && _closedTaskCompletionSource.Task.IsCompleted)
            {
                _closedTaskCompletionSource.TrySetCanceled();
                _closedTaskCompletionSource = null;
            }

            if (null == _closedTaskCompletionSource)
                _closedTaskCompletionSource = new TaskCompletionSource<object>();

            var request = args.Request;
            var startPosition = request.StartPosition;

            Debug.WriteLine("WinRtMediaStreamSource.MediaStreamSourceOnStarting({0})", startPosition);

            if (!startPosition.HasValue)
            {
                if (IsBuffering)
                {
                    Debug.WriteLine("WinRtMediaStreamSource.MediaStreamSourceOnStarting() deferring");

                    _onStartingRequest = args.Request;
                    _onStartingDeferral = args.Request.GetDeferral();
                    _onStartingSender = sender;

                    return;
                }

                try
                {
                    var pts = ResyncPresentationTimestamp() ?? TimeSpan.Zero;

                    Debug.WriteLine("WinRtMediaStreamSource.MediaStreamSourceOnStarting() actual " + pts);

                    request.SetActualStartPosition(pts);

                    CheckForSamples();

                }
                catch (Exception ex)
                {
                    Debug.WriteLine("WinRtMediaStreamSource.MediaStreamSourceOnStarting() failed: " + ex.Message);

                    sender.NotifyError(MediaStreamSourceErrorStatus.Other);
                }

                return;
            }

            MediaStreamSourceStartingRequestDeferral deferral = null;

            try
            {
                deferral = request.GetDeferral();

                var actual = await MediaManager.SeekMediaAsync(startPosition.Value).ConfigureAwait(false);

                Debug.WriteLine("WinRtMediaStreamSource.MediaStreamSourceOnStarting() actual seek " + actual);

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

        bool IsBuffering
        {
            get { return (null != _videoStreamState && _videoStreamState.IsBuffering) || (null != _audioStreamState && _audioStreamState.IsBuffering); }
        }

        void CompleteOnStarting()
        {
            Debug.WriteLine("WinRtMediaStreamSource.CompleteOnStarting()");

            if (IsBuffering)
                return;

            var deferral = Interlocked.Exchange(ref _onStartingDeferral, null);

            if (null == deferral)
                return;

            try
            {
                var pts = ResyncPresentationTimestamp() ?? TimeSpan.Zero;

                Debug.WriteLine("WinRtMediaStreamSource.CompleteOnStarting() pts " + pts);

                if (null != _onStartingRequest)
                {
                    _onStartingRequest.SetActualStartPosition(pts);
                    _onStartingRequest = null;
                }
                else
                {
                    Debug.WriteLine("WinRtMediaStreamSource.CompleteOnStarting() missing request");
                    throw new InvalidOperationException("WinRtMediaStreamSource.CompleteOnStarting() missing request");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("WinRtMediaStreamSource.CompleteOnStarting() failed: " + ex.Message);

                _onStartingSender.NotifyError(MediaStreamSourceErrorStatus.FailedToOpenFile);
            }
            finally
            {
                deferral.Complete();

                _onStartingSender = null;
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

        class StreamState
        {
            public readonly IMediaStreamDescriptor Descriptor;
            readonly string _name;
            readonly object _sampleLock = new object();

            readonly IStreamSource _streamSource;
            uint _bufferingProgress;
            MediaStreamSourceSampleRequestDeferral _deferral;
            bool _isClosed;
            uint _reportedBufferingProgress;
            MediaStreamSourceSampleRequest _request;

            public StreamState(string name, IStreamSource streamSource, IMediaStreamDescriptor descriptor)
            {
                if (name == null)
                    throw new ArgumentNullException("name");
                if (streamSource == null)
                    throw new ArgumentNullException("streamSource");
                if (descriptor == null)
                    throw new ArgumentNullException("descriptor");

                _name = name;
                _streamSource = streamSource;
                Descriptor = descriptor;
            }

            public bool IsBuffering
            {
                get { return _streamSource.BufferingProgress.HasValue; }
            }

            public TimeSpan? PresentationTimestamp
            {
                get { return _streamSource.PresentationTimestamp; }
            }

            public void CheckForSamples()
            {
                MediaStreamSourceSampleRequestDeferral deferral = null;
                MediaStreamSourceSampleRequest request = null;

                try
                {
                    lock (_sampleLock)
                    {
                        deferral = _deferral;

                        if (null == deferral)
                            return;

                        _deferral = null;

                        request = _request;

                        if (null != request)
                            _request = null;

                        if (_isClosed)
                            return;
                    }

                    if (!TryCompleteRequest(request))
                        return;

                    var localDeferral = deferral;

                    request = null;
                    deferral = null;

                    localDeferral.Complete();
                }
                finally
                {
                    if (null != deferral || null != request)
                    {
                        lock (_sampleLock)
                        {
                            SmDebug.Assert(null == _deferral);
                            SmDebug.Assert(null == _request);

                            if (!_isClosed)
                            {
                                _deferral = deferral;
                                _request = request;

                                deferral = null;
                            }
                        }

                        if (null != deferral)
                            deferral.Complete();
                    }
                }
            }

            bool TryCompleteRequest(MediaStreamSourceSampleRequest request)
            {
                if (_isClosed)
                    return true;

                TsPesPacket packet = null;

                try
                {
                    packet = _streamSource.GetNextSample();

                    if (null == packet)
                    {
                        if (_streamSource.IsEof)
                        {
                            //Debug.WriteLine("Sample {0} eof", _name);
                            return true;
                        }

                        if (_streamSource.BufferingProgress.HasValue)
                            _bufferingProgress = (uint)(Math.Round(100 * _streamSource.BufferingProgress.Value));
                        else
                            _bufferingProgress = 0;

                        if (_bufferingProgress != _reportedBufferingProgress)
                        {
                            Debug.WriteLine("Sample {0} buffering {1}%", _name, _bufferingProgress);

                            request.ReportSampleProgress(_bufferingProgress);
                            _reportedBufferingProgress = _bufferingProgress;
                        }

                        return false;
                    }

                    _bufferingProgress = _reportedBufferingProgress = 100;

                    var presentationTimestamp = packet.PresentationTimestamp;

#if WORKING_PROCESSED_EVENT
                    var packetBuffer = packet.Buffer.AsBuffer(packet.Index, packet.Length);
#else
                    // Make a copy of the buffer since Sample.Processed doesn't always seem to
                    // get called.
                    var packetBuffer = new Buffer((uint)packet.Length)
                    {
                        Length = (uint)packet.Length
                    };

                    packet.Buffer.CopyTo(packet.Index, packetBuffer, 0, packet.Length);
#endif

                    var mediaStreamSample = MediaStreamSample.CreateFromBuffer(packetBuffer, presentationTimestamp);

                    if (null == mediaStreamSample)
                        throw new InvalidOperationException("MediaStreamSamples cannot be null");

                    if (packet.DecodeTimestamp.HasValue)
                        mediaStreamSample.DecodeTimestamp = packet.DecodeTimestamp.Value;

                    if (packet.Duration.HasValue)
                        mediaStreamSample.Duration = packet.Duration.Value;

                    //Debug.WriteLine("Sample {0} at {1}. duration {2} length {3}",
                    //    _name, mediaStreamSample.Timestamp, mediaStreamSample.Duration, packet.Length);

                    request.Sample = mediaStreamSample;

#if WORKING_PROCESSED_EVENT
                    var localPacket = packet;

                    request.Sample.Processed += (sender, args) => _streamSource.FreeSample(localPacket);

                    // Prevent the .FreeSample() below from freeing this packet.
                    packet = null;
#endif

                    return true;
                }
                finally
                {
                    if (null != packet)
                        _streamSource.FreeSample(packet);
                }
            }

            public void SampleRequested(MediaStreamSourceSampleRequest request)
            {
                //Debug.WriteLine("StreamState.SampleRequested() " + _name);

                SmDebug.Assert(null == _deferral);
                SmDebug.Assert(null == _request);

                if (TryCompleteRequest(request))
                    return;

                var deferral = request.GetDeferral();

                lock (_sampleLock)
                {
                    SmDebug.Assert(null == _deferral);
                    SmDebug.Assert(null == _request);

                    if (!_isClosed)
                    {
                        _request = request;
                        _deferral = deferral;

                        deferral = null;
                    }
                }

                if (null != deferral)
                    deferral.Complete();
            }

            public void Cancel()
            {
                MediaStreamSourceSampleRequestDeferral deferral;

                lock (_sampleLock)
                {
                    deferral = _deferral;

                    if (null == deferral)
                        return;

                    _deferral = null;
                    _request = null;
                }

                if (null == deferral)
                    return;

                deferral.Complete();
            }

            public void Close()
            {
                lock (_sampleLock)
                    _isClosed = true;

                Cancel();
            }

            public void DiscardPacketsBefore(TimeSpan value)
            {
                _streamSource.DiscardPacketsBefore(value);
            }
        }
    }
}

// -----------------------------------------------------------------------
//  <copyright file="WinRtMediaStreamSource.cs" company="Henric Jungheim">
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
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using SM.Media.Configuration;
using SM.Media.Utility;
using SM.TsParser;
using Debug = System.Diagnostics.Debug;

namespace SM.Media
{
    public sealed class WinRtMediaStreamSource : IMediaStreamSource
    {
        readonly AsyncManualResetEvent _drainCompleted = new AsyncManualResetEvent(true);
#if DEBUG
        MediaStreamFsm _mediaStreamFsm = new MediaStreamFsm();
#endif
        readonly object _stateLock = new object();
        readonly object _streamConfigurationLock = new object();

        bool _isClosed;
        int _isDisposed;
        TimeSpan? _seekTarget;
        TimeSpan? _duration;
        Action<MediaStreamSource> _mssHandler;

        StreamState _videoStreamState;
        StreamState _audioStreamState;

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

            _isClosed = true;
        }

        public void Configure(MediaConfiguration configuration)
        {
            if (null != configuration.AudioConfiguration)
                _audioStreamState = new StreamState("Audio", configuration.AudioStream, CreateAudioDescriptor(configuration.AudioConfiguration));

            if (null != configuration.VideoConfiguration)
                _videoStreamState = new StreamState("Video", configuration.VideoStream, CreateVideoDescriptor(configuration.VideoConfiguration));

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
            lock (_stateLock)
            {
                _isClosed = true;
            }

            return _drainCompleted.WaitAsync();
        }

        public void ValidateEvent(MediaStreamFsm.MediaEvent mediaEvent)
        {
#if DEBUG
            _mediaStreamFsm.ValidateEvent(mediaEvent);
#endif
        }

        public void CheckForSamples()
        {
            //System.Diagnostics.Debug.WriteLine("WinRtMediaStreamSource.CheckForSamples()");

            if (null != _videoStreamState)
                _videoStreamState.CheckForSamples();

            if (null != _audioStreamState)
                _audioStreamState.CheckForSamples();
        }

        #endregion

        public void CancelPending()
        {
            //System.Diagnostics.Debug.WriteLine("WinRtMediaStreamSource.CancelPending()");

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
                    break;
                case "MP2V":
                    return VideoEncodingProperties.CreateMpeg2();
                    break;
                default:
                    break;
            }

            return null;
        }

        IMediaStreamDescriptor CreateVideoDescriptor(IVideoConfigurationSource configurationSource)
        {
            var encodingProperties = GetVideoEncodingProperties(configurationSource);

            if (null == encodingProperties)
                throw new ArgumentOutOfRangeException();

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

                    return new AudioStreamDescriptor(encodingProperties);
                case AudioFormat.Unknown:
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var audioEncodingProperties = propertyFactory((uint)configurationSource.SamplingFrequency,
                (uint)configurationSource.Channels, (uint?)configurationSource.Bitrate ?? 0u);

            audioEncodingProperties.BitsPerSample = 16;

            var descriptor = new AudioStreamDescriptor(audioEncodingProperties);

            if (string.IsNullOrEmpty(descriptor.Name))
                descriptor.Name = configurationSource.Name;

            return descriptor;
        }

        void CompleteConfigure()
        {
            var mss = CreateMediaStreamSource();

            var mssHandler = _mssHandler;

            if (null != mssHandler)
            {
                _mssHandler = null;
                mssHandler(mss);
            }
        }

        public Task WaitDrain()
        {
            return _drainCompleted.WaitAsync();
        }

        public void RegisterMediaStreamSourceHandler(Action<MediaStreamSource> mssHandler)
        {
            _mssHandler = mssHandler;
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

            return mediaStreamSource;
        }

        void MediaStreamSourceOnSampleRequested(MediaStreamSource sender, MediaStreamSourceSampleRequestedEventArgs args)
        {
            //Debug.WriteLine("WinRtMediaStreamSource.MediaStreamSourceOnSampleRequested()");

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

            Debug.WriteLine("WinRtMediaStreamSource.MediaStreamSourceOnStarting({0})", startPosition);

            if (!startPosition.HasValue)
                return;

            MediaStreamSourceStartingRequestDeferral deferral = null;

            try
            {
                CancelPending();

                deferral = request.GetDeferral();

                var actual = await MediaManager.SeekMediaAsync(startPosition.Value).ConfigureAwait(false);

                request.SetActualStartPosition(actual);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Unable to seek: " + ex.Message);
            }
            finally
            {
                if (null != deferral)
                    deferral.Complete();
            }
        }

        class StreamState
        {
            public readonly IMediaStreamDescriptor Descriptor;
            readonly string _name;

            readonly IStreamSource _streamSource;
            uint _bufferingProgress;
            MediaStreamSourceSampleRequestDeferral _deferral;
            uint _reportedBufferingProgress;
            MediaStreamSourceSampleRequest _request;
            SpinLock _sampleLock = new SpinLock();

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

            public void CheckForSamples()
            {
                MediaStreamSourceSampleRequestDeferral deferral = null;
                MediaStreamSourceSampleRequest request = null;

                try
                {
                    var lockTaken = false;

                    try
                    {
                        _sampleLock.Enter(ref lockTaken);

                        deferral = _deferral;

                        if (null == deferral)
                            return;

                        _deferral = null;

                        request = _request;

                        if (null != request)
                            _request = null;
                    }
                    finally
                    {
                        if (lockTaken)
                            _sampleLock.Exit();
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
                        var lockTaken = false;

                        try
                        {
                            _sampleLock.Enter(ref lockTaken);

                            Utility.Debug.Assert(null == _deferral);
                            Utility.Debug.Assert(null == _request);

                            _deferral = deferral;
                            _request = request;
                        }
                        finally
                        {
                            if (lockTaken)
                                _sampleLock.Exit();
                        }
                    }
                }
            }

            bool TryCompleteRequest(MediaStreamSourceSampleRequest request)
            {
                TsPesPacket packet = null;

                try
                {
                    packet = _streamSource.GetNextSample();

                    if (null == packet)
                    {
                        if (_streamSource.IsEof)
                            return true;

                        if (_streamSource.BufferingProgress.HasValue)
                            _bufferingProgress = (uint)(Math.Round(100 * _streamSource.BufferingProgress.Value));
                        else
                            _bufferingProgress = 0;

                        if (_bufferingProgress != _reportedBufferingProgress)
                        {
                            //Debug.WriteLine("Sample {0} buffering {1}%", _name, _bufferingProgress);

                            request.ReportSampleProgress(_bufferingProgress);
                            _reportedBufferingProgress = _bufferingProgress;
                        }

                        return false;
                    }

                    _bufferingProgress = _reportedBufferingProgress = 100;

                    var presentationTimestamp = _streamSource.PresentationTimestamp;

                    var packetBuffer = packet.Buffer.AsBuffer(packet.Index, packet.Length);

                    var mediaStreamSample = MediaStreamSample.CreateFromBuffer(packetBuffer, presentationTimestamp);

                    if (null == mediaStreamSample)
                        throw new InvalidOperationException("MediaStreamSamples cannot be null");

                    if (_streamSource.DecodeTimestamp.HasValue)
                        mediaStreamSample.DecodeTimestamp = _streamSource.DecodeTimestamp.Value;

                    if (packet.Duration.HasValue)
                        mediaStreamSample.Duration = packet.Duration.Value;

                    //Debug.WriteLine("Sample {0} at {1}. duration {2} length {3}",
                    //    _name, mediaStreamSample.Timestamp, mediaStreamSample.Duration, packet.Length);

                    request.Sample = mediaStreamSample;

                    var localPacket = packet;

                    request.Sample.Processed += (sender, args) => _streamSource.FreeSample(localPacket);

                    // Prevent the .FreeSample() below from freeing this packet.
                    packet = null;

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
                Utility.Debug.Assert(null == _deferral);
                Utility.Debug.Assert(null == _request);

                if (TryCompleteRequest(request))
                    return;

                var deferral = request.GetDeferral();

                var lockTaken = false;

                try
                {
                    _sampleLock.Enter(ref lockTaken);

                    Utility.Debug.Assert(null == _deferral);
                    Utility.Debug.Assert(null == _request);

                    _request = request;
                    _deferral = deferral;
                }
                finally
                {
                    if (lockTaken)
                        _sampleLock.Exit();
                }
            }

            public void Cancel()
            {
                MediaStreamSourceSampleRequestDeferral deferral;

                var lockTaken = false;

                try
                {
                    _sampleLock.Enter(ref lockTaken);

                    deferral = _deferral;

                    if (null == deferral)
                        return;

                    _deferral = null;
                    _request = null;
                }
                finally
                {
                    if (lockTaken)
                        _sampleLock.Exit();
                }

                if (null == deferral)
                    return;

                deferral.Complete();
            }
        }
    }
}

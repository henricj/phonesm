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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.UI.Core;
using SM.Media.Configuration;
using SM.Media.Utility;

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

        IMediaStreamDescriptor _audioStreamDescriptor;
        IStreamSource _audioStreamSource;
        uint? _videoBufferingProgress;
        uint? _audioBufferingProgress;
        bool _isClosed;
        int _isDisposed;
        TimeSpan? _seekTarget;
        IMediaStreamDescriptor _videoStreamDescriptor;
        IStreamSource _videoStreamSource;
        TimeSpan? _duration;
        Action<MediaStreamSource> _mssHandler;
        MediaStreamSourceSampleRequestDeferral _videoDeferral;
        MediaStreamSourceSampleRequestDeferral _audioDeferral;
        MediaStreamSourceSampleRequest _videoRequest;
        MediaStreamSourceSampleRequest _audioRequest;
        SpinLock _sampleLock = new SpinLock();
        uint? _videoReportedBufferingProgress;
        uint? _audioReportedBufferingProgress;

        public WinRtMediaStreamSource()
        {
            //AudioBufferLength = 150;     // 150ms of internal buffering, instead of 1s.

#if DEBUG
            _mediaStreamFsm.Reset();
#endif

            //_taskScheduler = new SingleThreadSignalTaskScheduler("WinRtMediaStreamSource", SignalHandler);
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
                ConfigureAudioStream(configuration.AudioConfiguration, configuration.AudioStream);

            if (null != configuration.VideoConfiguration)
                ConfigureVideoStream(configuration.VideoConfiguration, configuration.VideoStream);

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
            //Debug.WriteLine("WinRtMediaStreamSource.CheckForSamples()");

            MediaStreamSourceSampleRequestDeferral videoDeferral;
            MediaStreamSourceSampleRequest videoRequest;

            MediaStreamSourceSampleRequestDeferral audioDeferral;
            MediaStreamSourceSampleRequest audioRequest;

            var dispatches = new List<DispatchedHandler>();

            var lockTaken = false;

            try
            {
                _sampleLock.Enter(ref lockTaken);

                videoDeferral = _videoDeferral;
                _videoDeferral = null;

                videoRequest = _videoRequest;
                _videoRequest = null;

                audioDeferral = _audioDeferral;
                _audioDeferral = null;

                audioRequest = _audioRequest;
                _audioRequest = null;
            }
            finally
            {
                if (lockTaken)
                    _sampleLock.Exit();
            }

            try
            {
                if (null != videoDeferral)
                {
                    var sample = GetSample(_videoStreamSource, ref _videoBufferingProgress);

                    if (null != sample)
                    {
                        var localRequest = videoRequest;
                        var localDeferral = videoDeferral;

                        dispatches.Add(() =>
                                       {
                                           localRequest.Sample = sample;
                                           localDeferral.Complete();
                                       });

                        videoRequest = null;
                        videoDeferral = null;
                    }
                    else
                    {
                        if (_videoReportedBufferingProgress != _videoBufferingProgress)
                        {
                            var progress = _videoBufferingProgress ?? 0;

                            var localRequest = videoRequest;

                            dispatches.Add(() => localRequest.ReportSampleProgress(progress));

                            _videoReportedBufferingProgress = _videoBufferingProgress;
                        }
                    }
                }

                if (null != audioDeferral)
                {
                    var sample = GetSample(_audioStreamSource, ref _audioBufferingProgress);

                    if (null != sample)
                    {
                        var localRequest = audioRequest;
                        var localDeferral = audioDeferral;

                        dispatches.Add(() =>
                                       {
                                           localRequest.Sample = sample;
                                           localDeferral.Complete();
                                       });

                        audioRequest = null;
                        audioDeferral = null;
                    }
                    else
                    {
                        if (_audioReportedBufferingProgress != _audioBufferingProgress)
                        {
                            var progress = _audioBufferingProgress ?? 0;
                            var localRequest = audioRequest;

                            dispatches.Add(() => localRequest.ReportSampleProgress(progress));

                            _audioReportedBufferingProgress = _audioBufferingProgress;
                        }
                    }
                }
            }
            finally
            {
                if (null != videoDeferral || null != audioDeferral)
                {
                    lockTaken = false;

                    try
                    {
                        _sampleLock.Enter(ref lockTaken);

                        if (null != videoDeferral)
                        {
                            _videoDeferral = videoDeferral;
                            _videoRequest = videoRequest;
                        }

                        if (null != audioDeferral)
                        {
                            _audioDeferral = audioDeferral;
                            _audioRequest = audioRequest;
                        }
                    }
                    finally
                    {
                        if (lockTaken)
                            _sampleLock.Exit();
                    }
                }

                // We make sure this work happens *after* the spinlock protected members have been updated.
                if (dispatches.Count > 0)
                {
                    foreach (var dispatch in dispatches)
                        dispatch();
                }
            }
        }

        #endregion

        void ThrowIfDisposed()
        {
            if (0 == _isDisposed)
                return;

            throw new ObjectDisposedException(GetType().Name);
        }

        IMediaStreamDescriptor CreateVideoDescriptor(IVideoConfigurationSource configurationSource)
        {
            if ("H264" != configurationSource.VideoFourCc)
                throw new ArgumentOutOfRangeException();

            var encodingProperties = VideoEncodingProperties.CreateH264();

            return new VideoStreamDescriptor(encodingProperties);
        }

        void ConfigureVideoStream(IVideoConfigurationSource configurationSource, IStreamSource videoSource)
        {
            var descriptor = CreateVideoDescriptor(configurationSource);

            lock (_streamConfigurationLock)
            {
                _videoStreamSource = videoSource;
                _videoStreamDescriptor = descriptor;
            }
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
                case AudioFormat.Unknown:
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var audioEncodingProperties = propertyFactory((uint)configurationSource.SamplingFrequency,
                (uint)configurationSource.Channels, (uint?)configurationSource.Bitrate ?? 0u);

            return new AudioStreamDescriptor(audioEncodingProperties);
        }

        void ConfigureAudioStream(IAudioConfigurationSource configurationSource, IStreamSource audioSource)
        {
            var audioStreamDescriptor = CreateAudioDescriptor(configurationSource);

            lock (_streamConfigurationLock)
            {
                _audioStreamSource = audioSource;
                _audioStreamDescriptor = audioStreamDescriptor;
            }
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

            if (null != _videoStreamDescriptor && null != _audioStreamDescriptor)
                mediaStreamSource = new MediaStreamSource(_videoStreamDescriptor, _audioStreamDescriptor);

            else if (null != _videoStreamDescriptor)
                mediaStreamSource = new MediaStreamSource(_videoStreamDescriptor);

            else if (null != _audioStreamDescriptor)
                mediaStreamSource = new MediaStreamSource(_audioStreamDescriptor);
            else
                throw new InvalidOperationException("No streams configured");

            mediaStreamSource.CanSeek = _duration.HasValue;

            if (_duration.HasValue)
                mediaStreamSource.Duration = _duration.Value;

            mediaStreamSource.Starting += MediaStreamSourceOnStarting;
            mediaStreamSource.SampleRequested += MediaStreamSourceOnSampleRequested;
            return mediaStreamSource;
        }

        MediaStreamSample GetSample(IStreamSource source, ref uint? bufferingProgress)
        {
            MediaStreamSample mss = null;
            double? localBufferingProgress = null;

            if (source.GetNextSample(sample =>
                                     {
                                         localBufferingProgress = sample.BufferingProgress;

                                         if (!localBufferingProgress.HasValue)
                                             // There is no point in doing an await since the source stream is in memory.
                                             mss = MediaStreamSample.CreateFromStreamAsync(sample.Stream.AsInputStream(), (uint)sample.Stream.Length, sample.Timestamp).AsTask().Result;

                                         return true;
                                     }))
            {
                bufferingProgress = null;
                return mss;
            }

            if (localBufferingProgress.HasValue)
                bufferingProgress = (uint)Math.Round(100 * localBufferingProgress.Value);

            return null;
        }

        void MediaStreamSourceOnSampleRequested(MediaStreamSource sender, MediaStreamSourceSampleRequestedEventArgs args)
        {
            //Debug.WriteLine("WinRtMediaStreamSource.MediaStreamSourceOnSampleRequested()");

            var request = args.Request;

            if (request.StreamDescriptor == _videoStreamDescriptor)
            {
                var mss = GetSample(_videoStreamSource, ref _videoBufferingProgress);

                if (null != mss)
                {
                    request.Sample = mss;
                    return;
                }

                request.ReportSampleProgress(_videoBufferingProgress ?? 0);

                _videoReportedBufferingProgress = _videoBufferingProgress ?? 0;

                var deferral = request.GetDeferral();

                var lockTaken = false;

                try
                {
                    _sampleLock.Enter(ref lockTaken);

                    Debug.Assert(null == _videoDeferral);

                    _videoDeferral = deferral;
                    _videoRequest = request;
                }
                finally
                {
                    if (lockTaken)
                        _sampleLock.Exit();
                }
            }
            else if (request.StreamDescriptor == _audioStreamDescriptor)
            {
                var mss = GetSample(_audioStreamSource, ref _audioBufferingProgress);

                if (null != mss)
                {
                    request.Sample = mss;
                    return;
                }

                request.ReportSampleProgress(_audioBufferingProgress ?? 0);

                _audioReportedBufferingProgress = _audioBufferingProgress ?? 0;

                var deferral = request.GetDeferral();

                var lockTaken = false;

                try
                {
                    _sampleLock.Enter(ref lockTaken);

                    Debug.Assert(null == _audioDeferral);

                    _audioDeferral = deferral;
                    _audioRequest = request;
                }
                finally
                {
                    if (lockTaken)
                        _sampleLock.Exit();
                }
            }
        }

        void MediaStreamSourceOnStarting(MediaStreamSource sender, MediaStreamSourceStartingEventArgs args)
        {
            Debug.WriteLine("WinRtMediaStreamSource.MediaStreamSourceOnStarting()");

            args.Request.SetActualStartPosition(TimeSpan.Zero);
        }
    }
}

// -----------------------------------------------------------------------
//  <copyright file="TsMediaStreamSource.cs" company="Henric Jungheim">
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
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using SM.Media.Configuration;
using SM.Media.Utility;

namespace SM.Media
{
    public sealed class TsMediaStreamSource : MediaStreamSource, IMediaStreamSource
    {
        static readonly Dictionary<MediaSampleAttributeKeys, string> NoMediaSampleAttributes = new Dictionary<MediaSampleAttributeKeys, string>();
        readonly AsyncManualResetEvent _drainCompleted = new AsyncManualResetEvent(true);
        readonly object _stateLock = new object();
        readonly object _streamConfigurationLock = new object();
        readonly SingleThreadSignalTaskScheduler _taskScheduler;

        MediaStreamDescription _audioStreamDescription;
        IStreamSource _audioStreamSource;
        double _bufferingProgress;
        bool _isClosed;
        int _isDisposed;
        MediaStreamFsm _mediaStreamFsm = new MediaStreamFsm();
        volatile int _pendingOperations;
        TimeSpan _pendingSeekTarget;
        TimeSpan? _seekTarget;
        SourceState _state;
        int _streamClosedFlags;
        int _streamOpenFlags;
        MediaStreamDescription _videoStreamDescription;
        IStreamSource _videoStreamSource;

        public TsMediaStreamSource()
        {
            //AudioBufferLength = 150;     // 150ms of internal buffering, instead of 1s.

            _mediaStreamFsm.Reset();

            _taskScheduler = new SingleThreadSignalTaskScheduler("TsMediaStreamSource", SignalHandler);
        }

        bool IsDisposed
        {
            get { return 0 != _isDisposed; }
        }

        SourceState State
        {
            get { lock (_stateLock) return _state; }
            set
            {
                lock (_stateLock)
                {
                    if (_state == value)
                        return;

                    _state = value;
                }

                CheckPending();
            }
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

            Debug.WriteLine("TsMediaStreamSource.Dispose()");
            ValidateEvent(MediaStreamFsm.MediaEvent.DisposeCalled);

            _isClosed = true;

            if (null != _taskScheduler)
                _taskScheduler.Dispose();
        }

        public void Configure(MediaConfiguration configuration)
        {
            if (null != configuration.AudioConfiguration)
                ConfigureAudioStream(configuration.AudioConfiguration, configuration.AudioStream);

            if (null != configuration.VideoConfiguration)
                ConfigureVideoStream(configuration.VideoConfiguration, configuration.VideoStream);

            lock (_streamConfigurationLock)
            {
                CompleteConfigure(configuration.Duration);
            }
        }

        public void ReportError(string message)
        {
            Task.Factory.StartNew(() => ErrorOccurred(message), CancellationToken.None, TaskCreationOptions.None, _taskScheduler);
        }

        public Task CloseAsync()
        {
            if (0 == _streamOpenFlags)
                return TplTaskExtensions.CompletedTask;

            lock (_stateLock)
            {
                _isClosed = true;

                _state = SourceState.WaitForClose;
            }

            return _drainCompleted.WaitAsync();
        }

        public void CheckForSamples()
        {
            if (0 == (_pendingOperations & (int)(Operation.Audio | Operation.Video)))
                return;

            _taskScheduler.Signal();
        }

        public void CheckPending()
        {
            if (0 == _pendingOperations)
                return;

            _taskScheduler.Signal();
        }

        public void ValidateEvent(MediaStreamFsm.MediaEvent mediaEvent)
        {
#if DEBUG
            _mediaStreamFsm.ValidateEvent(mediaEvent);
#endif
        }

        #endregion

        async Task SeekHandler()
        {
            TimeSpan seekTimestamp;

            _taskScheduler.ThrowIfNotOnThread();

            var mediaManager = MediaManager;

            if (null == mediaManager)
                throw new InvalidOperationException("MediaManager has not been initialized");

            lock (_stateLock)
            {
                seekTimestamp = _pendingSeekTarget;
            }

            try
            {
                var position = await mediaManager.SeekMediaAsync(seekTimestamp);

                _taskScheduler.ThrowIfNotOnThread();

                if (_isClosed)
                    return;

                ValidateEvent(MediaStreamFsm.MediaEvent.CallingReportSeekCompleted);
                ReportSeekCompleted(position.Ticks);

                Debug.WriteLine("TsMediaStreamSource.SeekHandler({0}) completed, actual: {1}", seekTimestamp, position);

                State = SourceState.Play;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("TsMediaStreamSource.SeekHandler({0}) failed: {1}", seekTimestamp, ex.Message);
                ErrorOccurred("Seek failed: " + ex.Message);

                _taskScheduler.ThrowIfNotOnThread();
            }
        }

        void SignalHandler()
        {
            //Debug.WriteLine("TsMediaStreamSource.SignalHandler() pending {0}", _pendingOperations);

            _taskScheduler.ThrowIfNotOnThread();

            if (_isClosed)
                return;

            var lastOperation = Operation.None;

            for (; ; )
            {
                if (0 != HandleOperation(Operation.Seek))
                {
                    // Request the last operation again if we
                    // detect a possible Seek/GetSample race.
                    if (Operation.None != lastOperation)
                        RequestOperation(lastOperation);

                    var task = SeekHandler();

                    return;
                }

                if (SourceState.Play != State)
                    return;

                lastOperation = HandleOperation(Operation.Video | Operation.Audio);

                if (0 == lastOperation)
                    return;

                var reportBufferingMask = (Operation)_streamOpenFlags;

                var canCallReportBufferingProgress = reportBufferingMask == (lastOperation & reportBufferingMask);

                if (0 != (lastOperation & Operation.Video))
                {
                    if (null != _videoStreamSource
                        && !_videoStreamSource.GetNextSample(sample => StreamSampleHandler(sample, _videoStreamDescription, canCallReportBufferingProgress)))
                        RequestOperation(Operation.Video);
                }

                if (0 != (lastOperation & Operation.Audio))
                {
                    if (null != _audioStreamSource
                        && !_audioStreamSource.GetNextSample(sample => StreamSampleHandler(sample, _audioStreamDescription, canCallReportBufferingProgress)))
                        RequestOperation(Operation.Audio);
                }
            }
        }

        void ThrowIfDisposed()
        {
            if (0 == _isDisposed)
                return;

            throw new ObjectDisposedException(GetType().Name);
        }

        bool StreamSampleHandler(IStreamSample streamSample, MediaStreamDescription mediaStreamDescription, bool canCallReportBufferingProgress)
        {
            _taskScheduler.ThrowIfNotOnThread();

            if (_isClosed || null == streamSample)
            {
                SendLastStreamSample(mediaStreamDescription);
                return true;
            }

            var progress = streamSample.BufferingProgress;

            if (progress.HasValue)
            {
                if (!canCallReportBufferingProgress || Math.Abs(_bufferingProgress - progress.Value) < 0.05)
                    return false;

                Debug.WriteLine("Sample {0} buffering {1:F2}%", mediaStreamDescription.Type, progress * 100);

                _bufferingProgress = progress.Value;

                ValidateEvent(MediaStreamFsm.MediaEvent.CallingReportSampleCompleted);
                ReportGetSampleProgress(progress.Value);

                return false;
            }

            var sample = new MediaStreamSample(mediaStreamDescription, streamSample.Stream, 0, streamSample.Stream.Length, streamSample.Timestamp.Ticks, NoMediaSampleAttributes);

            //Debug.WriteLine("Sample {0} at {1}", sample.MediaStreamDescription.Type, TimeSpan.FromTicks(sample.Timestamp));

            ValidateEvent(MediaStreamFsm.MediaEvent.CallingReportSampleCompleted);
            ReportGetSampleCompleted(sample);

            return true;
        }

        void SendLastStreamSample(MediaStreamDescription mediaStreamDescription)
        {
            var sample = new MediaStreamSample(mediaStreamDescription, null, 0, 0, 0, NoMediaSampleAttributes);

            Debug.WriteLine("Sample {0} is null", mediaStreamDescription.Type);

            ValidateEvent(MediaStreamFsm.MediaEvent.CallingReportSampleCompleted);
            ReportGetSampleCompleted(sample);

            if (CloseStream(mediaStreamDescription.Type))
            {
                ValidateEvent(MediaStreamFsm.MediaEvent.StreamsClosed);
                _drainCompleted.Set();
            }
        }

        void ConfigureVideoStream(IVideoConfigurationSource configurationSource, IStreamSource videoSource)
        {
            var msa = new Dictionary<MediaStreamAttributeKeys, string>();

            msa[MediaStreamAttributeKeys.VideoFourCC] = configurationSource.VideoFourCc;

            var cpd = configurationSource.CodecPrivateData;

            if (!string.IsNullOrWhiteSpace(cpd))
                msa[MediaStreamAttributeKeys.CodecPrivateData] = cpd;

            msa[MediaStreamAttributeKeys.Height] = configurationSource.Height.ToString();
            msa[MediaStreamAttributeKeys.Width] = configurationSource.Width.ToString();

            var videoStreamDescription = new MediaStreamDescription(MediaStreamType.Video, msa);

            lock (_streamConfigurationLock)
            {
                _videoStreamSource = videoSource;
                _videoStreamDescription = videoStreamDescription;
            }
        }

        void ConfigureAudioStream(IAudioConfigurationSource configurationSource, IStreamSource audioSource)
        {
            var msa = new Dictionary<MediaStreamAttributeKeys, string>();

            var cpd = configurationSource.CodecPrivateData;

            if (!string.IsNullOrWhiteSpace(cpd))
                msa[MediaStreamAttributeKeys.CodecPrivateData] = cpd;

            var audioStreamDescription = new MediaStreamDescription(MediaStreamType.Audio, msa);

            lock (_streamConfigurationLock)
            {
                _audioStreamSource = audioSource;
                _audioStreamDescription = audioStreamDescription;
            }
        }

        void OpenStream(MediaStreamType type)
        {
            var operation = GetOperationFromType(type);

            var flag = (int)operation;

            var oldFlags = _streamOpenFlags;

            for (; ; )
            {
                var newFlags = oldFlags | flag;

                var flags = Interlocked.CompareExchange(ref _streamOpenFlags, newFlags, oldFlags);

                if (flags == oldFlags)
                    return;

                oldFlags = flags;
            }
        }

        bool CloseStream(MediaStreamType type)
        {
            var operation = GetOperationFromType(type);

            var flag = (int)operation;

            var oldFlags = _streamClosedFlags;

            for (; ; )
            {
                var newFlags = oldFlags & ~flag;

                if (newFlags == oldFlags)
                    return false;

                var flags = Interlocked.CompareExchange(ref _streamClosedFlags, newFlags, oldFlags);

                if (flags == oldFlags)
                    return oldFlags == _streamOpenFlags;

                oldFlags = flags;
            }
        }

        static Operation GetOperationFromType(MediaStreamType type)
        {
            switch (type)
            {
                case MediaStreamType.Audio:
                    return Operation.Audio;
                case MediaStreamType.Video:
                    return Operation.Video;
                default:
                    throw new ArgumentException("Only audio and video types are supported", "type");
            }
        }

        void CompleteConfigure(TimeSpan? duration)
        {
            var msd = new List<MediaStreamDescription>();

            if (null != _videoStreamSource && null != _videoStreamDescription)
            {
                msd.Add(_videoStreamDescription);
                OpenStream(_videoStreamDescription.Type);
            }

            if (null != _audioStreamSource && null != _audioStreamSource)
            {
                msd.Add(_audioStreamDescription);
                OpenStream(_audioStreamDescription.Type);
            }

            var mediaSourceAttributes = new Dictionary<MediaSourceAttributesKeys, string>();

            if (duration.HasValue)
                mediaSourceAttributes[MediaSourceAttributesKeys.Duration] = duration.Value.Ticks.ToString(CultureInfo.InvariantCulture);

            var canSeek = duration.HasValue;

            mediaSourceAttributes[MediaSourceAttributesKeys.CanSeek] = canSeek.ToString();

            Task.Factory.StartNew(() =>
                                  {
                                      Debug.WriteLine("TsMediaStreamSource: ReportOpenMediaCompleted ({0} streams)", msd.Count);

                                      foreach (var kv in mediaSourceAttributes)
                                          Debug.WriteLine("TsMediaStreamSource: ReportOpenMediaCompleted {0} = {1}", kv.Key, kv.Value);

                                      ValidateEvent(canSeek
                                          ? MediaStreamFsm.MediaEvent.CallingReportOpenMediaCompleted
                                          : MediaStreamFsm.MediaEvent.CallingReportOpenMediaCompletedLive);
                                      ReportOpenMediaCompleted(mediaSourceAttributes, msd);

                                      State = canSeek ? SourceState.Seek : SourceState.Play;
                                  }, CancellationToken.None, TaskCreationOptions.None, _taskScheduler);

            //ReportGetSampleProgress(0);
        }

        /// <summary>
        ///     The <see cref="T:System.Windows.Controls.MediaElement" /> calls this method to ask the
        ///     <see
        ///         cref="T:System.Windows.Media.MediaStreamSource" />
        ///     to open the media.
        /// </summary>
        protected override void OpenMediaAsync()
        {
            Debug.WriteLine("TsMediaStreamSource.OpenMediaAsync()");
            ValidateEvent(MediaStreamFsm.MediaEvent.OpenMediaAsyncCalled);

            ThrowIfDisposed();

            var mediaManager = MediaManager;

            if (null == mediaManager)
                throw new InvalidOperationException("MediaManager has not been initialized");

            mediaManager.OpenMedia();
        }

        /// <summary>
        ///     The <see cref="T:System.Windows.Controls.MediaElement" /> calls this method to ask the
        ///     <see
        ///         cref="T:System.Windows.Media.MediaStreamSource" />
        ///     to seek to the nearest randomly accessible point before the specified time. Developers respond to this method by calling
        ///     <see
        ///         cref="M:System.Windows.Media.MediaStreamSource.ReportSeekCompleted(System.Int64)" />
        ///     and by ensuring future calls to
        ///     <see
        ///         cref="M:System.Windows.Media.MediaStreamSource.ReportGetSampleCompleted(System.Windows.Media.MediaStreamSample)" />
        ///     will return samples from that point in the media.
        /// </summary>
        /// <param name="seekToTime"> The time as represented by 100 nanosecond increments to seek to. This is typically measured from the beginning of the media file. </param>
        protected override void SeekAsync(long seekToTime)
        {
            var seekTimestamp = TimeSpan.FromTicks(seekToTime);

            Debug.WriteLine("TsMediaStreamSource.SeekAsync({0})", seekTimestamp);
            ValidateEvent(MediaStreamFsm.MediaEvent.SeekAsyncCalled);

            StartSeek(seekTimestamp);
        }

        void StartSeek(TimeSpan seekTimestamp)
        {
            lock (_stateLock)
            {
                _state = SourceState.Seek;

                _pendingSeekTarget = _seekTarget ?? seekTimestamp;
            }

            RequestOperationAndSignal(Operation.Seek);
        }

        /// <summary>
        ///     The <see cref="T:System.Windows.Controls.MediaElement" /> calls this method to ask the
        ///     <see
        ///         cref="T:System.Windows.Media.MediaStreamSource" />
        ///     to prepare the next
        ///     <see
        ///         cref="T:System.Windows.Media.MediaStreamSample" />
        ///     of the requested stream type for the media pipeline.  Developers can respond to this method by calling either
        ///     <see
        ///         cref="M:System.Windows.Media.MediaStreamSource.ReportGetSampleCompleted(System.Windows.Media.MediaStreamSample)" />
        ///     or
        ///     <see
        ///         cref="M:System.Windows.Media.MediaStreamSource.ReportGetSampleProgress(System.Double)" />
        ///     .
        /// </summary>
        /// <param name="mediaStreamType">
        ///     The description of the stream that the next sample should come from which will be either
        ///     <see
        ///         cref="F:System.Windows.Media.MediaStreamType.Audio" />
        ///     or <see cref="F:System.Windows.Media.MediaStreamType.Video" /> .
        /// </param>
        protected override void GetSampleAsync(MediaStreamType mediaStreamType)
        {
            //Debug.WriteLine("TsMediaStreamSource.GetSampleAsync({0})", mediaStreamType);

            var op = LookupOperation(mediaStreamType);

            RequestOperationAndSignal(op);
        }

        /// <summary>
        ///     Called when a stream switch is requested on the <see cref="T:System.Windows.Controls.MediaElement" />.
        /// </summary>
        /// <param name="mediaStreamDescription"> The stream switched to. </param>
        protected override void SwitchMediaStreamAsync(MediaStreamDescription mediaStreamDescription)
        {
            Debug.WriteLine("TsMediaStreamSource.SwitchMediaStreamAsync()");

            throw new NotImplementedException();
        }

        /// <summary>
        ///     The <see cref="T:System.Windows.Controls.MediaElement" /> can call this method to request information about the
        ///     <see
        ///         cref="T:System.Windows.Media.MediaStreamSource" />
        ///     . Developers respond to this method by calling
        ///     <see
        ///         cref="M:System.Windows.Media.MediaStreamSource.ReportGetDiagnosticCompleted(System.Windows.Media.MediaStreamSourceDiagnosticKind,System.Int64)" />
        ///     .
        /// </summary>
        /// <param name="diagnosticKind">
        ///     A member of the <see cref="T:System.Windows.Media.MediaStreamSourceDiagnosticKind" /> enumeration describing what type of information is desired.
        /// </param>
        protected override void GetDiagnosticAsync(MediaStreamSourceDiagnosticKind diagnosticKind)
        {
            Debug.WriteLine("TsMediaStreamSource.GetDiagnosticAsync({0})", diagnosticKind);

            throw new NotImplementedException();
        }

        /// <summary>
        ///     The <see cref="T:System.Windows.Controls.MediaElement" /> can call this method when going through normal shutdown or as a result of an error. This lets the developer perform any needed cleanup of the
        ///     <see
        ///         cref="T:System.Windows.Media.MediaStreamSource" />
        ///     .
        /// </summary>
        protected override void CloseMedia()
        {
            Debug.WriteLine("TsMediaStreamSource.CloseMedia()");
            ValidateEvent(MediaStreamFsm.MediaEvent.CloseMediaCalled);

            lock (_stateLock)
            {
                _isClosed = true;

                _state = SourceState.Closed;
            }

            var mediaManager = MediaManager;

            if (null == mediaManager)
                throw new InvalidOperationException("MediaManager has not been initialized");

            mediaManager.CloseMedia();
        }

        Operation LookupOperation(MediaStreamType mediaStreamType)
        {
            switch (mediaStreamType)
            {
                case MediaStreamType.Audio:
                    return Operation.Audio;
                case MediaStreamType.Video:
                    return Operation.Video;
            }

            Debug.Assert(false);

            return 0;
        }

        void RequestOperationAndSignal(Operation operation)
        {
            if (RequestOperation(operation))
                _taskScheduler.Signal();
        }

        bool RequestOperation(Operation operation)
        {
            //Debug.WriteLine("TsMediaStreamSource.RequestOperation({0}) pending {1}", operation, _pendingOperations);

            var op = (int)operation;
            var current = _pendingOperations;

            for (; ; )
            {
                var value = current | op;

                if (value == current)
                    return false;

#pragma warning disable 0420
                var existing = Interlocked.CompareExchange(ref _pendingOperations, value, current);
#pragma warning restore 0420

                if (existing == current)
                    return true;

                current = existing;
            }
        }

        Operation HandleOperation(Operation operation)
        {
            var op = (int)operation;
            var current = _pendingOperations;

            for (; ; )
            {
                var value = current & ~op;

                if (value == current)
                    return Operation.None;

#pragma warning disable 0420
                var existing = Interlocked.CompareExchange(ref _pendingOperations, value, current);
#pragma warning restore 0420

                if (existing == current)
                    return (Operation)(current & op);

                current = existing;
            }
        }

        public Task WaitDrain()
        {
            if (0 == _streamOpenFlags)
                return TplTaskExtensions.CompletedTask;

            return _drainCompleted.WaitAsync();
        }

        #region Nested type: Operation

        [Flags]
        enum Operation
        {
            None = 0,
            Audio = 1,
            Video = 2,
            Seek = 4
        }

        #endregion

        #region Nested type: SourceState

        enum SourceState
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

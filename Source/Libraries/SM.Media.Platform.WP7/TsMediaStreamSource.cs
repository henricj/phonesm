// -----------------------------------------------------------------------
//  <copyright file="TsMediaStreamSource.cs" company="Henric Jungheim">
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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using SM.Media.Configuration;
using SM.Media.Utility;

namespace SM.Media
{
    public sealed class TsMediaStreamSource : MediaStreamSource, IDisposable
    {
        const int AudioStreamFlag = 1 << 0;
        const int VideoStreamFlag = 1 << 1;
        static readonly Dictionary<MediaSampleAttributeKeys, string> NoMediaSampleAttributes = new Dictionary<MediaSampleAttributeKeys, string>();
        readonly object _bufferingLock = new object();
        readonly CommandWorker _commandWorker = new CommandWorker();
        readonly AsyncManualResetEvent _drainCompleted = new AsyncManualResetEvent(true);
        readonly IMediaManager _mediaManager;
        readonly List<CommandWorker.Command> _pendingGets = new List<CommandWorker.Command>();
        readonly object _stateLock = new object();

        readonly object _streamConfigurationLock = new object();
        MediaStreamDescription _audioStreamDescription;
        IStreamSource _audioStreamSource;
        double _bufferingProgress;
        bool _bufferingReportPending;

        bool _isClosed;

        int _isDisposed;
        State _state;
        int _streamClosedFlags;
        int _streamOpenFlags;
        MediaStreamDescription _videoStreamDescription;
        IStreamSource _videoStreamSource;

        public TsMediaStreamSource(IMediaManager mediaManager)
        {
            if (null == mediaManager)
                throw new ArgumentNullException("mediaManager");

            //AudioBufferLength = 150;     // 150ms of internal buffering, instead of 1s.
            _mediaManager = mediaManager;
        }

        bool IsDisposed
        {
            get { return 0 != _isDisposed; }
        }

        #region IDisposable Members

        /// <summary>
        ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            var wasDisposed = Interlocked.Exchange(ref _isDisposed, 1);
            if (0 != wasDisposed)
                return;

            Debug.WriteLine("TsMediaStreamSource.Dispose()");
            _mediaManager.ValidateEvent(MediaStreamFsm.MediaEvent.DisposeCalled);

            _isClosed = true;

            _commandWorker.CloseAsync().Wait();
        }

        #endregion

        void ThrowIfDisposed()
        {
            if (0 == _isDisposed)
                return;

            throw new ObjectDisposedException(GetType().Name);
        }

        void StreamSampleHandler(IStreamSample streamSample, MediaStreamDescription mediaStreamDescription, int streamFlag)
        {
            if (!_isClosed && null != streamSample)
            {
                var sample = new MediaStreamSample(mediaStreamDescription, streamSample.Stream, 0, streamSample.Stream.Length, streamSample.Timestamp.Ticks, NoMediaSampleAttributes);

                Debug.WriteLine("Sample {0} at {1}", sample.MediaStreamDescription.Type, TimeSpan.FromTicks(sample.Timestamp));

                _mediaManager.ValidateEvent(MediaStreamFsm.MediaEvent.CallingReportSampleCompleted);
                ReportGetSampleCompleted(sample);
            }
            else
                SendLastStreamSample(mediaStreamDescription, streamFlag);
        }

        void SendLastStreamSample(MediaStreamDescription mediaStreamDescription, int streamFlag)
        {
            var sample = new MediaStreamSample(mediaStreamDescription, null, 0, 0, 0, NoMediaSampleAttributes);

            Debug.WriteLine("Sample is null");

            _mediaManager.ValidateEvent(MediaStreamFsm.MediaEvent.CallingReportSampleCompleted);
            ReportGetSampleCompleted(sample);

            if (0 != streamFlag && CloseStream(streamFlag))
            {
                _mediaManager.ValidateEvent(MediaStreamFsm.MediaEvent.StreamsClosed);
                _drainCompleted.Set();
            }
        }

        internal void MediaStreamOnConfigurationComplete(object sender, ConfigurationEventArgs configurationEventArgs)
        {
            var mediaStream = sender as IMediaParserMediaStream;

            if (null != mediaStream)
                mediaStream.ConfigurationComplete -= MediaStreamOnConfigurationComplete;

            var videoStream = configurationEventArgs.ConfigurationSource as IVideoConfigurationSource;

            if (null != videoStream)
            {
                ConfigureVideoStream(videoStream, configurationEventArgs.StreamSource);

                return;
            }

            var audioStream = configurationEventArgs.ConfigurationSource as IAudioConfigurationSource;

            if (null != audioStream)
            {
                ConfigureAudioStream(audioStream, configurationEventArgs.StreamSource);

                return;
            }
        }

        void ConfigureVideoStream(IVideoConfigurationSource configurationSource, IStreamSource videoSource)
        {
            videoSource.SetSink(streamSample => StreamSampleHandler(streamSample, _videoStreamDescription, VideoStreamFlag));

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

                CompleteConfigure();
            }
        }

        void ConfigureAudioStream(IAudioConfigurationSource configurationSource, IStreamSource audioSource)
        {
            audioSource.SetSink(streamSample => StreamSampleHandler(streamSample, _audioStreamDescription, AudioStreamFlag));

            var msa = new Dictionary<MediaStreamAttributeKeys, string>();

            var cpd = configurationSource.CodecPrivateData;

            if (!string.IsNullOrWhiteSpace(cpd))
                msa[MediaStreamAttributeKeys.CodecPrivateData] = cpd;

            var audioStreamDescription = new MediaStreamDescription(MediaStreamType.Audio, msa);

            lock (_streamConfigurationLock)
            {
                _audioStreamSource = audioSource;
                _audioStreamDescription = audioStreamDescription;

                CompleteConfigure();
            }
        }

        void OpenStream(int flag)
        {
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

        bool CloseStream(int flag)
        {
            var oldFlags = _streamClosedFlags;

            for (; ; )
            {
                var newFlags = oldFlags & ~flag;

                if (newFlags == oldFlags)
                {
                    Debug.Assert(false, "Attempting to close a closed stream");
                    return false;
                }

                var flags = Interlocked.CompareExchange(ref _streamClosedFlags, newFlags, oldFlags);

                if (flags == oldFlags)
                    return oldFlags == _streamOpenFlags;

                oldFlags = flags;
            }
        }

        void CompleteConfigure()
        {
            if (null == _videoStreamSource || null == _audioStreamSource)
                return;

            var msd = new List<MediaStreamDescription>();

            if (null != _videoStreamSource && null != _videoStreamDescription)
            {
                msd.Add(_videoStreamDescription);
                OpenStream(VideoStreamFlag);
            }

            if (null != _audioStreamSource && null != _audioStreamSource)
            {
                msd.Add(_audioStreamDescription);
                OpenStream(AudioStreamFlag);
            }

            var mediaSourceAttributes = new Dictionary<MediaSourceAttributesKeys, string>();

            //mediaSourceAttributes[MediaSourceAttributesKeys.Duration] = TimeSpan.FromMinutes(10).Ticks.ToString(CultureInfo.InvariantCulture);
            mediaSourceAttributes[MediaSourceAttributesKeys.CanSeek] = "0";

            _commandWorker.SendCommand(new CommandWorker.Command(
                                           () =>
                                           {
                                               Debug.WriteLine("TsMediaStreamSource: ReportOpenMediaCompleted ({0} streams)", msd.Count);

                                               _mediaManager.ValidateEvent(MediaStreamFsm.MediaEvent.CallingReportOpenMediaCompleted);
                                               ReportOpenMediaCompleted(mediaSourceAttributes, msd);

                                               return null;
                                           }));

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
            _mediaManager.ValidateEvent(MediaStreamFsm.MediaEvent.OpenMediaAsyncCalled);

            ThrowIfDisposed();

            _mediaManager.OpenMedia();
        }

        internal void ReportProgress(double bufferingProgress)
        {
            lock (_bufferingLock)
            {
                _bufferingProgress = bufferingProgress;

                if (_bufferingReportPending)
                    return;

                _bufferingReportPending = true;

                _commandWorker.SendCommand(new CommandWorker.Command(
                                               () =>
                                               {
                                                   double value;

                                                   lock (_bufferingLock)
                                                   {
                                                       _bufferingReportPending = false;
                                                       value = _bufferingProgress;
                                                   }

                                                   ReportGetSampleProgress(value);

                                                   return null;
                                               }));
            }
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
            _mediaManager.ValidateEvent(MediaStreamFsm.MediaEvent.SeekAsyncCalled);

            lock (_stateLock)
            {
                _state = State.Seek;
            }

            _commandWorker.SendCommand(new CommandWorker.Command(
                                           async () =>
                                           {
                                               if (_isClosed)
                                                   return;

                                               var position = await _mediaManager.SeekMediaAsync(seekTimestamp);

                                               if (_isClosed)
                                                   return;

                                               _mediaManager.ValidateEvent(MediaStreamFsm.MediaEvent.CallingReportSeekCompleted);
                                               ReportSeekCompleted(position.Ticks);

                                               lock (_stateLock)
                                               {
                                                   foreach (var getCmd in _pendingGets)
                                                       _commandWorker.SendCommand(getCmd);

                                                   _pendingGets.Clear();

                                                   _state = State.Play;
                                               }
                                           }));
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
            var command = new CommandWorker.Command(() =>
                                                    {
                                                        IStreamSource streamSource = null;
                                                        MediaStreamDescription streamDescription = null;
                                                        var streamFlag = 0;

                                                        switch (mediaStreamType)
                                                        {
                                                            case MediaStreamType.Video:
                                                                streamSource = _videoStreamSource;
                                                                streamDescription = _videoStreamDescription;
                                                                streamFlag = VideoStreamFlag;
                                                                break;
                                                            case MediaStreamType.Audio:
                                                                streamSource = _audioStreamSource;
                                                                streamDescription = _audioStreamDescription;
                                                                streamFlag = AudioStreamFlag;
                                                                break;
                                                        }

                                                        if (null == streamSource)
                                                        {
                                                            ReportGetSampleProgress(0);
                                                            return null;
                                                        }

                                                        lock (_stateLock)
                                                        { }
                                                        if (_isClosed)
                                                        {
                                                            SendLastStreamSample(streamDescription, streamFlag);
                                                            return null;
                                                        }

                                                        streamSource.GetNextSample();

                                                        return null;
                                                    });

            lock (_stateLock)
            {
                var state = _state;

                Debug.WriteLine("TsMediaStreamSource.GetSampleAsync({0}) state {1}", mediaStreamType, state);

                _mediaManager.ValidateEvent(MediaStreamFsm.MediaEvent.GetSampleAsyncCalled);

                if (State.Play != state)
                {
                    Debug.WriteLine("TsMediaStreamSource defer Get({0})", mediaStreamType);
                    _pendingGets.Add(command);
                }
                else
                    _commandWorker.SendCommand(command);
            }
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
            _mediaManager.ValidateEvent(MediaStreamFsm.MediaEvent.CloseMediaCalled);

            lock (_stateLock)
            {
                _isClosed = true;

                _state = State.Closed;
            }

            _mediaManager.CloseMedia();
        }

        public Task CloseAsync()
        {
            if (0 == _streamOpenFlags)
                return TplTaskExtensions.CompletedTask;

            lock (_stateLock)
            {
                _isClosed = true;

                _state = State.WaitForClose;
            }

            return _drainCompleted.WaitAsync();
        }

        public Task WaitDrain()
        {
            if (0 == _streamOpenFlags)
                return TplTaskExtensions.CompletedTask;

            return _drainCompleted.WaitAsync();
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
// -----------------------------------------------------------------------
//  <copyright file="MediaStreamConfigurator.cs" company="Henric Jungheim">
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
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using SM.Media.Configuration;
using SM.Media.MediaParser;
using SM.Media.Utility;

namespace SM.Media
{
    public sealed class MediaStreamConfigurator : IMediaStreamConfigurator, IMediaStreamControl
    {
        static readonly TimeSpan OpenTimeout = TimeSpan.FromSeconds(20);
        readonly Func<TsMediaStreamSource> _mediaStreamSourceFactory;
        readonly object _streamConfigurationLock = new object();
        MediaStreamDescription _audioStreamDescription;
        IStreamSource _audioStreamSource;
        TsMediaStreamSource _mediaStreamSource;
        TaskCompletionSource<IMediaStreamConfiguration> _openCompletionSource;
        int _streamClosedFlags;
        int _streamOpenFlags;
        MediaStreamDescription _videoStreamDescription;
        IStreamSource _videoStreamSource;

        public MediaStreamConfigurator(Func<TsMediaStreamSource> mediaStreamSourceFactory)
        {
            _mediaStreamSourceFactory = mediaStreamSourceFactory;
        }

        public MediaStreamDescription AudioStreamDescription
        {
            set { _audioStreamDescription = value; }
            get { return _audioStreamDescription; }
        }

        public MediaStreamDescription VideoStreamDescription
        {
            set { _videoStreamDescription = value; }
            get { return _videoStreamDescription; }
        }

        public int StreamOpenFlags
        {
            get { return _streamOpenFlags; }
        }

        IStreamSource AudioStreamSource
        {
            set { _audioStreamSource = value; }
            get { return _audioStreamSource; }
        }

        IStreamSource VideoStreamSource
        {
            set { _videoStreamSource = value; }
            get { return _videoStreamSource; }
        }

        #region IMediaStreamConfigurator Members

        public void Configure(IMediaConfiguration configuration)
        {
            if (null != configuration.Audio)
                ConfigureAudioStream(configuration.Audio);

            if (null != configuration.Video)
                ConfigureVideoStream(configuration.Video);

            lock (_streamConfigurationLock)
            {
                CompleteConfigure(configuration.Duration);
            }
        }

        public void Dispose()
        {
            AudioStreamSource = null;
            AudioStreamDescription = null;

            VideoStreamSource = null;
            VideoStreamDescription = null;
        }

        public IMediaManager MediaManager { get; set; }

        public TimeSpan? SeekTarget
        {
            get { return _mediaStreamSource.SeekTarget; }
            set { _mediaStreamSource.SeekTarget = value; }
        }

        public Task<TMediaStreamSource> CreateMediaStreamSourceAsync<TMediaStreamSource>(CancellationToken cancellationToken)
            where TMediaStreamSource : class
        {
            if (null != _mediaStreamSource)
                throw new InvalidOperationException("MediaStreamSource already exists");

            _mediaStreamSource = _mediaStreamSourceFactory();

            var ret = _mediaStreamSource as TMediaStreamSource;

            if (null == ret)
                throw new InvalidCastException(string.Format("Cannot convert {0} to {1}", _mediaStreamSource.GetType().FullName, typeof(TMediaStreamSource).FullName));

            return TaskEx.FromResult(ret);
        }

        public Task CloseAsync()
        {
            return _mediaStreamSource.CloseAsync();
        }

        public void ReportError(string message)
        {
            _mediaStreamSource.ReportError(message);
        }

        public void CheckForSamples()
        {
            _mediaStreamSource.CheckForSamples();
        }

        public void ValidateEvent(MediaStreamFsm.MediaEvent mediaEvent)
        {
            _mediaStreamSource.ValidateEvent(mediaEvent);
        }

        #endregion

        #region IMediaStreamControl Members

        async Task<IMediaStreamConfiguration> IMediaStreamControl.OpenAsync(CancellationToken cancellationToken)
        {
            if (null == _mediaStreamSource)
                throw new InvalidOperationException("MediaStreamSource has not been created");

            var mediaManager = MediaManager;

            if (null == mediaManager)
                throw new InvalidOperationException("MediaManager has not been initialized");

            if (null != _openCompletionSource && !_openCompletionSource.Task.IsCompleted)
                _openCompletionSource.TrySetCanceled();

            var openCompletionSource = new TaskCompletionSource<IMediaStreamConfiguration>();

            _openCompletionSource = openCompletionSource;

            Action cancellationAction = () =>
            {
                mediaManager.CloseMedia();
                openCompletionSource.TrySetCanceled();
            };

            using (cancellationToken.Register(cancellationAction))
            {
                var timeoutTask = TaskEx.Delay(OpenTimeout, cancellationToken);

                mediaManager.OpenMedia();

                await TaskEx.WhenAny(_openCompletionSource.Task, timeoutTask).ConfigureAwait(false);
            }

            if (!_openCompletionSource.Task.IsCompleted)
                cancellationAction();

            return await _openCompletionSource.Task.ConfigureAwait(false);
        }

        Task<TimeSpan> IMediaStreamControl.SeekAsync(TimeSpan position, CancellationToken cancellationToken)
        {
            var mediaManager = MediaManager;

            if (null == mediaManager)
                throw new InvalidOperationException("MediaManager has not been initialized");

            return mediaManager.SeekMediaAsync(position);
        }

        async Task IMediaStreamControl.CloseAsync(CancellationToken cancellationToken)
        {
            var mediaManager = MediaManager;

            if (null == mediaManager)
            {
                Debug.WriteLine("MediaStreamConfigurator.CloseMediaHandler() null media manager");
                return;
            }

            mediaManager.CloseMedia();
        }

        #endregion

        void CompleteConfigure(TimeSpan? duration)
        {
            try
            {
                var descriptions = new List<MediaStreamDescription>();

                if (null != _videoStreamSource && null != _videoStreamDescription)
                {
                    descriptions.Add(_videoStreamDescription);

                    OpenStream(TsMediaStreamSource.Operation.Video);
                }

                if (null != _audioStreamSource && null != _audioStreamSource)
                {
                    descriptions.Add(_audioStreamDescription);

                    OpenStream(TsMediaStreamSource.Operation.Audio);
                }

                var mediaSourceAttributes = new Dictionary<MediaSourceAttributesKeys, string>();

                if (duration.HasValue)
                    mediaSourceAttributes[MediaSourceAttributesKeys.Duration] = duration.Value.Ticks.ToString(CultureInfo.InvariantCulture);

                var canSeek = duration.HasValue;

                mediaSourceAttributes[MediaSourceAttributesKeys.CanSeek] = canSeek.ToString();

                var configuration = new MediaStreamConfiguration
                {
                    VideoStreamSource = _videoStreamSource,
                    AudioStreamSource = _audioStreamSource,
                    Descriptions = descriptions,
                    Attributes = mediaSourceAttributes,
                    Duration = duration
                };

                _openCompletionSource.TrySetResult(configuration);
            }
            catch (OperationCanceledException)
            {
                _openCompletionSource.TrySetCanceled();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MediaStreamConfigurator.CompleteConfigure() failed: " + ex.Message);

                _openCompletionSource.TrySetException(ex);
            }
        }

        void ConfigureVideoStream(IMediaParserMediaStream video)
        {
            var configurationSource = (IVideoConfigurationSource)video.ConfigurationSource;

            var msa = new Dictionary<MediaStreamAttributeKeys, string>();

            msa[MediaStreamAttributeKeys.VideoFourCC] = configurationSource.VideoFourCc;

            var cpd = configurationSource.CodecPrivateData;

            Debug.WriteLine("MediaStreamConfigurator.ConfigureVideoStream(): CodecPrivateData: " + cpd);

            if (!string.IsNullOrWhiteSpace(cpd))
                msa[MediaStreamAttributeKeys.CodecPrivateData] = cpd;

            msa[MediaStreamAttributeKeys.Height] = configurationSource.Height.ToString();
            msa[MediaStreamAttributeKeys.Width] = configurationSource.Width.ToString();

            var videoStreamDescription = new MediaStreamDescription(MediaStreamType.Video, msa);

            lock (_streamConfigurationLock)
            {
                _videoStreamSource = video.StreamSource;
                _videoStreamDescription = videoStreamDescription;
            }
        }

        void ConfigureAudioStream(IMediaParserMediaStream audio)
        {
            var configurationSource = (IAudioConfigurationSource)audio.ConfigurationSource;

            var msa = new Dictionary<MediaStreamAttributeKeys, string>();

            var cpd = configurationSource.CodecPrivateData;

            Debug.WriteLine("TsMediaStreamSource.ConfigureAudioStream(): CodecPrivateData: " + cpd);

            if (!string.IsNullOrWhiteSpace(cpd))
                msa[MediaStreamAttributeKeys.CodecPrivateData] = cpd;

            var audioStreamDescription = new MediaStreamDescription(MediaStreamType.Audio, msa);

            lock (_streamConfigurationLock)
            {
                _audioStreamSource = audio.StreamSource;
                _audioStreamDescription = audioStreamDescription;
            }
        }

        void OpenStream(TsMediaStreamSource.Operation operation)
        {
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

        public bool CloseStream(TsMediaStreamSource.Operation operation)
        {
            var flag = (int)operation;

            var oldFlags = _streamClosedFlags;

            for (; ; )
            {
                var newFlags = oldFlags | flag;

                if (newFlags == oldFlags)
                    return false;

                var flags = Interlocked.CompareExchange(ref _streamClosedFlags, newFlags, oldFlags);

                if (flags == oldFlags)
                    return newFlags == _streamOpenFlags;

                oldFlags = flags;
            }
        }
    }
}

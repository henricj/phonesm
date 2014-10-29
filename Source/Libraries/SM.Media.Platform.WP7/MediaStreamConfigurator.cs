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
        IMediaManager _mediaManager;
        TsMediaStreamSource _mediaStreamSource;
        TaskCompletionSource<IMediaStreamConfiguration> _openCompletionSource;
        TaskCompletionSource<object> _playingCompletionSource;
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

        public Task PlayAsync(IMediaConfiguration configuration, CancellationToken cancellationToken)
        {
            //Debug.WriteLine("MediaStreamConfigurator.PlayAsync()");

            if (null != configuration.Audio)
                ConfigureAudioStream(configuration.Audio);

            if (null != configuration.Video)
                ConfigureVideoStream(configuration.Video);

            lock (_streamConfigurationLock)
            {
                CompleteConfigure(configuration.Duration);
            }

            return _playingCompletionSource.Task;
        }

        public void Dispose()
        {
            AudioStreamSource = null;
            AudioStreamDescription = null;

            VideoStreamSource = null;
            VideoStreamDescription = null;

            CleanupMediaStreamSource();
        }

        public IMediaManager MediaManager
        {
            get { return _mediaManager; }
            set
            {
                if (ReferenceEquals(_mediaManager, value))
                    return;

                _mediaManager = value;

                if (null == value)
                    CleanupMediaStreamSource();
            }
        }

        public TimeSpan? SeekTarget
        {
            get { return _mediaStreamSource.SeekTarget; }
            set { _mediaStreamSource.SeekTarget = value; }
        }

        public Task<TMediaStreamSource> CreateMediaStreamSourceAsync<TMediaStreamSource>(CancellationToken cancellationToken)
            where TMediaStreamSource : class
        {
            //Debug.WriteLine("MediaStreamConfigurator.CreateMediaStreamSourceAsync()");

            cancellationToken.ThrowIfCancellationRequested();

            if (null != _mediaStreamSource)
                throw new InvalidOperationException("MediaStreamSource already exists");

            _mediaStreamSource = _mediaStreamSourceFactory();

            var ret = _mediaStreamSource as TMediaStreamSource;

            if (null == ret)
                throw new InvalidCastException(string.Format("Cannot convert {0} to {1}", _mediaStreamSource.GetType().FullName, typeof(TMediaStreamSource).FullName));

            _playingCompletionSource = new TaskCompletionSource<object>();

            return TaskEx.FromResult(ret);
        }

        public async Task CloseAsync()
        {
            //Debug.WriteLine("MediaStreamConfigurator.CloseAsync()");

            var mediaStreamSource = _mediaStreamSource;

            if (null != mediaStreamSource)
                await mediaStreamSource.CloseAsync().ConfigureAwait(false);

            var playingTask = _playingCompletionSource;

            if (null != playingTask)
            {
                await _playingCompletionSource.Task.ConfigureAwait(false);
            }
        }

        public void ReportError(string message)
        {
            //Debug.WriteLine("MediaStreamConfigurator.ReportError() " + message);

            var mediaStreamSource = _mediaStreamSource;

            if (null == mediaStreamSource)
                Debug.WriteLine("MediaStreamConfigurator.ReportError() null _mediaStreamSource");
            else
                mediaStreamSource.ReportError(message);
        }

        public void CheckForSamples()
        {
            var mm = _mediaStreamSource;

            if (null != mm)
                mm.CheckForSamples();
        }

        public void ValidateEvent(MediaStreamFsm.MediaEvent mediaEvent)
        {
            var mm = _mediaStreamSource;

            if (null != mm)
                mm.ValidateEvent(mediaEvent);
        }

        #endregion

        #region IMediaStreamControl Members

        async Task<IMediaStreamConfiguration> IMediaStreamControl.OpenAsync(CancellationToken cancellationToken)
        {
            //Debug.WriteLine("MediaStreamConfigurator.IMediaStreamControl.OpenAsync()");

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
                var task = mediaManager.CloseMediaAsync();

                TaskCollector.Default.Add(task, "MediaSteamConfigurator.OpenAsync mediaManager.CloseMediaAsync");

                openCompletionSource.TrySetCanceled();
            };

            using (cancellationToken.Register(cancellationAction))
            {
                var timeoutTask = TaskEx.Delay(OpenTimeout, cancellationToken);

                await TaskEx.WhenAny(_openCompletionSource.Task, timeoutTask).ConfigureAwait(false);
            }

            if (!_openCompletionSource.Task.IsCompleted)
                cancellationAction();

            return await _openCompletionSource.Task.ConfigureAwait(false);
        }

        Task<TimeSpan> IMediaStreamControl.SeekAsync(TimeSpan position, CancellationToken cancellationToken)
        {
            //Debug.WriteLine("MediaStreamConfigurator.IMediaStreamControl.SeekAsync() " + position);

            var mediaManager = MediaManager;

            if (null == mediaManager)
                throw new InvalidOperationException("MediaManager has not been initialized");

            return mediaManager.SeekMediaAsync(position);
        }

        Task IMediaStreamControl.CloseAsync(CancellationToken cancellationToken)
        {
            //Debug.WriteLine("MediaStreamConfigurator.IMediaStreamControl.CloseAsync");

            var mediaManager = MediaManager;

            if (null == mediaManager)
            {
                Debug.WriteLine("MediaStreamConfigurator.CloseMediaHandler() null media manager");
                return TplTaskExtensions.CompletedTask;
            }

            _playingCompletionSource.TrySetResult(null);

            return TplTaskExtensions.CompletedTask;
        }

        #endregion

        void CleanupMediaStreamSource()
        {
            var mss = _mediaStreamSource;

            if (null != mss)
            {
                _mediaStreamSource = null;

                mss.DisposeSafe();
            }
        }

        void CompleteConfigure(TimeSpan? duration)
        {
            //Debug.WriteLine("MediaStreamConfigurator.CompleteConfigure() " + duration);

            try
            {
                var descriptions = new List<MediaStreamDescription>();

                if (null != _videoStreamSource && null != _videoStreamDescription)
                    descriptions.Add(_videoStreamDescription);

                if (null != _audioStreamSource && null != _audioStreamSource)
                    descriptions.Add(_audioStreamDescription);

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
    }
}

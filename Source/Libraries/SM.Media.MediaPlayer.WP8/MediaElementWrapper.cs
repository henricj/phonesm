// -----------------------------------------------------------------------
//  <copyright file="MediaElementWrapper.cs" company="Henric Jungheim">
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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.PlayerFramework;
using SM.Media.Segments;
using SM.Media.Utility;
using SM.Media.Web;
using LogReadyRoutedEventArgs = System.Windows.Media.LogReadyRoutedEventArgs;
using LogReadyRoutedEventHandler = Microsoft.PlayerFramework.LogReadyRoutedEventHandler;
using TimelineMarkerRoutedEventArgs = System.Windows.Media.TimelineMarkerRoutedEventArgs;
using TimelineMarkerRoutedEventHandler = Microsoft.PlayerFramework.TimelineMarkerRoutedEventHandler;

namespace SM.Media.MediaPlayer
{
    /// <summary>
    ///     Wraps the MediaElement to allow it to adhere to the IMediaElement interface.
    ///     IMediaElement is used to allow the SmoothStreamingMediaElement or other custom MediaElements to be used by
    ///     MediaPlayer
    /// </summary>
    /// <remarks>
    ///     This code is based on
    ///     https://playerframework.codeplex.com/SourceControl/latest#Phone.SL/Controls/MediaElementWrapper.cs
    /// </remarks>
    public class MediaElementWrapper : ContentControl, IMediaElement
    {
        readonly IHttpClients _httpClients;
        readonly TaskCompletionSource<object> _templateAppliedTaskSource;
        readonly SignalTask _workerTask;
        readonly object _lock = new object();
        Uri _requestedSource;
        Uri _source;
        bool _closeRequested;
        TsMediaManager _tsMediaManager;
        TsMediaStreamSource _tsMediaStreamSource;
        ISegmentManager _playlist;
        readonly ISegmentManagerFactory _segmentManagerFactory;
        string _defaultSourceType = ".m3u8";

        /// <summary>
        ///     The MediaElement being wrapped
        /// </summary>
        MediaElement _mediaElement;

        /// <summary>
        ///     Select the source type to be used if a type cannot be guessed from the source URL.  The default is ".m3u8".
        /// </summary>
        /// createD
        public string DefaultSourceType
        {
            get { return _defaultSourceType; }
            set { _defaultSourceType = value; }
        }

        /// <summary>
        ///     Creates a new instance of the MediaElementWrapper class.
        /// </summary>
        /// <param name="httpClients"></param>
        /// <param name="segmentManagerFactory"></param>
        public MediaElementWrapper(IHttpClients httpClients, ISegmentManagerFactory segmentManagerFactory)
        {
            Debug.WriteLine("MediaElementWrapper.ctor()");

            if (httpClients == null)
                throw new ArgumentNullException("httpClients");
            if (segmentManagerFactory == null)
                throw new ArgumentNullException("segmentManagerFactory");

            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            VerticalContentAlignment = VerticalAlignment.Stretch;

            _httpClients = httpClients;
            _segmentManagerFactory = segmentManagerFactory;

            MediaElement = new MediaElement();
            MediaElement.MediaEnded += MediaElement_MediaEnded;
            MediaElement.MediaFailed += MediaElement_MediaFailed;
#if WINDOWS_PHONE
            MediaElement.MarkerReached += MediaElement_MarkerReached;
#endif
            Content = MediaElement;
            _templateAppliedTaskSource = new TaskCompletionSource<object>();

            _workerTask = new SignalTask(Worker, CancellationToken.None);
        }

        async Task Worker()
        {
            try
            {
                bool closeRequested;
                Uri requestedSource;

                lock (_lock)
                {
                    closeRequested = _closeRequested;

                    if (closeRequested)
                        _closeRequested = false;

                    requestedSource = _requestedSource;

                    if (null != requestedSource)
                        _requestedSource = null;
                }

                if (closeRequested)
                {
                    //Debug.WriteLine("MediaElementWrapper.Worker() calling CloseMediaAsync()");

                    //var stopwatch = Stopwatch.StartNew();

                    await CloseMediaAsync().ConfigureAwait(false);

                    //stopwatch.Stop();

                    //Debug.WriteLine("MediaElementWrapper.Worker() returned from CloseMediaAsync() after " + stopwatch.Elapsed);

                    return;
                }

                if (null != requestedSource)
                {
                    //Debug.WriteLine("MediaElementWrapper.Worker() calling SetMediaSourceAsync()");

                    //var stopwatch = Stopwatch.StartNew();

                    await SetMediaSourceAsync(requestedSource).ConfigureAwait(false);

                    //stopwatch.Stop();

                    //Debug.WriteLine("MediaElementWrapper.Worker() returned from SetMediaSourceAsync() after " + stopwatch.Elapsed);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MediaElementWrapper.Worker() failed: " + ex.Message);
            }
        }

        /// <summary>
        ///     The underlying MediaElement being wrapped
        /// </summary>
        protected MediaElement MediaElement
        {
            get { return _mediaElement; }
            private set
            {
                Debug.WriteLine("MediaElementWrapper.MediaElement setter");

                if (_mediaElement != null)
                {
                    _mediaElement.CurrentStateChanged -= mediaElement_CurrentStateChanged;
                    _mediaElement.LogReady -= mediaElement_LogReady;
#if !WINDOWS_PHONE
                    mediaElement.RateChanged -= mediaElement_RateChanged;
#endif
                }

                _mediaElement = value;

                if (_mediaElement != null)
                {
                    _mediaElement.CurrentStateChanged += mediaElement_CurrentStateChanged;
                    _mediaElement.LogReady += mediaElement_LogReady;
#if !WINDOWS_PHONE
                    mediaElement.RateChanged += mediaElement_RateChanged;
#endif
                }
            }
        }

        #region IMediaElement Members

        /// <inheritdoc />
        public Task TemplateAppliedTask
        {
            get { return _templateAppliedTaskSource.Task; }
        }

        /// <inheritdoc />
        public event RoutedEventHandler BufferingProgressChanged
        {
            add { MediaElement.BufferingProgressChanged += value; }
            remove { MediaElement.BufferingProgressChanged -= value; }
        }

        /// <inheritdoc />
        public event RoutedEventHandler CurrentStateChanged;

        /// <inheritdoc />
        public event RoutedEventHandler DownloadProgressChanged
        {
            add { MediaElement.DownloadProgressChanged += value; }
            remove { MediaElement.DownloadProgressChanged -= value; }
        }

        /// <inheritdoc />
        public event LogReadyRoutedEventHandler LogReady;

#if WINDOWS_PHONE
        /// <inheritdoc />
        public event TimelineMarkerRoutedEventHandler MarkerReached;
#endif

        /// <inheritdoc />
        public event RoutedEventHandler MediaEnded
        {
            add { MediaElement.MediaEnded += value; }
            remove { MediaElement.MediaEnded -= value; }
        }

        /// <inheritdoc />
        public event EventHandler<ExceptionRoutedEventArgs> MediaFailed
        {
            add { MediaElement.MediaFailed += value; }
            remove { MediaElement.MediaFailed -= value; }
        }

        /// <inheritdoc />
        public event RoutedEventHandler MediaOpened
        {
            add { MediaElement.MediaOpened += value; }
            remove { MediaElement.MediaOpened -= value; }
        }

#if !WINDOWS_PHONE
        void mediaElement_RateChanged(object sender, System.Windows.Media.RateChangedRoutedEventArgs e)
        {
            if (RateChanged != null) RateChanged(this, new RateChangedRoutedEventArgs(e.NewRate));
        }

        /// <inheritdoc /> 
        public event RateChangedRoutedEventHandler RateChanged;
        
        /// <inheritdoc /> 
        public System.Collections.Generic.Dictionary<string, string> Attributes
        {
            get { return MediaElement.Attributes; }
        }

        /// <inheritdoc /> 
        public bool IsDecodingOnGPU
        {
            get { return MediaElement.IsDecodingOnGPU; }
        }

        /// <inheritdoc /> 
        public double PlaybackRate
        {
            get { return MediaElement.PlaybackRate; }
            set { MediaElement.PlaybackRate = value; }
        }
#endif

        /// <inheritdoc />
        public void Pause()
        {
            Debug.WriteLine("MediaElementWrapper.Pause()");

            MediaElement.Pause();
        }

        /// <inheritdoc />
        public void Play()
        {
            Debug.WriteLine("MediaElementWrapper.Play()");

            //MediaElement.Play();

            var task = StartPlaybackWrapperAsync();

            TaskCollector.Default.Add(task, "Play: StartPlaybackWrapperAsync()");
            // We do not wait for the task to complete.
        }

        /// <inheritdoc />
        public void RequestLog()
        {
            Debug.WriteLine("MediaElementWrapper.RequestLog()");

            MediaElement.RequestLog();
        }

        /// <inheritdoc />
        public void SetSource(Stream stream)
        {
            Debug.WriteLine("MediaElementWrapper.SetSource(Stream)");

            MediaElement.SetSource(stream);
        }

        /// <inheritdoc />
        public void SetSource(MediaStreamSource mediaStreamSource)
        {
            Debug.WriteLine("MediaElementWrapper.SetSource(MediaStreamSource)");

            MediaElement.SetSource(mediaStreamSource);
        }

        /// <inheritdoc />
        public void Stop()
        {
            Debug.WriteLine("MediaElementWrapper.Stop()");

            MediaElement.Stop();
        }

        /// <inheritdoc />
        public int AudioStreamCount
        {
            get { return MediaElement.AudioStreamCount; }
        }

        /// <inheritdoc />
        public int? AudioStreamIndex
        {
            get { return MediaElement.AudioStreamIndex; }
            set { MediaElement.AudioStreamIndex = value; }
        }

        /// <inheritdoc />
        public bool AutoPlay
        {
            get { return MediaElement.AutoPlay; }
            set { MediaElement.AutoPlay = value; }
        }

        /// <inheritdoc />
        public double Balance
        {
            get { return MediaElement.Balance; }
            set { MediaElement.Balance = value; }
        }

        /// <inheritdoc />
        public double BufferingProgress
        {
            get { return MediaElement.BufferingProgress; }
        }

        /// <inheritdoc />
        public TimeSpan BufferingTime
        {
            get { return MediaElement.BufferingTime; }
            set { MediaElement.BufferingTime = value; }
        }

        /// <inheritdoc />
        public bool CanPause
        {
            get { return MediaElement.CanPause; }
        }

        /// <inheritdoc />
        public bool CanSeek
        {
            get { return MediaElement.CanSeek; }
        }

        /// <inheritdoc />
        public MediaElementState CurrentState
        {
            get { return MediaElement.CurrentState; }
        }

        /// <inheritdoc />
        public double DownloadProgress
        {
            get { return MediaElement.DownloadProgress; }
        }

        /// <inheritdoc />
        public double DownloadProgressOffset
        {
            get { return MediaElement.DownloadProgressOffset; }
        }

        /// <inheritdoc />
        public double DroppedFramesPerSecond
        {
            get { return MediaElement.DroppedFramesPerSecond; }
        }

        /// <inheritdoc />
        public bool IsMuted
        {
            get { return MediaElement.IsMuted; }
            set { MediaElement.IsMuted = value; }
        }

        /// <inheritdoc />
        public LicenseAcquirer LicenseAcquirer
        {
            get { return MediaElement.LicenseAcquirer; }
            set { MediaElement.LicenseAcquirer = value; }
        }

        /// <inheritdoc />
        public TimelineMarkerCollection Markers
        {
            get { return MediaElement.Markers; }
        }

        /// <inheritdoc />
        public Duration NaturalDuration
        {
            get { return MediaElement.NaturalDuration; }
        }

        /// <inheritdoc />
        public int NaturalVideoHeight
        {
            get { return MediaElement.NaturalVideoHeight; }
        }

        /// <inheritdoc />
        public int NaturalVideoWidth
        {
            get { return MediaElement.NaturalVideoWidth; }
        }

        /// <inheritdoc />
        public TimeSpan Position
        {
            get { return MediaElement.Position; }
            set { MediaElement.Position = value; }
        }

        /// <inheritdoc />
        public double RenderedFramesPerSecond
        {
            get { return MediaElement.RenderedFramesPerSecond; }
        }

        /// <inheritdoc />
        public Uri Source
        {
            get { return _source; }
            set
            {
                Debug.WriteLine("MediaElementWrapper.Source setter: " + value);

                if (value == null)
                {
                    MediaElement.Source = null;

                    lock (_lock)
                    {
                        _closeRequested = true;
                        _requestedSource = null;
                    }

                    // hack: MediaElement doesn't raise CurrentStateChanged on its own
                    if (CurrentStateChanged != null)
                        CurrentStateChanged(this, new RoutedEventArgs());
                }
                else
                {
                    lock (_lock)
                    {
                        _closeRequested = false;
                        _requestedSource = value;
                    }
                }

                _workerTask.Fire();
            }
        }

        async Task SetMediaSourceAsync(Uri source)
        {
            await OpenMediaAsync(source).ConfigureAwait(true);

            await SetSourceAsync(_tsMediaStreamSource).ConfigureAwait(false);

            var task = StartPlaybackWrapperAsync();

            TaskCollector.Default.Add(task, "SetMediaSourceAsync: StartPlaybackWrapperAsync()");
        }

        public Task SetSourceAsync(MediaStreamSource mediaStreamSource)
        {
            return Dispatcher.DispatchAsync(() => SetSource(mediaStreamSource));
        }

        /// <inheritdoc />
        public Stretch Stretch
        {
            get { return MediaElement.Stretch; }
            set { MediaElement.Stretch = value; }
        }

        /// <inheritdoc />
        public double Volume
        {
            get { return MediaElement.Volume; }
            set { MediaElement.Volume = value; }
        }

        #endregion

        #region Position

        /// <summary>
        ///     Identifies the Position dependency property.
        /// </summary>
        public static readonly DependencyProperty PositionProperty = DependencyProperty.Register("Position", typeof(TimeSpan), typeof(MediaElementWrapper), new PropertyMetadata(TimeSpan.Zero));

        #endregion

        /// <inheritdoc />
        public override void OnApplyTemplate()
        {
            Debug.WriteLine("MediaElementWrapper.OnApplyTemplate()");

            base.OnApplyTemplate();
            _templateAppliedTaskSource.TrySetResult(null);
        }

        void mediaElement_LogReady(object sender, LogReadyRoutedEventArgs e)
        {
            Debug.WriteLine("MediaElementWrapper.mediaElement_LogReady()");

            if (LogReady != null) LogReady(this, new Microsoft.PlayerFramework.LogReadyRoutedEventArgs(e.Log, e.LogSource));
        }

        void mediaElement_CurrentStateChanged(object sender, RoutedEventArgs e)
        {
#if DEBUG
            var state = "<unknown>";

            if (null != MediaElement && MediaElement.Dispatcher.CheckAccess())
                state = MediaElement.CurrentState.ToString();

            Debug.WriteLine("MediaElementWrapper.mediaElement_CurrentStateChanged(): " + state);
#endif

            if (CurrentStateChanged != null) CurrentStateChanged(sender, e);
        }

#if WINDOWS_PHONE
        void MediaElement_MarkerReached(object sender, TimelineMarkerRoutedEventArgs e)
        {
            if (MarkerReached != null)
            {
                MarkerReached(this, new Microsoft.PlayerFramework.TimelineMarkerRoutedEventArgs
                                    {
                                        Marker = e.Marker
                                    });
            }
        }
#else
    /// <inheritdoc /> 
        public event TimelineMarkerRoutedEventHandler MarkerReached
        {
            add
            {
                MediaElement.MarkerReached += value;
            }
            remove
            {
                MediaElement.MarkerReached -= value;
            }
        }
#endif

        void MediaElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("MediaElementWrapper.MediaElement_MediaEnded()");

            Close();
        }

        void MediaElement_MediaFailed(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("MediaElementWrapper.MediaElement_MediaFailed()");

            Close();
        }

        async Task StartPlaybackWrapperAsync()
        {
            Debug.WriteLine("MediaElementWrapper.StartPlaybackWrapperAsync()");

            try
            {
                await StartPlaybackAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Send a "Failed" message here?
                Debug.WriteLine("MediaElementWrapper.StartPlaybackWrapperAsync() failed: " + ex.Message);
            }
        }

        async Task StartPlaybackAsync()
        {
            Debug.WriteLine("MediaElementWrapper.StartPlaybackAsync()");

            if (null == _source)
                return;

            var isDoneTask = Dispatcher.DispatchAsync(
                () =>
                {
                    var state = MediaElement.CurrentState;

                    if (MediaElementState.Closed != state && MediaElementState.Opening != state)
                    {
                        if (new[] { MediaElementState.Paused, MediaElementState.Stopped }.Contains(state))
                            MediaElement.Play();

                        return true;
                    }

                    return false;
                });

            if (await isDoneTask.ConfigureAwait(false))
                return;

            if (null == _tsMediaManager || null == _tsMediaStreamSource || TsMediaManager.MediaState.Error == _tsMediaManager.State || TsMediaManager.MediaState.Closed == _tsMediaManager.State)
            {
                if (null == _source)
                    return;

                await OpenMediaAsync(_source).ConfigureAwait(false);

                Debug.Assert(null != _tsMediaManager);
            }

            _tsMediaManager.Play();
        }

        async Task OpenMediaAsync(Uri source)
        {
            Debug.WriteLine("MediaElementWrapper.OpenMediaAsync()");

            if (null != _tsMediaStreamSource)
                await CloseMediaAsync().ConfigureAwait(false);

            Debug.Assert(null == _tsMediaStreamSource);
            Debug.Assert(null == _tsMediaManager);
            Debug.Assert(null == _playlist);

            if (null == source)
                return;

            if (!source.IsAbsoluteUri)
            {
                Debug.WriteLine("MediaElementWrapper.OpenMediaAsync() source is not absolute: " + source);
                return;
            }

            _source = source;

            _playlist = await _segmentManagerFactory.CreateDefaultAsync(source, DefaultSourceType).ConfigureAwait(false);

            var segmentReaderManager = new SegmentReaderManager(new[] { _playlist }, _httpClients.CreateSegmentClient);

            _tsMediaStreamSource = new TsMediaStreamSource();

            var mediaManagerParameters = new MediaManagerParameters
                                         {
                                             SegmentReaderManager = segmentReaderManager,
                                             MediaStreamSource = _tsMediaStreamSource
                                         };

            _tsMediaManager = new TsMediaManager(mediaManagerParameters);

            _tsMediaManager.OnStateChange += TsMediaManagerOnStateChange;
        }

        async Task CloseMediaAsync()
        {
            Debug.WriteLine("MediaElementWrapper.CloseMediaAsync()");

            var mediaManager = _tsMediaManager;

            if (null != mediaManager)
            {
                try
                {
                    //Debug.WriteLine("MediaElementWrapper.CloseMediaAsync() calling mediaManager.CloseAsync()");

                    //var stopwatch = Stopwatch.StartNew();

                    await mediaManager.CloseAsync().ConfigureAwait(false);

                    //stopwatch.Stop();

                    //Debug.WriteLine("MediaElementWrapper.CloseMediaAsync() returned from mediaManager.CloseAsync() after " + stopwatch.Elapsed);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Media manager close failed: " + ex.Message);
                }

                mediaManager.OnStateChange -= TsMediaManagerOnStateChange;

                _tsMediaManager = null;
            }

            var playlist = _playlist;

            if (null != playlist)
            {
                _playlist = null;

                playlist.CleanupBackground("MediaElementWrapper.CloseMediaAsync playlist");
            }

            var tsMediaStreamSource = _tsMediaStreamSource;

            if (null != tsMediaStreamSource)
            {
                _tsMediaStreamSource = null;

                tsMediaStreamSource.DisposeBackground("MediaElementWrapper.CloseMediaAsync tsMediaStreamSource");
            }

            if (null != mediaManager)
                mediaManager.DisposeBackground("MediaElementWrapper.CloseMediaAsync mediaManager");

            _source = null;

            Debug.WriteLine("MediaElementWrapper.CloseMediaAsync() completed");
        }

        void TsMediaManagerOnStateChange(object sender, TsMediaManagerStateEventArgs e)
        {
            Debug.WriteLine("MediaElementWrapper.TsMediaManagerOnOnStateChange() to {0}: {1}", e.State, e.Message);
        }

        public void Close()
        {
            Debug.WriteLine("MediaElementWrapper.Close()");

            Debug.Assert(Dispatcher.CheckAccess());

            MediaElement.Source = null;

            lock (_lock)
            {
                _closeRequested = true;
                _requestedSource = null;
            }

            _workerTask.Fire();
        }

        public void Cleanup()
        {
            Debug.WriteLine("MediaElementWrapper.Cleanup()");

            Close();

            if (null != _tsMediaManager)
                _tsMediaManager.OnStateChange -= TsMediaManagerOnStateChange;

            var playlist = _playlist;

            if (null != playlist)
            {
                _playlist = null;

                playlist.CleanupBackground("MediaElementWrapper.Cleanup() playlist.CloseAsync()");
            }

            _workerTask.WaitAsync().Wait();
        }
    }
}

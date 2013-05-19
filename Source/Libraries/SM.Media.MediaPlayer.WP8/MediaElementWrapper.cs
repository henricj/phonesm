// -----------------------------------------------------------------------
//  <copyright file="MediaElementWrapper.cs" company="Henric Jungheim">
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.PlayerFramework;
using SM.Media.Playlists;
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
    ///     IMediaElement is used to allow the SmoothStreamingMediaElement or other custom MediaElements to be used by MediaPlayer
    /// </summary>
    /// <remarks>
    ///     This code is based on
    ///     https://playerframework.codeplex.com/SourceControl/latest#Phone.SL/Controls/MediaElementWrapper.cs
    /// </remarks>
    public class MediaElementWrapper : ContentControl, IMediaElement
    {
        readonly IHttpClients _httpClients;
        // This class is 
        readonly TaskCompletionSource<object> _templateAppliedTaskSource;

        /// <summary>
        ///     The MediaElement being wrapped
        /// </summary>
        MediaElement _mediaElement;

        /// <summary>
        ///     Creates a new instance of the MediaElementWrapper class.
        /// </summary>
        /// <param name="httpClients"></param>
        public MediaElementWrapper(IHttpClients httpClients)
        {
            if (httpClients == null)
                throw new ArgumentNullException("httpClients");

            _httpClients = httpClients;

            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            VerticalContentAlignment = VerticalAlignment.Stretch;
            MediaElement = new MediaElement();
            MediaElement.MediaEnded += MediaElement_MediaEnded;
            MediaElement.MediaFailed += MediaElement_MediaFailed;
#if WINDOWS_PHONE
            MediaElement.MarkerReached += MediaElement_MarkerReached;
#endif
            Content = MediaElement;
            _templateAppliedTaskSource = new TaskCompletionSource<object>();
        }

        /// <summary>
        ///     The underlying MediaElement being wrapped
        /// </summary>
        protected MediaElement MediaElement
        {
            get { return _mediaElement; }
            private set
            {
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
            MediaElement.Pause();
        }

        /// <inheritdoc />
        public void Play()
        {
            var task = StartPlaybackWrapperAsync();

            // We do not wait for the task to complete.
        }

        /// <inheritdoc />
        public void RequestLog()
        {
            MediaElement.RequestLog();
        }

        /// <inheritdoc />
        public void SetSource(Stream stream)
        {
            MediaElement.SetSource(stream);
        }

        /// <inheritdoc />
        public void SetSource(MediaStreamSource mediaStreamSource)
        {
            MediaElement.SetSource(mediaStreamSource);
        }

        /// <inheritdoc />
        public void Stop()
        {
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
            get { return null == _programManager ? null : _programManager.Playlists.FirstOrDefault(); }
            set
            {
                if (value == null) // hack: MediaElement doesn't raise CurrentStateChanged on its own
                {
                    Cleanup();

                    if (CurrentStateChanged != null)
                        CurrentStateChanged(this, new RoutedEventArgs());
                }
                else
                {
                    var task = SetMediaSourceAsync(value);

                    // Do not wait for the task to complete.
                }
            }
        }

        async Task SetMediaSourceAsync(Uri value)
        {
            _programManager = new ProgramManager(_httpClients.RootPlaylistClient)
                              {
                                  Playlists = new[] { value }
                              };

            if (null != _tsMediaStreamSource)
            {
                // Implement cleanup
                Debug.Assert(true);
            }

            await OpenMediaAsync();

            SetSource(_tsMediaStreamSource);

            var task = StartPlaybackWrapperAsync();
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

        IMediaElementManager _mediaElementManager;
        ProgramManager _programManager;
        TsMediaManager _tsMediaManager;
        TsMediaStreamSource _tsMediaStreamSource;

        #endregion

        /// <inheritdoc />
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _templateAppliedTaskSource.TrySetResult(null);
        }

        void mediaElement_LogReady(object sender, LogReadyRoutedEventArgs e)
        {
            if (LogReady != null) LogReady(this, new Microsoft.PlayerFramework.LogReadyRoutedEventArgs(e.Log, e.LogSource));
        }

        void mediaElement_CurrentStateChanged(object sender, RoutedEventArgs e)
        {
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
            Debug.WriteLine("MediaElementWrapper.MediaElement_MediaEnded");

            Close();
        }

        void MediaElement_MediaFailed(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("MediaElementWrapper.MediaElement_MediaFailed");

            Close();
        }

        async Task StartPlaybackWrapperAsync()
        {
            try
            {
                await StartPlaybackAsync();
            }
            catch (Exception ex)
            {
                // Send a "Failed" message here?
                Debug.WriteLine("MediaElementWrapper.StartPlaybackWrapperAsync failed: " + ex.Message);
            }
        }

        async Task StartPlaybackAsync()
        {
            Debug.WriteLine("MediaElementWrapper.StartPlaybackAsync");

            if (null == _programManager)
                return;

            var state = MediaElement.CurrentState;

            if (MediaElementState.Closed != state && MediaElementState.Opening != state)
            {
                if (new[] { MediaElementState.Paused, MediaElementState.Stopped }.Contains(state))
                    MediaElement.Play();

                return;
            }

            await OpenMediaAsync();

            _tsMediaManager.Play();
        }

        async Task OpenMediaAsync()
        {
            Program program;
            ISubProgram subProgram;

            var programs = await _programManager.LoadAsync().ConfigureAwait(false);

            program = programs.Values.FirstOrDefault();

            if (null == program)
            {
                Debug.WriteLine("MediaElementWrapper.SetMediaSource: program not found");
                throw new FileNotFoundException("Unable to load program");
            }

            subProgram = program.SubPrograms.FirstOrDefault();

            if (null == subProgram)
            {
                Debug.WriteLine("MediaElementWrapper.SetMediaSource: no sub programs found");
                throw new FileNotFoundException("Unable to load program stream");
            }

            var playlist = new PlaylistSegmentManager(uri => new CachedWebRequest(uri, _httpClients.GetPlaylistClient(uri)), subProgram);

            _mediaElementManager = new NoOpMediaElementManager();

            var segmentReaderManager = new SegmentReaderManager(new[] { playlist }, _httpClients.GetSegmentClient);

            if (null != _tsMediaManager)
                _tsMediaManager.OnStateChange -= TsMediaManagerOnOnStateChange;

            if (null == _tsMediaStreamSource)
                _tsMediaStreamSource = new TsMediaStreamSource();

            _tsMediaManager = new TsMediaManager(segmentReaderManager, _mediaElementManager, _tsMediaStreamSource);

            _tsMediaManager.OnStateChange += TsMediaManagerOnOnStateChange;
        }

        void TsMediaManagerOnOnStateChange(object sender, TsMediaManagerStateEventArgs e)
        {
            Debug.WriteLine("MediaElementWrapper.TsMediaManagerOnOnStateChange to {0}: {1}", e.State, e.Message);
        }

        public void Close()
        {
            if (null == _tsMediaManager)
                return;

            _tsMediaManager.Close();
        }

        public void Cleanup()
        {
            Close();

            if (null != _tsMediaManager)
                _tsMediaManager.OnStateChange -= TsMediaManagerOnOnStateChange;

            if (null != _mediaElementManager)
            {
                _mediaElementManager.Close()
                                    .Wait();
            }
        }
    }
}

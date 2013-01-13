// -----------------------------------------------------------------------
//  <copyright file="MediaElementWrapper.cs" company="Henric Jungheim">
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.PlayerFramework;
using SM.Media.Playlists;
using SM.Media.Segments;
using SM.Media.Utility;
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
    public class MediaElementWrapper : ContentControl, IMediaElement
    {
        readonly TaskCompletionSource<object> _templateAppliedTaskSource;

        /// <summary>
        ///     The MediaElement being wrapped
        /// </summary>
        MediaElement _mediaElement;

        /// <summary>
        ///     Creates a new instance of the MediaElementWrapper class.
        /// </summary>
        public MediaElementWrapper()
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            VerticalContentAlignment = VerticalAlignment.Stretch;
            MediaElement = new MediaElement();
            MediaElement.MarkerReached += MediaElement_MarkerReached;
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
                }

                _mediaElement = value;

                if (_mediaElement != null)
                {
                    _mediaElement.CurrentStateChanged += mediaElement_CurrentStateChanged;
                    _mediaElement.LogReady += mediaElement_LogReady;
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


        /// <inheritdoc />
        public event TimelineMarkerRoutedEventHandler MarkerReached;


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

        /// <inheritdoc />
        public void Pause()
        {
            MediaElement.Pause();
        }

        /// <inheritdoc />
        public void Play()
        {
            MediaElement.Play();
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
            get { return MediaElement.Source; }
            set
            {
                var task = SetMediaSource(value);

                if (value == null) // hack: MediaElement doesn't raise CurrentStateChanged on its own
                {
                    if (CurrentStateChanged != null) CurrentStateChanged(this, new RoutedEventArgs());
                }
            }
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

        MediaElementManager _mediaElementManager;
        TsMediaManager _tsMediaManager;

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

        void MediaElement_MarkerReached(object sender, TimelineMarkerRoutedEventArgs e)
        {
            if (MarkerReached != null) MarkerReached(this, new Microsoft.PlayerFramework.TimelineMarkerRoutedEventArgs { Marker = e.Marker });
        }

        async Task SetMediaSource(Uri source)
        {
            var programManager = new ProgramManager { Playlists = new[] { source } };

            Program program;
            ISubProgram subProgram;

            try
            {
                var programs = await programManager.LoadAsync();

                program = programs.Values.FirstOrDefault();

                if (null == program)
                {
                    Debug.WriteLine("MediaElementWrapper.SetMediaSource: program not found");
                    return;
                }

                subProgram = program.SubPrograms.FirstOrDefault();

                if (null == subProgram)
                {
                    Debug.WriteLine("MediaElementWrapper.SetMediaSource: no sub programs found");
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MediaElementWrapper.SetMediaSource: " + ex.Message);
                return;
            }

            var webRequestFactory = new HttpWebRequestFactory(program.Url);

            var playlist = new PlaylistSegmentManager(uri => new CachedWebRequest(uri, webRequestFactory.Create), subProgram);

            _mediaElementManager = new MediaElementManager(Dispatcher,
                                                           () => MediaElement,
                                                           me => { });

            var segmentReaderManager = new SegmentReaderManager(new[] { playlist }, webRequestFactory.CreateChildFactory(playlist.Url));

            _tsMediaManager = new TsMediaManager(segmentReaderManager, _mediaElementManager, mm => new TsMediaStreamSource(mm));

            _tsMediaManager.Play(segmentReaderManager);
        }
    }
}

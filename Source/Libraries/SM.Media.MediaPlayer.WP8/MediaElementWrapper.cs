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
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.PlayerFramework;
using SM.Media.Utility;
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
    public sealed class MediaElementWrapper : ContentControl, IMediaElement
    {
        readonly IMediaStreamFacade _mediaStreamFacade;
        readonly TaskCompletionSource<object> _templateAppliedTaskSource;
        Uri _source;

        /// <summary>
        ///     The MediaElement being wrapped
        /// </summary>
        MediaElement _mediaElement;

        /// <summary>
        ///     Creates a new instance of the MediaElementWrapper class.
        /// </summary>
        /// <param name="mediaStreamFacade"></param>
        public MediaElementWrapper(IMediaStreamFacade mediaStreamFacade)
        {
            if (mediaStreamFacade == null)
                throw new ArgumentNullException("mediaStreamFacade");

            Debug.WriteLine("MediaElementWrapper.ctor()");

            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            VerticalContentAlignment = VerticalAlignment.Stretch;

            _mediaStreamFacade = mediaStreamFacade;

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
        public MediaElement MediaElement
        {
            get { return _mediaElement; }
            set
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

            MediaElement.Play();
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

            if (null != mediaStreamSource)
                MediaElement.SetSource(mediaStreamSource);
            else
                MediaElement.Source = null;
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
            set
            {
#if WINDOWS_PHONE7
    // Setting WP7's MediaElement.Position always seeks to 0.
                _mediaStreamFacade.SeekTarget = value;
#endif
                MediaElement.Position = value;
            }
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

                _source = value;

                if (value == null)
                {
                    MediaElement.Source = null;

                    // hack: MediaElement doesn't raise CurrentStateChanged on its own
                    if (CurrentStateChanged != null)
                        CurrentStateChanged(this, new RoutedEventArgs());
                }

                var t = SetSourceAsync();

                TaskCollector.Default.Add(t, "MediaElementWrapper.Source setter");
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

        async Task SetSourceAsync()
        {
            Debug.WriteLine("MediaElementWrapper.SetSourceAsync()");

            var mss = await _mediaStreamFacade.CreateMediaStreamSourceAsync(_source, CancellationToken.None).ConfigureAwait(false);

            // Capturing the context would be pointless since we might
            // not be on the UI thread to begin with.
            if (Dispatcher.CheckAccess())
                SetSource(mss);
            else
                await Dispatcher.DispatchAsync(() => SetSource(mss));
        }

        public void Cleanup()
        {
            Debug.WriteLine("MediaElementWrapper.Cleanup()");

            Close();

            _mediaStreamFacade.CloseAsync().Wait();

            _mediaStreamFacade.DisposeSafe();
        }

        public void Close()
        {
            Debug.WriteLine("MediaElementWrapper.Close()");

            Debug.Assert(Dispatcher.CheckAccess());

            MediaElement.Source = null;

            _mediaStreamFacade.RequestStop();
        }
    }
}

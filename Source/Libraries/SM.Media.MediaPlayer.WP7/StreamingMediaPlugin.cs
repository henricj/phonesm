// -----------------------------------------------------------------------
//  <copyright file="StreamingMediaPlugin.cs" company="Henric Jungheim">
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
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.SilverlightMediaFramework.Plugins;
using Microsoft.SilverlightMediaFramework.Plugins.Metadata;
using Microsoft.SilverlightMediaFramework.Plugins.Primitives;
using Microsoft.SilverlightMediaFramework.Utilities.Extensions;
using SM.Media.Utility;
using SM.Media.Web;

namespace SM.Media.MediaPlayer
{
    /// <summary>
    ///     Represents a media plug-in that can play streaming media.
    /// </summary>
    [ExportMediaPlugin(PluginName = PluginName,
        PluginDescription = PluginDescription,
        PluginVersion = PluginVersion,
        SupportedDeliveryMethods = SupportedDeliveryMethodsInternal,
        SupportedMediaTypes = new[] { "application/vnd.apple.mpegurl" })]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class StreamingMediaPlugin : IMediaPlugin
    {
        const string PluginName = "StreamingMediaPlugin";

        const string PluginDescription =
            "Provides HTTP Live Streaming capabilities for the Silverlight Media Framework by wrapping the MediaElement.";

        const string PluginVersion = "1.1.0.0";

        const DeliveryMethods SupportedDeliveryMethodsInternal = DeliveryMethods.Streaming;

        const double SupportedPlaybackRate = 1;
        static readonly double[] SupportedRates = { SupportedPlaybackRate };

        protected MediaElement MediaElement { get; set; }

        Stream _streamSource;
        HttpClients _httpClients;
        readonly IApplicationInformation _applicationInformation = ApplicationInformationFactory.Default;
        MediaStreamFascade _mediaStreamFascade;
        Dispatcher _dispatcher;

        #region Events

        /// <summary>
        ///     Occurs when the plug-in is loaded.
        /// </summary>
        public event Action<IPlugin> PluginLoaded;

        /// <summary>
        ///     Occurs when the plug-in is unloaded.
        /// </summary>
        public event Action<IPlugin> PluginUnloaded;

        /// <summary>
        ///     Occurs when an exception occurs when the plug-in is loaded.
        /// </summary>
        public event Action<IPlugin, Exception> PluginLoadFailed;

        /// <summary>
        ///     Occurs when an exception occurs when the plug-in is unloaded.
        /// </summary>
        public event Action<IPlugin, Exception> PluginUnloadFailed;

        /// <summary>
        ///     Occurs when the log is ready.
        /// </summary>
        public event Action<IPlugin, LogEntry> LogReady;

        //IMediaPlugin Events

        /// <summary>
        ///     Occurs when a seek operation has completed.
        /// </summary>
        public event Action<IMediaPlugin> SeekCompleted;

        /// <summary>
        ///     Occurs when the percent of the media being buffered changes.
        /// </summary>
        public event Action<IMediaPlugin, double> BufferingProgressChanged;

        /// <summary>
        ///     Occurs when the percent of the media downloaded changes.
        /// </summary>
        public event Action<IMediaPlugin, double> DownloadProgressChanged;

        /// <summary>
        ///     Occurs when a marker defined for the media file has been reached.
        /// </summary>
        public event Action<IMediaPlugin, MediaMarker> MarkerReached;

        /// <summary>
        ///     Occurs when the media reaches the end.
        /// </summary>
        public event Action<IMediaPlugin> MediaEnded;

        /// <summary>
        ///     Occurs when the media does not open successfully.
        /// </summary>
        public event Action<IMediaPlugin, Exception> MediaFailed;

        /// <summary>
        ///     Occurs when the media successfully opens.
        /// </summary>
        public event Action<IMediaPlugin> MediaOpened;

        /// <summary>
        ///     Occurs when the state of playback for the media changes.
        /// </summary>
        public event Action<IMediaPlugin, MediaPluginState> CurrentStateChanged;

#pragma warning disable 67
        /// <summary>
        ///     Occurs when the user clicks on an ad.
        /// </summary>
        public event Action<IAdaptiveMediaPlugin, IAdContext> AdClickThrough;

        /// <summary>
        ///     Occurs when there is an error playing an ad.
        /// </summary>
        public event Action<IAdaptiveMediaPlugin, IAdContext> AdError;

        /// <summary>
        ///     Occurs when the progress of the currently playing ad has been updated.
        /// </summary>
        public event Action<IAdaptiveMediaPlugin, IAdContext, AdProgress> AdProgressUpdated;

        /// <summary>
        ///     Occurs when the state of the currently playing ad has changed.
        /// </summary>
        public event Action<IAdaptiveMediaPlugin, IAdContext> AdStateChanged;

        /// <summary>
        ///     Occurs when the media's playback rate changes.
        /// </summary>
        public event Action<IMediaPlugin> PlaybackRateChanged;
#pragma warning restore 67

        #endregion

        #region Properties

        public CacheMode CacheMode
        {
            get { return MediaElement != null ? MediaElement.CacheMode : null; }
            set { MediaElement.IfNotNull(i => i.CacheMode = value); }
        }

        /// <summary>
        ///     Gets a value indicating whether a plug-in is currently loaded.
        /// </summary>
        public bool IsLoaded { get; private set; }

        /// <summary>
        ///     Gets or sets a value that indicates whether the media file starts to play immediately after it is opened.
        /// </summary>
        public bool AutoPlay
        {
            get { return MediaElement != null && MediaElement.AutoPlay; }
            set { MediaElement.IfNotNull(i => i.AutoPlay = value); }
        }

        /// <summary>
        ///     Gets or sets the ratio of the volume level across stereo speakers.
        /// </summary>
        /// <remarks>
        ///     The value is in the range between -1 and 1. The default value of 0 signifies an equal volume between left and right
        ///     stereo speakers.
        ///     A value of -1 represents 100 percent volume in the speakers on the left, and a value of 1 represents 100 percent
        ///     volume in the speakers on the right.
        /// </remarks>
        public double Balance
        {
            get { return MediaElement != null ? MediaElement.Balance : default(double); }
            set { MediaElement.IfNotNull(i => i.Balance = value); }
        }

        /// <summary>
        ///     Gets a value indicating if the current media item can be paused.
        /// </summary>
        public bool CanPause
        {
            get { return MediaElement != null && MediaElement.CanPause; }
        }

        /// <summary>
        ///     Gets a value indicating if the current media item allows seeking to a play position.
        /// </summary>
        public bool CanSeek
        {
            get { return MediaElement != null && MediaElement.CanSeek; }
        }

        /// <summary>
        ///     Gets the total time of the current media item.
        /// </summary>
        public TimeSpan Duration
        {
            get
            {
                return MediaElement != null && MediaElement.NaturalDuration.HasTimeSpan
                    ? MediaElement.NaturalDuration.TimeSpan
                    : TimeSpan.Zero;
            }
        }

        /// <summary>
        ///     Gets the end time of the current media item.
        /// </summary>
        public TimeSpan EndPosition
        {
            get { return Duration; }
        }

        /// <summary>
        ///     Gets or sets a value indicating if the current media item is muted so that no audio is playing.
        /// </summary>
        public bool IsMuted
        {
            get { return MediaElement != null && MediaElement.IsMuted; }
            set { MediaElement.IfNotNull(i => i.IsMuted = value); }
        }

        /// <summary>
        ///     Gets or sets the LicenseAcquirer associated with the IMediaPlugin.
        ///     The LicenseAcquirer handles acquiring licenses for DRM encrypted content.
        /// </summary>
        public LicenseAcquirer LicenseAcquirer
        {
            get
            {
                return MediaElement != null
                    ? MediaElement.LicenseAcquirer
                    : null;
            }
            set { MediaElement.IfNotNull(i => i.LicenseAcquirer = value); }
        }

        /// <summary>
        ///     Gets the size value (unscaled width and height) of the current media item.
        /// </summary>
        public Size NaturalVideoSize
        {
            get
            {
                return MediaElement != null
                    ? new Size(MediaElement.NaturalVideoWidth, MediaElement.NaturalVideoHeight)
                    : Size.Empty;
            }
        }

        /// <summary>
        ///     Gets the play speed of the current media item.
        /// </summary>
        /// <remarks>
        ///     A rate of 1.0 is normal speed.
        /// </remarks>
        public double PlaybackRate
        {
            get { return SupportedPlaybackRate; }
            set
            {
                if (value != SupportedPlaybackRate)
                    throw new ArgumentOutOfRangeException("value");
            }
        }

        /// <summary>
        ///     Gets the current state of the media item.
        /// </summary>
        public MediaPluginState CurrentState
        {
            get
            {
                return MediaElement != null
                    ? ConvertToPlayState(MediaElement.CurrentState)
                    : MediaPluginState.Stopped;
            }
        }

        /// <summary>
        ///     Gets the current position of the media item.
        /// </summary>
        public TimeSpan Position
        {
            get
            {
                return MediaElement != null
                    ? MediaElement.Position
                    : TimeSpan.Zero;
            }
            set
            {
                if (MediaElement != null)
                {
                    _mediaStreamFascade.SeekTarget = value;
                    MediaElement.Position = value;
                    SeekCompleted.IfNotNull(i => i(this));
                }
            }
        }

        /// <summary>
        ///     Gets whether this plugin supports ad scheduling.
        /// </summary>
        public bool SupportsAdScheduling
        {
            get { return false; }
        }

        /// <summary>
        ///     Gets the start position of the current media item (0).
        /// </summary>
        public TimeSpan StartPosition
        {
            get { return TimeSpan.Zero; }
        }

        /// <summary>
        ///     Gets the stretch setting for the current media item.
        /// </summary>
        public Stretch Stretch
        {
            get
            {
                return MediaElement != null
                    ? MediaElement.Stretch
                    : default(Stretch);
            }
            set { MediaElement.IfNotNull(i => i.Stretch = value); }
        }

        /// <summary>
        ///     Gets or sets a Boolean value indicating whether to
        ///     enable GPU acceleration.  In the case of the Progressive
        ///     MediaElement, the CacheMode being set to BitmapCache
        ///     is the equivalent of setting EnableGPUAcceleration = true
        /// </summary>
        public bool EnableGPUAcceleration
        {
            get { return MediaElement != null && MediaElement.CacheMode is BitmapCache; }
            set
            {
                if (value)
                    MediaElement.IfNotNull(i => i.CacheMode = new BitmapCache());
                else
                    MediaElement.IfNotNull(i => i.CacheMode = null);
            }
        }

        /// <summary>
        ///     Gets the delivery methods supported by this plugin.
        /// </summary>
        public DeliveryMethods SupportedDeliveryMethods
        {
            get { return SupportedDeliveryMethodsInternal; }
        }

        /// <summary>
        ///     Gets a collection of the playback rates for the current media item.
        /// </summary>
        public IEnumerable<double> SupportedPlaybackRates
        {
            get { return SupportedRates; }
        }

        /// <summary>
        ///     Gets a reference to the media player control.
        /// </summary>
        public FrameworkElement VisualElement
        {
            get { return MediaElement; }
        }

        /// <summary>
        ///     Gets or sets the initial volume setting as a value between 0 and 1.
        /// </summary>
        public double Volume
        {
            get
            {
                return MediaElement != null
                    ? MediaElement.Volume
                    : 0;
            }
            set { MediaElement.IfNotNull(i => i.Volume = value); }
        }

        /// <summary>
        ///     Gets the dropped frames per second.
        /// </summary>
        public double DroppedFramesPerSecond
        {
            get
            {
                return MediaElement != null
                    ? MediaElement.DroppedFramesPerSecond
                    : 0;
            }
        }

        /// <summary>
        ///     Gets the rendered frames per second.
        /// </summary>
        public double RenderedFramesPerSecond
        {
            get
            {
                return MediaElement != null
                    ? MediaElement.RenderedFramesPerSecond
                    : 0;
            }
        }

        /// <summary>
        ///     Gets the percentage of the current buffering that is completed.
        /// </summary>
        public double BufferingProgress
        {
            get
            {
                return MediaElement != null
                    ? MediaElement.BufferingProgress
                    : default(double);
            }
        }

        /// <summary>
        ///     Gets or sets the amount of time for the current buffering action.
        /// </summary>
        public TimeSpan BufferingTime
        {
            get
            {
                return MediaElement != null
                    ? MediaElement.BufferingTime
                    : TimeSpan.Zero;
            }
            set { MediaElement.IfNotNull(i => i.BufferingTime = value); }
        }

        /// <summary>
        ///     Gets the percentage of the current buffering that is completed
        /// </summary>
        public double DownloadProgress
        {
            get { return MediaElement.DownloadProgress; }
        }

        /// <summary>
        ///     Gets the download progress offset
        /// </summary>
        public double DownloadProgressOffset
        {
            get { return MediaElement.DownloadProgressOffset; }
        }

        /// <summary>
        ///     Gets or sets the location of the media file.
        /// </summary>
        public Uri Source
        {
            get { return null == _mediaStreamFascade ? null : _mediaStreamFascade.Source; }
            set
            {
                Debug.WriteLine("StreamingMediaPlugin.Source setter: " + value);

                if (null == _mediaStreamFascade)
                    return;

                _mediaStreamFascade.Source = value;

                _mediaStreamFascade.Play();
            }
        }

        public Stream StreamSource
        {
            get { return _streamSource; }

            set
            {
                Debug.WriteLine("StreamingMediaPlugin.StreamSource setter");

                if (MediaElement != null)
                {
                    MediaElement.SetSource(value);
                    _streamSource = value;
                }
            }
        }

        #endregion

        /// <summary>
        ///     Starts playing the current media file from its current position.
        /// </summary>
        public void Play()
        {
            Debug.WriteLine("StreamingMediaPlugin.Play()");

            StartPlayback();
        }

        /// <summary>
        ///     Pauses the currently playing media.
        /// </summary>
        public void Pause()
        {
            Debug.WriteLine("StreamingMediaPlugin.Pause()");

            MediaElement.IfNotNull(i => i.Pause());
        }

        /// <summary>
        ///     Stops playing the current media.
        /// </summary>
        public void Stop()
        {
            Debug.WriteLine("StreamingMediaPlugin.Stop()");

            MediaElement.IfNotNull(i => i.Stop());
        }

        /// <summary>
        ///     Loads a plug-in for playing streaming media.
        /// </summary>
        public void Load()
        {
            try
            {
                if (null != _httpClients)
                    _httpClients.Dispose();

                _httpClients = new HttpClients(userAgent: _applicationInformation.CreateUserAgent());


                InitializeStreamingMediaElement();
                IsLoaded = true;
                PluginLoaded.IfNotNull(i => i(this));
                //SendLogEntry(KnownLogEntryTypes.ProgressiveMediaPluginLoaded, message: ProgressiveMediaPluginResources.ProgressiveMediaPluginLoadedLogMessage);

                _dispatcher = MediaElement.Dispatcher;

                _mediaStreamFascade = MediaStreamFascadeSettings.Parameters.Create(_httpClients, SetSourceAsync);
            }
            catch (Exception ex)
            {
                PluginLoadFailed.IfNotNull(i => i(this, ex));
            }
        }

        /// <summary>
        ///     Unloads a plug-in for streaming media.
        /// </summary>
        public void Unload()
        {
            try
            {
                IsLoaded = false;

                if (null != _mediaStreamFascade)
                {
                    _mediaStreamFascade.DisposeSafe();
                    _mediaStreamFascade = null;
                }

                if (null != _httpClients)
                {
                    _httpClients.Dispose();
                    _httpClients = null;
                }

                DestroyStreamingMediaElement();
                PluginUnloaded.IfNotNull(i => i(this));
                //SendLogEntry(KnownLogEntryTypes.ProgressiveMediaPluginUnloaded, message: ProgressiveMediaPluginResources.ProgressiveMediaPluginUnloadedLogMessage);
            }
            catch (Exception ex)
            {
                PluginUnloadFailed.IfNotNull(i => i(this, ex));
            }
        }

        /// <summary>
        ///     Requests that this plugin generate a LogEntry via the LogReady event
        /// </summary>
        public void RequestLog()
        {
            MediaElement.IfNotNull(i => i.RequestLog());
        }

        /// <summary>
        ///     Schedules an ad to be played by this plugin.
        /// </summary>
        /// <param name="adSource">The source of the ad content.</param>
        /// <param name="deliveryMethod">The delivery method of the ad content.</param>
        /// <param name="duration">
        ///     The duration of the ad content that should be played.  If omitted the plugin will play the full
        ///     duration of the ad content.
        /// </param>
        /// <param name="startTime">
        ///     The position within the media where this ad should be played.  If omitted ad will begin playing
        ///     immediately.
        /// </param>
        /// <param name="clickThrough">The URL where the user should be directed when they click the ad.</param>
        /// <param name="pauseTimeline">
        ///     Indicates if the timeline of the currently playing media should be paused while the ad is
        ///     playing.
        /// </param>
        /// <param name="appendToAd">
        ///     Another scheduled ad that this ad should be appended to.  If omitted this ad will be scheduled
        ///     independently.
        /// </param>
        /// <param name="data">User data.</param>
        /// <returns>A reference to the IAdContext that contains information about the scheduled ad.</returns>
        public IAdContext ScheduleAd(Uri adSource, DeliveryMethods deliveryMethod, TimeSpan? duration = null,
            TimeSpan? startTime = null, TimeSpan? startOffset = null, Uri clickThrough = null, bool pauseTimeline = true,
            IAdContext appendToAd = null, object data = null, bool isLinearClip = false)
        {
            throw new NotImplementedException();
        }

        void InitializeStreamingMediaElement()
        {
            if (MediaElement == null)
            {
                MediaElement = new MediaElement();
                MediaElement.MediaOpened += MediaElement_MediaOpened;
                MediaElement.MediaFailed += MediaElement_MediaFailed;
                MediaElement.MediaEnded += MediaElement_MediaEnded;
                MediaElement.CurrentStateChanged += MediaElement_CurrentStateChanged;
                MediaElement.BufferingProgressChanged += MediaElement_BufferingProgressChanged;
                MediaElement.DownloadProgressChanged += MediaElement_DownloadProgressChanged;
                MediaElement.LogReady += MediaElement_LogReady;
            }
        }

        void DestroyStreamingMediaElement()
        {
            if (MediaElement != null)
            {
                MediaElement.MediaOpened -= MediaElement_MediaOpened;
                MediaElement.MediaFailed -= MediaElement_MediaFailed;
                MediaElement.MediaEnded -= MediaElement_MediaEnded;
                MediaElement.CurrentStateChanged -= MediaElement_CurrentStateChanged;
                MediaElement.BufferingProgressChanged -= MediaElement_BufferingProgressChanged;
                MediaElement.DownloadProgressChanged -= MediaElement_DownloadProgressChanged;
                MediaElement.LogReady -= MediaElement_LogReady;
                MediaElement.Source = null;
                MediaElement = null;
            }
        }

        void MediaElement_DownloadProgressChanged(object sender, RoutedEventArgs e)
        {
            DownloadProgressChanged.IfNotNull(i => i(this, MediaElement.DownloadProgress));
        }

        void MediaElement_BufferingProgressChanged(object sender, RoutedEventArgs e)
        {
            BufferingProgressChanged.IfNotNull(i => i(this, MediaElement.BufferingProgress));
        }

        void MediaElement_MarkerReached(object sender, TimelineMarkerRoutedEventArgs e)
        {
            //var logMessage = string.Format(ProgressiveMediaPluginResources.TimelineMarkerReached, e.Marker.Time,
            //                               e.Marker.Type, e.Marker.Text);
            //SendLogEntry(KnownLogEntryTypes.MediaElementMarkerReached, message: logMessage);

            var mediaMarker = new MediaMarker
                              {
                                  Type = e.Marker.Type,
                                  Begin = e.Marker.Time,
                                  End = e.Marker.Time,
                                  Content = e.Marker.Text
                              };

            NotifyMarkerReached(mediaMarker);
        }

        void MediaElement_CurrentStateChanged(object sender, RoutedEventArgs e)
        {
            var playState = ConvertToPlayState(MediaElement.CurrentState);
            CurrentStateChanged.IfNotNull(i => i(this, playState));
        }

        void MediaElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            MediaEnded.IfNotNull(i => i(this));

            CleanupMedia();
        }

        void MediaElement_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            MediaFailed.IfNotNull(i => i(this, e.ErrorException));

            CleanupMedia();
        }

        void MediaElement_MediaOpened(object sender, RoutedEventArgs e)
        {
            MediaOpened.IfNotNull(i => i(this));
        }

        void MediaElement_LogReady(object sender, LogReadyRoutedEventArgs e)
        {
            //var message = string.Format(ProgressiveMediaPluginResources.MediaElementGeneratedLogMessageFormat,
            //                            e.LogSource);
            //var extendedProperties = new Dictionary<string, object> { { "Log", e.Log } };
            //SendLogEntry(KnownLogEntryTypes.MediaElementLogReady, LogLevel.Statistics, message, extendedProperties: extendedProperties);
        }

        void NotifyMarkerReached(MediaMarker mediaMarker)
        {
            MarkerReached.IfNotNull(i => i(this, mediaMarker));
        }

        void SendLogEntry(string type, LogLevel severity = LogLevel.Information,
            string message = null,
            DateTime? timeStamp = null,
            IEnumerable<KeyValuePair<string, object>> extendedProperties = null)
        {
            if (LogReady != null)
            {
                var logEntry = new LogEntry
                               {
                                   Type = type,
                                   Severity = severity,
                                   Message = message,
                                   SenderName = PluginName,
                                   Timestamp = timeStamp.HasValue ? timeStamp.Value : DateTime.Now
                               };

                extendedProperties.ForEach(logEntry.ExtendedProperties.Add);
                LogReady(this, logEntry);
            }
        }

        static MediaPluginState ConvertToPlayState(MediaElementState mediaElementState)
        {
            return (MediaPluginState)Enum.Parse(typeof(MediaPluginState), mediaElementState.ToString(), true);
        }

        void CleanupMedia()
        {
            Debug.WriteLine("StreamingMediaPlugin.CleanupMedia()");

            Close();

            _mediaStreamFascade.CloseAsync().Wait();

            _mediaStreamFascade.DisposeSafe();
        }

        public void Close()
        {
            Debug.WriteLine("StreamingMediaPlugin.Close()");

            Debug.Assert(_dispatcher.CheckAccess());

            MediaElement.Source = null;

            _mediaStreamFascade.RequestStop();
        }

        Task SetSourceAsync(IMediaStreamSource mediaStreamSource)
        {
            Debug.WriteLine("StreamingMediaPlugin.SetSourceAsync()");

            var mss = (MediaStreamSource)mediaStreamSource;

            return _dispatcher.InvokeAsync(() =>
                                           {
                                               if (null == MediaElement)
                                                   return;

                                               MediaElement.SetSource(mss);
                                           });
        }

        void StartPlayback()
        {
            Debug.WriteLine("StreamingMediaPlugin.StartPlayback()");

            if (null == MediaElement)
                return;

            if (null == _mediaStreamFascade)
                return;

            if (MediaElementState.Closed != MediaElement.CurrentState)
            {
                if (new[] { MediaElementState.Paused, MediaElementState.Stopped }.Contains(MediaElement.CurrentState))
                {
                    MediaElement.Play();

                    return;
                }
            }

            _mediaStreamFascade.Play();
        }
    }
}

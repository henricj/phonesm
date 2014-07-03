// -----------------------------------------------------------------------
//  <copyright file="MainPage.xaml.cs" company="Henric Jungheim">
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

//#define STREAM_SWITCHING

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Info;
using SM.Media;
using SM.Media.Utility;
using SM.Media.Web;
using SM.Media.Web.HttpClientReader;

namespace HlsView
{
    public partial class MainPage : PhoneApplicationPage
    {
#if STREAM_SWITCHING
        readonly DispatcherTimer _timer;
#endif

        static readonly TimeSpan StepSize = TimeSpan.FromMinutes(2);
        static readonly IApplicationInformation ApplicationInformation = ApplicationInformationFactory.Default;
        readonly DispatcherTimer _positionSampler;
        IMediaStreamFacade _mediaStreamFacade;
        TimeSpan _previousPosition;
        readonly IHttpClientsParameters _httpClientsParameters;
        int _track;
        readonly IList<MediaTrack> _tracks = TrackManager.Tracks;

        // Constructor
        public MainPage()
        {
            InitializeComponent();

            _httpClientsParameters = new HttpClientsParameters { UserAgent = ApplicationInformation.CreateUserAgent() };

            _positionSampler = new DispatcherTimer
                               {
                                   Interval = TimeSpan.FromMilliseconds(75)
                               };
            _positionSampler.Tick += OnPositionSamplerOnTick;

#if STREAM_SWITCHING
            _timer = new DispatcherTimer();

            _timer.Tick += (sender, args) =>
                           {
                               GC.Collect();
                               GC.WaitForPendingFinalizers();
                               GC.Collect();

                               var gcMemory = GC.GetTotalMemory(true).BytesToMiB();

                               _track = (int)(_tracks.Count * GlobalPlatformServices.Default.GetRandomNumber());

                               var task = PlayCurrentTrackAsync();
                               TaskCollector.Default.Add(task, "MainPage Timer PlayCurrentTrackAsync");

                               var track = CurrentTrack;

                               Debug.WriteLine("Switching to {0} (GC {1:F3} MiB App {2:F3}/{3:F3}/{4:F3} MiB)",
                                   null == track ? "<none>" : track.Url.ToString(), gcMemory,
                                   DeviceStatus.ApplicationCurrentMemoryUsage.BytesToMiB(),
                                   DeviceStatus.ApplicationPeakMemoryUsage.BytesToMiB(),
                                   DeviceStatus.ApplicationMemoryUsageLimit.BytesToMiB());

                               var interval = TimeSpan.FromSeconds(17 + GlobalPlatformServices.Default.GetRandomNumber() * 33);

                               Debug.WriteLine("MainPage.UpdateState() interval set to " + interval);

                               _timer.Interval = interval;
                           };

            _timer.Interval = TimeSpan.FromSeconds(25);

            _timer.Start();
#endif // STREAM_SWITCHING
        }

        MediaTrack CurrentTrack
        {
            get
            {
                if (_track < 0 || _track >= _tracks.Count)
                    return null;

                return _tracks[_track];
            }
        }

        void mediaElement1_CurrentStateChanged(object sender, RoutedEventArgs e)
        {
            var state = null == mediaElement1 ? MediaElementState.Closed : mediaElement1.CurrentState;

            if (null != _mediaStreamFacade)
            {
                var managerState = _mediaStreamFacade.State;

                if (MediaElementState.Closed == state)
                {
                    if (TsMediaManager.MediaState.OpenMedia == managerState || TsMediaManager.MediaState.Opening == managerState || TsMediaManager.MediaState.Playing == managerState)
                        state = MediaElementState.Opening;
                }
            }

            UpdateState(state);
        }

        void UpdateState(MediaElementState state)
        {
            Debug.WriteLine("MediaElement State: " + state);

            if (MediaElementState.Buffering == state && null != mediaElement1)
                MediaStateBox.Text = string.Format("Buffering {0:F2}%", mediaElement1.BufferingProgress * 100);
            else
                MediaStateBox.Text = state.ToString();

            switch (state)
            {
                case MediaElementState.Closed:
                    playButton.IsEnabled = true;
                    stopButton.IsEnabled = false;
                    break;
                case MediaElementState.Paused:
                    playButton.IsEnabled = true;
                    stopButton.IsEnabled = true;
                    errorBox.Visibility = Visibility.Collapsed;
                    break;
                case MediaElementState.Playing:
                    playButton.IsEnabled = false;
                    stopButton.IsEnabled = true;
                    errorBox.Visibility = Visibility.Collapsed;
                    break;
                default:
                    stopButton.IsEnabled = true;
                    errorBox.Visibility = Visibility.Collapsed;
                    break;
            }

            OnPositionSamplerOnTick(null, null);
        }

        void OnPositionSamplerOnTick(object o, EventArgs ea)
        {
            if (null == mediaElement1 || (MediaElementState.Playing != mediaElement1.CurrentState && MediaElementState.Paused != mediaElement1.CurrentState))
            {
                PositionBox.Text = "--:--:--.--";

                return;
            }

            try
            {
                var positionSample = mediaElement1.Position;

                if (positionSample == _previousPosition)
                    return;

                _previousPosition = positionSample;

                PositionBox.Text = FormatTimeSpan(positionSample);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Position sampler failed: " + ex.Message);
            }
        }

        string FormatTimeSpan(TimeSpan timeSpan)
        {
            var sb = new StringBuilder();

            if (timeSpan < TimeSpan.Zero)
            {
                sb.Append('-');

                timeSpan = -timeSpan;
            }

            if (timeSpan.Days > 1)
                sb.AppendFormat(timeSpan.ToString(@"%d\."));

            sb.Append(timeSpan.ToString(@"hh\:mm\:ss\.ff"));

            return sb.ToString();
        }

        void play_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Play clicked");

            if (null == mediaElement1)
            {
                Debug.WriteLine("MainPage Play no media element");
                return;
            }

            if (MediaElementState.Paused == mediaElement1.CurrentState)
            {
                mediaElement1.Play();
                return;
            }

            var task = PlayCurrentTrackAsync();

            TaskCollector.Default.Add(task, "MainPage Play OnPlayAsync");
        }

        async Task PlayCurrentTrackAsync()
        {
            errorBox.Visibility = Visibility.Collapsed;
            playButton.IsEnabled = false;

            var track = CurrentTrack;

            if (null == track)
            {
                await _mediaStreamFacade.StopAsync(CancellationToken.None);

                mediaElement1.Stop();
                mediaElement1.Source = null;

                return;
            }

            if (track.UseNativePlayer)
            {
                if (null != _mediaStreamFacade)
                    await _mediaStreamFacade.StopAsync(CancellationToken.None);

                mediaElement1.Source = track.Url;
            }
            else
            {
                if (null != mediaElement1.Source)
                    mediaElement1.Source = null;

                try
                {
                    InitializeMediaStream();

                    var mss = await _mediaStreamFacade.CreateMediaStreamSourceAsync(track.Url, CancellationToken.None);

                    if (null == mss)
                    {
                        Debug.WriteLine("MainPage.PlayCurrentTrackAsync() Unable to create media stream source");
                        return;
                    }

                    mediaElement1.SetSource(mss);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("MainPage.PlayCurrentTrackAsync() Unable to create media stream source: " + ex.Message);
                    return;
                }
            }

            mediaElement1.Play();

            _positionSampler.Start();
        }

        void InitializeMediaStream()
        {
            if (null != _mediaStreamFacade)
                return;

            _mediaStreamFacade = MediaStreamFacadeSettings.Parameters.Create();

            _mediaStreamFacade.SetParameter(_httpClientsParameters);

            _mediaStreamFacade.StateChange += TsMediaManagerOnStateChange;
        }

        void StopMedia()
        {
            _positionSampler.Stop();

            if (null != mediaElement1)
                mediaElement1.Source = null;
        }

        void CloseMedia()
        {
            StopMedia();

            if (null == _mediaStreamFacade)
                return;

            var mediaStreamFacade = _mediaStreamFacade;

            _mediaStreamFacade = null;

            mediaStreamFacade.StateChange -= TsMediaManagerOnStateChange;

            // Don't block the cleanup in case someone is mashing the play button.
            // It could deadlock.
            mediaStreamFacade.DisposeBackground("MainPage CloseMedia");
        }

        void TsMediaManagerOnStateChange(object sender, TsMediaManagerStateEventArgs tsMediaManagerStateEventArgs)
        {
            Dispatcher.BeginInvoke(() =>
                                   {
                                       var message = tsMediaManagerStateEventArgs.Message;

                                       if (!string.IsNullOrWhiteSpace(message))
                                       {
                                           errorBox.Text = message;
                                           errorBox.Visibility = Visibility.Visible;
                                       }

                                       mediaElement1_CurrentStateChanged(null, null);
                                   });
        }

        void mediaElement1_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            errorBox.Text = e.ErrorException.Message;
            errorBox.Visibility = Visibility.Visible;

            CloseMedia();

            playButton.IsEnabled = true;

#if STREAM_SWITCHING
            if (_timer.Interval > TimeSpan.FromSeconds(3))
                _timer.Interval = TimeSpan.FromSeconds(3);
#endif
        }

        void mediaElement1_MediaEnded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("MainPage MediaEnded");

            StopMedia();

#if STREAM_SWITCHING
            if (_timer.Interval > TimeSpan.FromSeconds(3))
                _timer.Interval = TimeSpan.FromSeconds(3);
#endif
        }

        void stopButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Stop clicked");

            StopMedia();
        }

        void wakeButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Wake clicked");

            if (Debugger.IsAttached)
                Debugger.Break();

            mediaElement1_CurrentStateChanged(null, null);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            CloseMedia();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            CloseMedia();
        }

        void plusButton_Click(object sender, RoutedEventArgs e)
        {
            if (null == mediaElement1 || mediaElement1.CurrentState != MediaElementState.Playing)
                return;

            var position = mediaElement1.Position;

            mediaElement1.Position = position + StepSize;

            Debug.WriteLine("Step from {0} to {1} (CanSeek: {2} NaturalDuration: {3})", position, mediaElement1.Position, mediaElement1.CanSeek, mediaElement1.NaturalDuration);
        }

        void minusButton_Click(object sender, RoutedEventArgs e)
        {
            if (null == mediaElement1 || mediaElement1.CurrentState != MediaElementState.Playing)
                return;

            var position = mediaElement1.Position;

            if (position < StepSize)
                position = TimeSpan.Zero;
            else
                position -= StepSize;

            mediaElement1.Position = position;

            Debug.WriteLine("Step from {0} to {1} (CanSeek: {2} NaturalDuration: {3})", position, mediaElement1.Position, mediaElement1.CanSeek, mediaElement1.NaturalDuration);
        }

        void mediaElement1_BufferingProgressChanged(object sender, RoutedEventArgs e)
        {
            mediaElement1_CurrentStateChanged(sender, e);
        }
    }
}

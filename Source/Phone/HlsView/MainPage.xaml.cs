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
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
#endif // STREAM_SWITCHING

        static readonly TimeSpan StepSize = TimeSpan.FromMinutes(2);
        static readonly IApplicationInformation ApplicationInformation = ApplicationInformationFactory.Default;
        readonly IHttpClientsParameters _httpClientsParameters;
        readonly DispatcherTimer _positionSampler;
        MediaStreamFacade _mediaStreamFacade;
        TimeSpan _previousPosition;
        static readonly MediaElementState[] NotStopStates = { MediaElementState.Closed, MediaElementState.Stopped };
        static readonly MediaElementState[] PlayStates = { MediaElementState.Closed, MediaElementState.Paused, MediaElementState.Stopped };
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

                               if (++_track >= _tracks.Count)
                                   _track = 0;

                               var task = PlayCurrentTrackAsync();

                               TaskCollector.Default.Add(task, "MainPage Play");

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

            //_timer.Tick += (sender, args) =>
            //               {
            //                   Debug.WriteLine("Stopping player");

            //                   player.Stop();
            //               };

            _timer.Interval = TimeSpan.FromSeconds(20);

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

            stopButton.IsEnabled = !NotStopStates.Contains(state);
            playButton.IsEnabled = PlayStates.Contains(state);

            OnPositionSamplerOnTick(null, null);
        }

        void OnPositionSamplerOnTick(object o, EventArgs ea)
        {
            if (null == mediaElement1 || (MediaElementState.Playing != mediaElement1.CurrentState && MediaElementState.Paused != mediaElement1.CurrentState))
            {
                PositionBox.Text = "--:--:--.--";

                return;
            }

            var positionSample = mediaElement1.Position;

            if (positionSample == _previousPosition)
                return;

            _previousPosition = positionSample;

            PositionBox.Text = FormatTimeSpan(positionSample);
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

            if (null != mediaElement1 && MediaElementState.Paused == mediaElement1.CurrentState)
            {
                mediaElement1.Play();
                return;
            }

            var task = PlayCurrentTrackAsync();

            TaskCollector.Default.Add(task, "MainPage Play");
        }

        async Task PlayCurrentTrackAsync()
        {
            errorBox.Visibility = Visibility.Collapsed;
            playButton.IsEnabled = false;

            var track = CurrentTrack;

            if (null != track)
            {
                try
                {
                    InitializeMediaStream();

                    var mss = await _mediaStreamFacade.CreateMediaStreamSourceAsync(track.Url, CancellationToken.None);

                    if (null == mss)
                    {
                        Debug.WriteLine("MainPage Play unable to create media stream source");
                        return;
                    }

                    if (null == mediaElement1)
                    {
                        Debug.WriteLine("MainPage Play null media element");
                        return;
                    }

                    mediaElement1.SetSource(mss);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("MainPage Play failed: " + ex.Message);
                    return;
                }

                mediaElement1.Play();

                _positionSampler.Start();
            }
            else
            {
                await _mediaStreamFacade.StopAsync(CancellationToken.None);

                mediaElement1.Stop();
                mediaElement1.Source = null;
            }
        }

        void InitializeMediaStream()
        {
            if (null != _mediaStreamFacade)
                return;

            _mediaStreamFacade = new MediaStreamFacade();

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
        }

        void mediaElement1_MediaEnded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("MainPage MediaEnded");

            StopMedia();
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

            var me = mediaElement1;

            ContentPanel.Children.Remove(me);

            me.MediaFailed -= mediaElement1_MediaFailed;
            me.MediaEnded -= mediaElement1_MediaEnded;
            me.CurrentStateChanged -= mediaElement1_CurrentStateChanged;
            me.BufferingProgressChanged -= OnBufferingProgressChanged;

            mediaElement1 = null;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            CloseMedia();

            var me = new MediaElement
                     {
                         Margin = new Thickness(0)
                     };

            me.MediaFailed += mediaElement1_MediaFailed;
            me.MediaEnded += mediaElement1_MediaEnded;
            me.CurrentStateChanged += mediaElement1_CurrentStateChanged;
            me.BufferingProgressChanged += OnBufferingProgressChanged;
            ContentPanel.Children.Add(me);

            mediaElement1 = me;
        }

        void plusButton_Click(object sender, RoutedEventArgs e)
        {
            if (null == mediaElement1 || mediaElement1.CurrentState != MediaElementState.Playing)
                return;

            var position = mediaElement1.Position;

            _mediaStreamFacade.SeekTarget = position + StepSize; // WP7's MediaElement needs help.
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

            _mediaStreamFacade.SeekTarget = position; // WP7's MediaElement needs help.
            mediaElement1.Position = position;

            Debug.WriteLine("Step from {0} to {1} (CanSeek: {2} NaturalDuration: {3})", position, mediaElement1.Position, mediaElement1.CanSeek, mediaElement1.NaturalDuration);
        }

        void OnBufferingProgressChanged(object sender, RoutedEventArgs routedEventArgs)
        {
            mediaElement1_CurrentStateChanged(sender, routedEventArgs);
        }
    }
}

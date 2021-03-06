﻿// -----------------------------------------------------------------------
//  <copyright file="MainPage.xaml.cs" company="Henric Jungheim">
//  Copyright (c) 2012-2016.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012-2016 Henric Jungheim <software@henric.org>
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
using Windows.Media;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using SM.Media;
using SM.Media.MediaManager;
using SM.Media.Playlists;
using SM.Media.Utility;

namespace HlsView
{
    public partial class MainPage : Page
    {
        static readonly TimeSpan StepSize = TimeSpan.FromMinutes(2);
        readonly DispatcherTimer _positionSampler;
#if STREAM_SWITCHING
        readonly DispatcherTimer _timer;
#endif
        readonly IList<MediaTrack> _tracks = TrackManager.Tracks;
        IMediaStreamFacade _mediaStreamFacade;
        TimeSpan _previousPosition;
        int _track;
        bool _wasFull;

        // Constructor
        public MainPage()
        {
            InitializeComponent();

            _positionSampler = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(75)
            };
            _positionSampler.Tick += OnPositionSamplerOnTick;

            Unloaded += (sender, args) => OnUnload();

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

                UpdateNextPrev();

                var task = PlayCurrentTrackAsync();
                TaskCollector.Default.Add(task, "MainPage PlayCurrentTrackAsync");

                var track = CurrentTrack;

                Debug.WriteLine("Switching to {0} (track {1} GC {2:F3} MiB)",
                    null == track ? "<none>" : track.Url.ToString(), _track, gcMemory);
            };

            _timer.Interval = TimeSpan.FromSeconds(15);

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

        void UpdateTrack(SystemMediaTransportControls systemMediaTransportControls)
        {
            var d = systemMediaTransportControls.DisplayUpdater;

            d.ClearAll();

            var track = CurrentTrack;

            if (null != track)
            {
                d.Type = MediaPlaybackType.Video;

                if (!string.IsNullOrEmpty(track.Title))
                    d.VideoProperties.Title = track.Title;
            }

            d.Update();
        }

        void UpdateNextPrev()
        {
            var smtc = SystemMediaTransportControls.GetForCurrentView();

            smtc.IsPreviousEnabled = _track > 0;
            smtc.IsNextEnabled = _track < _tracks.Count - 1;

            UpdateTrack(smtc);
        }

        async void SystemControls_ButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            Debug.WriteLine("MainPage SystemControls ButtonPressed: " + args.Button);

            try
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => SystemControlsHandleButton(args.Button));
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Main SystemControls ButtonPressed: {0}, failed: {1}", args.Button, ex.Message);
            }
        }

        void SystemControlsHandleButton(SystemMediaTransportControlsButton button)
        {
            Debug.WriteLine("MainPage.SystemControlsHandleButton");

            switch (button)
            {
                case SystemMediaTransportControlsButton.Play:
                    OnPlay();
                    break;
                case SystemMediaTransportControlsButton.Pause:
                    mediaElement1.Pause();
                    break;
                case SystemMediaTransportControlsButton.Stop:
                    mediaElement1.Stop();
                    break;
                case SystemMediaTransportControlsButton.Next:
                    OnNext();
                    break;
                case SystemMediaTransportControlsButton.Previous:
                    OnPrevious();
                    break;
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
                    if (MediaManagerState.OpenMedia == managerState || MediaManagerState.Opening == managerState || MediaManagerState.Playing == managerState)
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

            var smtc = SystemMediaTransportControls.GetForCurrentView();

            switch (state)
            {
                case MediaElementState.Closed:
                    playButton.IsEnabled = true;
                    stopButton.IsEnabled = false;

                    smtc.PlaybackStatus = MediaPlaybackStatus.Closed;

                    break;
                case MediaElementState.Paused:
                    playButton.IsEnabled = true;
                    stopButton.IsEnabled = true;

                    smtc.PlaybackStatus = MediaPlaybackStatus.Paused;

                    break;
                case MediaElementState.Playing:
                    playButton.IsEnabled = false;
                    stopButton.IsEnabled = true;

                    smtc.PlaybackStatus = MediaPlaybackStatus.Playing;

                    break;
                case MediaElementState.Stopped:
                    playButton.IsEnabled = true;
                    stopButton.IsEnabled = true;

                    smtc.PlaybackStatus = MediaPlaybackStatus.Stopped;

                    break;
                default:
                    stopButton.IsEnabled = true;

                    break;
            }

            if (null == mediaElement1)
                _wasFull = false;
            else
            {
                if (MediaElementState.Playing == state)
                {
                    if (_wasFull)
                        mediaElement1.IsFullWindow = true;
                }
                else
                {
                    _wasFull = mediaElement1.IsFullWindow;

                    if (_wasFull)
                        mediaElement1.IsFullWindow = false;
                }
            }

            OnPositionSamplerOnTick(null, null);
        }

        void OnPositionSamplerOnTick(object o, object o1)
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

            OnPlay();
        }

        void OnPlay()
        {
            Debug.WriteLine("MainPage.OnPlay()");

            if (null == mediaElement1)
            {
                Debug.WriteLine("MainPage.OnPlay() mediaElement1 is null");
                return;
            }

            if (MediaElementState.Paused == mediaElement1.CurrentState)
            {
                mediaElement1.Play();

                return;
            }

            var task = PlayCurrentTrackAsync();

            TaskCollector.Default.Add(task, "MainPage.OnPlay() PlayCurrentTrackAsync");
        }

        async Task PlayCurrentTrackAsync()
        {
            errorBox.Visibility = Visibility.Collapsed;
            playButton.IsEnabled = false;

            var track = CurrentTrack;

            if (null == track)
            {
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
                mediaElement1.Source = null;

                try
                {
                    InitializeMediaStream();

                    var mss = await _mediaStreamFacade.CreateMediaStreamSourceAsync(track, CancellationToken.None);

                    if (null == mss)
                    {
                        Debug.WriteLine("MainPage.PlayCurrentTrackAsync() Unable to create media stream source");
                        return;
                    }

                    mediaElement1.SetMediaStreamSource(mss);
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

        void OnNext()
        {
            Debug.WriteLine("MainPage.OnNextTrack()");

            if (++_track >= _tracks.Count)
                _track = 0;

            UpdateNextPrev();

            var task = PlayCurrentTrackAsync();

            TaskCollector.Default.Add(task, "MainPage.OnNext() PlayCurrentTrackAsync");
        }

        void OnPrevious()
        {
            Debug.WriteLine("MainPage.OnPreviousTrack()");

            if (--_track < 0)
                _track = 0;

            UpdateNextPrev();

            var task = PlayCurrentTrackAsync();

            TaskCollector.Default.Add(task, "MainPage.OnPrevious() PlayCurrentTrackAsync");
        }

        void InitializeMediaStream()
        {
            if (null != _mediaStreamFacade)
                return;

            // Enable UseHttpConnection to use sockets directly instead of going through
            // the system's default HTTP client.  HttpConnection is not as picky about
            // headers and it will not pollute the system's HTTP cache, but it will always
            // open a new connection for each request.
            //MediaStreamFacadeSettings.Parameters.UseHttpConnection = true;

            _mediaStreamFacade = MediaStreamFacadeSettings.Parameters.Create();

            _mediaStreamFacade.StateChange += TsMediaManagerOnStateChange;
        }

        void CleanupMediaStream()
        {
            mediaElement1.Source = null;

            if (null == _mediaStreamFacade)
                return;

            _mediaStreamFacade.StateChange -= TsMediaManagerOnStateChange;

            _mediaStreamFacade.DisposeSafe();

            _mediaStreamFacade = null;
        }

        void TsMediaManagerOnStateChange(object sender, MediaManagerStateEventArgs mediaManagerStateEventArgs)
        {
            var awaiter = Dispatcher.RunAsync(CoreDispatcherPriority.Low,
                () =>
                {
                    var message = mediaManagerStateEventArgs.Message;

                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        errorBox.Text = message;
                        errorBox.Visibility = Visibility.Visible;
                    }

                    mediaElement1_CurrentStateChanged(null, null);
                });

            TaskCollector.Default.Add(awaiter.AsTask(), "MainPage TsMediaManagerOnStateChange");
        }

        void mediaElement1_MediaOpened(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("MainPage media opened");

            if (null == mediaElement1)
                return;

            if (mediaElement1.IsFullWindow && mediaElement1.IsAudioOnly)
                mediaElement1.IsFullWindow = false;
        }

        void mediaElement1_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            Debug.WriteLine("MainPage media failed");

            errorBox.Text = e.ErrorMessage;
            errorBox.Visibility = Visibility.Visible;

            CleanupMediaStream();

            playButton.IsEnabled = true;
        }

        void mediaElement1_MediaEnded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("MainPage media ended");

            StopMedia();
        }

        void stopButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Stop clicked");

            if (null != mediaElement1)
                mediaElement1.Source = null;

            if (null != _mediaStreamFacade)
                _mediaStreamFacade.RequestStop();
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
            Debug.WriteLine("MainPage.OnNavigatedFrom()");

            base.OnNavigatedFrom(e);

            StopMedia();

            var smtc = SystemMediaTransportControls.GetForCurrentView();

            smtc.ButtonPressed -= SystemControls_ButtonPressed;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Debug.WriteLine("MainPage.OnNavigatedTo()");

            base.OnNavigatedTo(e);

            var smtc = SystemMediaTransportControls.GetForCurrentView();

            smtc.PlaybackStatus = MediaPlaybackStatus.Closed;

            UpdateTrack(smtc);

            smtc.ButtonPressed += SystemControls_ButtonPressed;

            smtc.IsPlayEnabled = true;
            smtc.IsPauseEnabled = true;
            smtc.IsStopEnabled = true;

            var multipleTracks = _tracks.Count > 1;

            smtc.IsNextEnabled = multipleTracks;
            smtc.IsPreviousEnabled = false;
        }

        void nextButton_Click(object sender, RoutedEventArgs e)
        {
            OnNext();
        }

        void prevButton_Click(object sender, RoutedEventArgs e)
        {
            OnPrevious();
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

        void StopMedia()
        {
            if (null != mediaElement1)
                mediaElement1.Source = null;

            _positionSampler.Stop();
        }

        void mediaElement1_BufferingProgressChanged(object sender, RoutedEventArgs e)
        {
            mediaElement1_CurrentStateChanged(sender, e);
        }

        void OnUnload()
        {
            Debug.WriteLine("MainPage unload");

            StopMedia();

            var mediaStreamFacade = _mediaStreamFacade;
            _mediaStreamFacade = null;

            mediaStreamFacade.DisposeBackground("MainPage unload");
        }
    }
}

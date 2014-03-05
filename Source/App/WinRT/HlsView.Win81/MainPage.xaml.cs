﻿// -----------------------------------------------------------------------
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

using System;
using System.Diagnostics;
using System.Text;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using SM.Media;
using SM.Media.Utility;
using SM.Media.Web;

namespace HlsView
{
    public partial class MainPage : Page
    {
#if STREAM_SWITCHING
        static readonly string[] Sources =
        {
            "http://www.npr.org/streams/mp3/nprlive24.pls",
            "http://www.nasa.gov/multimedia/nasatv/NTV-Public-IPS.m3u8",
            "http://devimages.apple.com/iphone/samples/bipbop/bipbopall.m3u8",
            null,
            "https://devimages.apple.com.edgekey.net/streaming/examples/bipbop_16x9/bipbop_16x9_variant.m3u8"
        };

        readonly DispatcherTimer _timer;
        int _count;
#endif

        static readonly TimeSpan StepSize = TimeSpan.FromMinutes(2);
        static readonly IApplicationInformation ApplicationInformation = ApplicationInformationFactory.DefaultTask.Result;
        readonly IHttpClients _httpClients;
        readonly IMediaElementManager _mediaElementManager;
        readonly DispatcherTimer _positionSampler;
        IMediaStreamFascade _mediaStreamFascade;
        TimeSpan _previousPosition;

        // Constructor
        public MainPage()
        {
            InitializeComponent();

            _mediaElementManager = new WinRtMediaElementManager(Dispatcher,
                () =>
                {
                    UpdateState(MediaElementState.Opening);

                    return mediaElement1;
                },
                me => UpdateState(MediaElementState.Closed));

            var userAgent = ApplicationInformation.CreateUserAgent();

            _httpClients = new HttpClients(userAgent: userAgent);

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

                               var source = Sources[_count++ % Sources.Length];

                               Debug.WriteLine("Switching to {0} (count {1} GC {2:F3} MiB)", source, _count, gcMemory);

                               InitializeMediaStream();

                               _mediaStreamFascade.Source = null == source ? null : new Uri(source);

                               _positionSampler.Start();
                           };

            _timer.Interval = TimeSpan.FromSeconds(15);

            _timer.Start();
#endif // STREAM_SWITCHING
        }

        void mediaElement1_CurrentStateChanged(object sender, RoutedEventArgs e)
        {
            var state = null == mediaElement1 ? MediaElementState.Closed : mediaElement1.CurrentState;

            if (null != _mediaStreamFascade)
            {
                var managerState = _mediaStreamFascade.State;

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
                    break;
                case MediaElementState.Playing:
                    playButton.IsEnabled = false;
                    stopButton.IsEnabled = true;
                    break;
                default:
                    stopButton.IsEnabled = true;
                    break;
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

            if (null == mediaElement1)
            {
                Debug.WriteLine("MainPage.play_Click() mediaElement1 is null");
                return;
            }

            if (MediaElementState.Paused == mediaElement1.CurrentState)
            {
                mediaElement1.Play();

                return;
            }

            errorBox.Visibility = Visibility.Collapsed;
            playButton.IsEnabled = false;

            InitializeMediaStream();

            _mediaStreamFascade.Source = new Uri(
                //"http://www.nasa.gov/multimedia/nasatv/NTV-Public-IPS.m3u8"
                //"http://devimages.apple.com/iphone/samples/bipbop/bipbopall.m3u8"
                "https://devimages.apple.com.edgekey.net/streaming/examples/bipbop_16x9/bipbop_16x9_variant.m3u8"
                );

            mediaElement1.Play();

            _positionSampler.Start();
        }

        void InitializeMediaStream()
        {
            if (null != _mediaStreamFascade)
                return;

            _mediaStreamFascade = MediaStreamFascadeSettings.Parameters.Create(_httpClients, _mediaElementManager.SetSourceAsync);

            _mediaStreamFascade.SetParameter(_mediaElementManager);

            _mediaStreamFascade.StateChange += TsMediaManagerOnStateChange;
        }

        void CleanupMediaStream()
        {
            mediaElement1.Source = null;

            if (null == _mediaStreamFascade)
                return;

            _mediaStreamFascade.StateChange -= TsMediaManagerOnStateChange;

            _mediaStreamFascade.DisposeSafe();

            _mediaStreamFascade = null;
        }

        void TsMediaManagerOnStateChange(object sender, TsMediaManagerStateEventArgs tsMediaManagerStateEventArgs)
        {
            var awaiter = Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
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
            errorBox.Text = e.ErrorMessage;
            errorBox.Visibility = Visibility.Visible;

            CleanupMediaStream();

            playButton.IsEnabled = true;
        }

        void mediaElement1_MediaEnded(object sender, RoutedEventArgs e)
        {
            StopMedia();
        }

        void stopButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Stop clicked");

            if (null != mediaElement1)
                mediaElement1.Source = null;
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

            CleanupMediaStream();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            CleanupMediaStream();
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

        public void OnUnload()
        {
            Debug.WriteLine("MainPage unload");

            if (null != mediaElement1)
                mediaElement1.Source = null;

            var mediaStreamFascade = _mediaStreamFascade;
            _mediaStreamFascade = null;

            mediaStreamFascade.DisposeBackground("MainPage unload");
        }
    }
}

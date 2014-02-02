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

using System;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using Microsoft.Phone.Controls;
using SM.Media;
using SM.Media.Utility;
using SM.Media.Web;

namespace HlsView
{
    public partial class MainPage : PhoneApplicationPage
    {
        static readonly TimeSpan StepSize = TimeSpan.FromMinutes(2);
        static readonly IApplicationInformation ApplicationInformation = ApplicationInformationFactory.Default;
        readonly IMediaElementManager _mediaElementManager;
        readonly MediaStreamFascadeParameters _mediaStreamFascadeParameters;
        readonly DispatcherTimer _positionSampler;
        MediaStreamFascade _mediaStreamFascade;
        TimeSpan _previousPosition;

        // Constructor
        public MainPage()
        {
            InitializeComponent();

            _mediaElementManager = new MediaElementManager(Dispatcher,
                () =>
                {
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

                    UpdateState(MediaElementState.Opening);

                    return me;
                },
                me =>
                {
                    if (null != me)
                    {
                        Debug.Assert(ReferenceEquals(me, mediaElement1));

                        ContentPanel.Children.Remove(me);

                        me.MediaFailed -= mediaElement1_MediaFailed;
                        me.MediaEnded -= mediaElement1_MediaEnded;
                        me.CurrentStateChanged -= mediaElement1_CurrentStateChanged;
                        me.BufferingProgressChanged -= OnBufferingProgressChanged;
                    }

                    mediaElement1 = null;

                    UpdateState(MediaElementState.Closed);
                });

            var httpClients = new HttpClients(userAgent: ApplicationInformation.CreateUserAgent());

            _mediaStreamFascadeParameters = MediaStreamFascadeParameters.Create<TsMediaStreamSource>(httpClients);

            _mediaStreamFascadeParameters.MediaManagerParameters.MediaElementManager = _mediaElementManager;

            _positionSampler = new DispatcherTimer
                               {
                                   Interval = TimeSpan.FromMilliseconds(75)
                               };
            _positionSampler.Tick += OnPositionSamplerOnTick;
        }

        void OnBufferingProgressChanged(object sender, RoutedEventArgs routedEventArgs)
        {
            mediaElement1_CurrentStateChanged(sender, routedEventArgs);
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

            if (MediaElementState.Closed == state)
            {
                playButton.IsEnabled = true;
                stopButton.IsEnabled = false;
            }
            else if (MediaElementState.Paused == state)
            {
                playButton.IsEnabled = true;
                stopButton.IsEnabled = true;
            }
            else
                stopButton.IsEnabled = true;
        }

        void OnPositionSamplerOnTick(object o, EventArgs ea)
        {
            if (null == mediaElement1 || MediaElementState.Playing != mediaElement1.CurrentState)
                return;

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

            errorBox.Visibility = Visibility.Collapsed;
            playButton.IsEnabled = false;

            if (null != _mediaStreamFascade)
            {
                _mediaStreamFascade.StateChange -= TsMediaManagerOnStateChange;
                _mediaStreamFascade.DisposeSafe();
            }

            _mediaStreamFascade = new MediaStreamFascade(_mediaStreamFascadeParameters, _mediaElementManager.SetSourceAsync)
                                  {
                                      Source = new Uri(
                                          "http://www.nasa.gov/multimedia/nasatv/NTV-Public-IPS.m3u8"
                                          //"http://devimages.apple.com/iphone/samples/bipbop/bipbopall.m3u8"
                                          )
                                  };

            _mediaStreamFascade.StateChange += TsMediaManagerOnStateChange;

            _mediaStreamFascade.Play();

            _positionSampler.Start();
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

            CleanupMedia();

            playButton.IsEnabled = true;
        }

        void CleanupMedia()
        {
            _positionSampler.Stop();

            if (null != _mediaStreamFascade)
                _mediaStreamFascade.RequestStop();
        }

        void mediaElement1_MediaEnded(object sender, RoutedEventArgs e)
        {
            CleanupMedia();
        }

        void stopButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Stop clicked");

            CleanupMedia();
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

            CleanupMedia();

            if (null != _mediaElementManager)
            {
                _mediaElementManager.CloseAsync()
                                    .Wait();
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (null != _mediaElementManager)
            {
                _mediaElementManager.CloseAsync()
                                    .Wait();
            }
        }

        void plusButton_Click(object sender, RoutedEventArgs e)
        {
            if (null == mediaElement1 || mediaElement1.CurrentState != MediaElementState.Playing)
                return;

            var position = mediaElement1.Position;

            _mediaStreamFascade.SeekTarget = position + StepSize; // WP7's MediaElement needs help.
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

            _mediaStreamFascade.SeekTarget = position; // WP7's MediaElement needs help.
            mediaElement1.Position = position;

            Debug.WriteLine("Step from {0} to {1} (CanSeek: {2} NaturalDuration: {3})", position, mediaElement1.Position, mediaElement1.CanSeek, mediaElement1.NaturalDuration);
        }
    }
}

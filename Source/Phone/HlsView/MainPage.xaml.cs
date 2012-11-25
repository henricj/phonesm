// -----------------------------------------------------------------------
//  <copyright file="MainPage.xaml.cs" company="Henric Jungheim">
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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using Microsoft.Phone.Controls;
using SM.Media;
using SM.Media.Playlists;

namespace HlsView
{
    public partial class MainPage : PhoneApplicationPage
    {
        readonly DispatcherTimer _positionSampler;
        int _positionSampleCount;
        TimeSpan _previousPosition;
        ITsMediaManager _tsMediaManager;

        // Constructor
        public MainPage()
        {
            InitializeComponent();

            _positionSampler = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(75) };
            _positionSampler.Tick += OnPositionSamplerOnTick;
        }


        void mediaElement1_CurrentStateChanged(object sender, RoutedEventArgs e)
        {
            var state = mediaElement1.CurrentState;

            Debug.WriteLine("MediaElement State: " + state);

            MediaStateBox.Text = state.ToString();

            if (MediaElementState.Closed == state)
            {
                playButton.IsEnabled = true;
                stopButton.IsEnabled = false;
            }
            else
                stopButton.IsEnabled = true;
        }

        void OnPositionSamplerOnTick(object o, EventArgs ea)
        {
            var positionSample = mediaElement1.Position;

            if (positionSample == _previousPosition)
                return;

            _previousPosition = positionSample;

            _tsMediaManager.ReportPosition(positionSample);

            if (++_positionSampleCount > 2)
            {
                _positionSampleCount = 0;

                var positionText = positionSample.ToString();

                PositionBox.Text = positionText; //.Substring(0, positionText.Length - 5);
            }
        }

        void play_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Play clicked");

            errorBox.Visibility = Visibility.Collapsed;
            playButton.IsEnabled = false;

            var simpleSegmentManager = new PlaylistSegmentManager(new Uri("http://www.nasa.gov/multimedia/nasatv/NTV-Public-IPS.m3u8"));

            _tsMediaManager = new TsMediaManager(mediaElement1);

            _tsMediaManager.Play(simpleSegmentManager);

            _positionSampler.Start();
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

            if (null != _tsMediaManager)
                _tsMediaManager.Close();
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

            var state = mediaElement1.CurrentState;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            CleanupMedia();

            ContentPanel.Children.Remove(mediaElement1);

            mediaElement1.MediaFailed -= mediaElement1_MediaFailed;
            mediaElement1.MediaEnded -= mediaElement1_MediaEnded;
            mediaElement1.CurrentStateChanged -= mediaElement1_CurrentStateChanged;

            mediaElement1 = null;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (null == mediaElement1)
            {
                // <MediaElement Name="mediaElement1" AutoPlay="True" MediaFailed="mediaElement1_MediaFailed" MediaEnded="mediaElement1_MediaEnded" />
                mediaElement1 = new MediaElement { Margin = new Thickness(0) };

                mediaElement1.MediaFailed += mediaElement1_MediaFailed;
                mediaElement1.MediaEnded += mediaElement1_MediaEnded;
                mediaElement1.CurrentStateChanged += mediaElement1_CurrentStateChanged;

                ContentPanel.Children.Add(mediaElement1);
            }

            mediaElement1_CurrentStateChanged(null, null);
        }
    }
}
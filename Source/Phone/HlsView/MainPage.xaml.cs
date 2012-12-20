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
using System.Linq;
using System.Net;
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
        IMediaElementManager _mediaElementManager;

        // Constructor
        public MainPage()
        {
            InitializeComponent();

            _positionSampler = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(75) };
            _positionSampler.Tick += OnPositionSamplerOnTick;
        }


        void mediaElement1_CurrentStateChanged(object sender, RoutedEventArgs e)
        {
            var state = null == mediaElement1 ? MediaElementState.Closed : mediaElement1.CurrentState;

            UpdateState(state);
        }

        void UpdateState(MediaElementState state)
        {
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
            if (null == mediaElement1)
                return;

            var positionSample = mediaElement1.Position;

            if (positionSample == _previousPosition)
                return;

            _previousPosition = positionSample;

            _tsMediaManager.ReportPosition(positionSample);

            if (++_positionSampleCount > 2)
            {
                _positionSampleCount = 0;

                var positionText = positionSample.ToString();

                PositionBox.Text = positionText;
            }
        }

        async void play_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Play clicked");

            errorBox.Visibility = Visibility.Collapsed;
            playButton.IsEnabled = false;

            var programManager = new ProgramManager { Playlists = new[] { new Uri("http://www.nasa.gov/multimedia/nasatv/NTV-Public-IPS.m3u8") } };
            //var programManager = new ProgramManager { Playlists = new[] { new Uri("http://devimages.apple.com/iphone/samples/bipbop/bipbopall.m3u8") } };

            Program program;
            ISubProgram subProgram;

            try
            {
                var programs = await programManager.LoadAsync();

                program = programs.Values.FirstOrDefault();

                if (null == program)
                {
                    errorBox.Text = "No programs found";
                    errorBox.Visibility = Visibility.Visible;
                    playButton.IsEnabled = true;

                    return;
                }

                subProgram = program.SubPrograms.FirstOrDefault();

                if (null == subProgram)
                {
                    errorBox.Text = "No program streams found";
                    errorBox.Visibility = Visibility.Visible;
                    playButton.IsEnabled = true;

                    return;
                }
            }
            catch (Exception ex)
            {
                errorBox.Text = ex.Message;
                errorBox.Visibility = Visibility.Visible;
                playButton.IsEnabled = true;

                return;
            }

            Func<Uri, HttpWebRequest> webRequestFactory = new HttpWebRequestFactory(program.Url).Create;

            var playlist = new PlaylistSegmentManager(uri => new CachedWebRequest(uri, webRequestFactory), subProgram);

            _mediaElementManager = new MediaElementManager(Dispatcher,
                                                           () =>
                                                           {
                                                               var me = new MediaElement { Margin = new Thickness(0) };

                                                               me.MediaFailed += mediaElement1_MediaFailed;
                                                               me.MediaEnded += mediaElement1_MediaEnded;
                                                               me.CurrentStateChanged += mediaElement1_CurrentStateChanged;

                                                               ContentPanel.Children.Add(me);

                                                               mediaElement1 = me;

                                                               UpdateState(MediaElementState.Opening);

                                                               return me;
                                                           },
                                                           me =>
                                                           {
                                                               Debug.Assert(ReferenceEquals(me, mediaElement1));

                                                               ContentPanel.Children.Remove(me);

                                                               me.MediaFailed -= mediaElement1_MediaFailed;
                                                               me.MediaEnded -= mediaElement1_MediaEnded;
                                                               me.CurrentStateChanged -= mediaElement1_CurrentStateChanged;

                                                               mediaElement1 = null;

                                                               UpdateState(MediaElementState.Closed);
                                                           });

            _tsMediaManager = new TsMediaManager(_mediaElementManager, mm => new TsMediaStreamSource(mm));

            _tsMediaManager.Play(new SM.Media.Segments.SegmentReaderManager(playlist));

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

            mediaElement1_CurrentStateChanged(null, null);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            CleanupMedia();

            if (null != _mediaElementManager)
                _mediaElementManager.Close().Wait();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (null != _mediaElementManager)
                _mediaElementManager.Close().Wait();
        }
    }
}

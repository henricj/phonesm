// -----------------------------------------------------------------------
//  <copyright file="MainPage.xaml.cs" company="Henric Jungheim">
//  Copyright (c) 2012-2015.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012-2015 Henric Jungheim <software@henric.org>
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
using System.Windows;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Info;
using Microsoft.PlayerFramework;
using SM.Media.MediaPlayer;
using SM.Media.Playlists;
using SM.Media.Utility;

namespace SamplePlayer.WP8
{
    public partial class MainPage : PhoneApplicationPage
    {
#if STREAM_SWITCHING
        readonly DispatcherTimer _timer;
#endif

        static readonly Uri StopUrl = new Uri("stop://stop");
        readonly IList<MediaTrack> _tracks = TrackManager.Tracks;
        int _trackIndex;

        // Constructor
        public MainPage()
        {
            InitializeComponent();

#if STREAM_SWITCHING
            player.PlayerStateChanged += (sender, args) =>
            {
                if (PlayerState.Failed == args.NewValue || PlayerState.Ending == args.NewValue)
                {
                    if (_timer.Interval < TimeSpan.FromSeconds(3))
                        _timer.Interval = TimeSpan.FromSeconds(3);
                }
            };

            _timer = new DispatcherTimer();

            _timer.Tick += (sender, args) =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                var gcMemory = GC.GetTotalMemory(true).BytesToMiB();


                if (++_trackIndex >= _tracks.Count)
                    _trackIndex = 0;

                var source = UpdateSource();

                Debug.WriteLine("Switching to {0} (GC {1:F3} MiB App {2:F3}/{3:F3}/{4:F3} MiB)", source, gcMemory,
                    DeviceStatus.ApplicationCurrentMemoryUsage.BytesToMiB(),
                    DeviceStatus.ApplicationPeakMemoryUsage.BytesToMiB(),
                    DeviceStatus.ApplicationMemoryUsageLimit.BytesToMiB());

                var interval = TimeSpan.FromSeconds(15);

                Debug.WriteLine("MainPage.UpdateState() interval set to " + interval);

                _timer.Interval = interval;
            };

            //_timer.Tick += (sender, args) =>
            //               {
            //                   Debug.WriteLine("Stopping player");

            //                   player.Stop();
            //               };

            _timer.Interval = TimeSpan.FromSeconds(15);

            _timer.Start();
#endif
            var passThroughTracks = new HashSet<Uri>(_tracks.Where(t => null != t && t.UseNativePlayer).Select(t => t.Url));

            StreamingMediaSettings.Parameters.IsPassThrough = passThroughTracks.Contains;

            // Sample code to localize the ApplicationBar
            //BuildLocalizedApplicationBar();
        }

        // Sample code for building a localized ApplicationBar
        //private void BuildLocalizedApplicationBar()
        //{
        //    // Set the page's ApplicationBar to a new instance of ApplicationBar.
        //    ApplicationBar = new ApplicationBar();

        //    // Create a new button and set the text value to the localized string from AppResources.
        //    ApplicationBarIconButton appBarButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/appbar.add.rest.png", UriKind.Relative));
        //    appBarButton.Text = AppResources.AppBarButtonText;
        //    ApplicationBar.Buttons.Add(appBarButton);

        //    // Create a new menu item with the localized string from AppResources.
        //    ApplicationBarMenuItem appBarMenuItem = new ApplicationBarMenuItem(AppResources.AppBarMenuItemText);
        //    ApplicationBar.MenuItems.Add(appBarMenuItem);
        //}

        void play_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Play clicked");

            if (null == player.Source || MediaElementState.Closed == player.CurrentState)
                UpdateSource();

            player.Play();
        }

        void stopButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Stop clicked");

            player.Close();

            // Deal with player framework quirks.
            player.Source = null;
            player.Source = StopUrl;
            player.Source = null;
        }

        void wakeButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Wake clicked");

            if (Debugger.IsAttached)
                Debugger.Break();
        }

        void nextButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Next clicked");

            if (++_trackIndex >= _tracks.Count)
                _trackIndex = 0;

            UpdateSource();
        }

        void prevButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Prev clicked");

            if (--_trackIndex < 0)
                _trackIndex = _tracks.Count - 1;

            UpdateSource();
        }

        Uri UpdateSource()
        {
            // Work around quirk.  If Source isn't set to null before
            // setting another stream, playback will be cancelled.
            player.Source = null;

            if (_tracks.Count < 1)
                return null;

            if (_trackIndex < 0)
                _trackIndex = 0;
            else if (_trackIndex >= _tracks.Count)
                _trackIndex = _tracks.Count - 1;

            var track = _tracks[_trackIndex];

            if (null == track)
                return null;

            player.Source = track.Url;

            return track.Url;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            player.Dispose();

            base.OnNavigatedFrom(e);
        }
    }
}

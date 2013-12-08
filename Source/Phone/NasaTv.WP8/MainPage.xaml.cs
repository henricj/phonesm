﻿// -----------------------------------------------------------------------
//  <copyright file="MainPage.xaml.cs" company="Henric Jungheim">
//  Copyright (c) 2012, 2013.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012, 2013 Henric Jungheim <software@henric.org>
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
using System.Net.Http.Headers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using SM.Media;
using SM.Media.Playlists;
using SM.Media.Segments;
using SM.Media.Utility;
using SM.Media.Web;

namespace NasaTv8
{
    public partial class MainPage : PhoneApplicationPage
    {
        // Constructor

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

        static readonly IApplicationInformation ApplicationInformation = new ApplicationInformation();
        readonly IHttpClients _httpClients;
        MediaElementManager _mediaElementManager;
        PlaylistSegmentManager _playlist;
        ITsMediaManager _tsMediaManager;
        TsMediaStreamSource _tsMediaStreamSource;

        public MainPage()
        {
            InitializeComponent();

            _httpClients = new HttpClients(userAgent: new ProductInfoHeaderValue(ApplicationInformation.Title ?? "Unknown", ApplicationInformation.Version ?? "0.0"));

            // Sample code to localize the ApplicationBar
            //BuildLocalizedApplicationBar();

            foreach (ApplicationBarIconButton ib in ApplicationBar.Buttons)
            {
                switch (ib.Text)
                {
                    case "stop":
                        stopButton = ib;
                        break;
                    case "play":
                        playButton = ib;
                        break;
                }
            }

            OnStop();
        }

        void OnPlay()
        {
            errorBox.Visibility = Visibility.Collapsed;
            playButton.IsEnabled = false;

            if (null != mediaElement1)
                mediaElement1.Visibility = Visibility.Visible;

            LayoutRoot.Background.Opacity = 0.25;
        }

        void OnStop()
        {
            playButton.IsEnabled = true;
            stopButton.IsEnabled = false;
            ApplicationBar.IsVisible = true;

            if (null != mediaElement1)
                mediaElement1.Visibility = Visibility.Collapsed;

            LayoutRoot.Background.Opacity = 1;
        }

        async void playButton_Click(object sender, EventArgs e)
        {
            Debug.WriteLine("Play clicked");

            // Try to avoid shattering MediaElement's glass jaw.
            if (MediaElementState.Closed != mediaElement1.CurrentState)
            {
                CleanupMedia();

                return;
            }

            OnPlay();

            if (null != _playlist)
            {
                _playlist.Dispose();
                _playlist = null;
            }

            if (null != _tsMediaStreamSource)
            {
                _tsMediaStreamSource.Dispose();
                _tsMediaStreamSource = null;
            }

            var segmentsFactory = new SegmentsFactory(_httpClients);

            var programManager = new ProgramManager(_httpClients, segmentsFactory.CreateStreamSegments)
                                 {
                                     Playlists = new[] { new Uri("http://www.nasa.gov/multimedia/nasatv/NTV-Public-IPS.m3u8") }
                                 };
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

            var programClient = _httpClients.CreatePlaylistClient(program.Url);

            _playlist = new PlaylistSegmentManager(uri => new CachedWebRequest(uri, programClient), subProgram, segmentsFactory.CreateStreamSegments);

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

                    ContentPanel.Children.Add(me);

                    mediaElement1 = me;

                    UpdateState(MediaElementState.Opening);

                    return me;
                },
                me =>
                {
                    if (null != mediaElement1)
                    {
                        Debug.Assert(ReferenceEquals(me, mediaElement1));

                        ContentPanel.Children.Remove(me);

                        me.MediaFailed -= mediaElement1_MediaFailed;
                        me.MediaEnded -= mediaElement1_MediaEnded;
                        me.CurrentStateChanged -= mediaElement1_CurrentStateChanged;

                        mediaElement1 = null;
                    }

                    UpdateState(MediaElementState.Closed);
                });

            var segmentReaderManager = new SegmentReaderManager(new[] { _playlist }, _httpClients.CreateSegmentClient);

            if (null != _tsMediaManager)
                _tsMediaManager.OnStateChange -= TsMediaManagerOnStateChange;

            _tsMediaStreamSource = new TsMediaStreamSource();

            _tsMediaManager = new TsMediaManager(segmentReaderManager, _mediaElementManager, _tsMediaStreamSource);

            _tsMediaManager.OnStateChange += TsMediaManagerOnStateChange;

            _tsMediaManager.Play();
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
            //mediaElement1.Stop();
            //mediaElement1.Source = null;

            if (null != _tsMediaManager)
                _tsMediaManager.Close();
        }

        void mediaElement1_MediaEnded(object sender, RoutedEventArgs e)
        {
            CleanupMedia();
        }

        void stopButton_Click(object sender, EventArgs e)
        {
            Debug.WriteLine("Stop clicked");

            CleanupMedia();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            CleanupMedia();

            if (null != _mediaElementManager)
                _mediaElementManager.CloseAsync().Wait();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (null != _mediaElementManager)
                _mediaElementManager.CloseAsync().Wait();
        }

        void PhoneApplicationPageTap(object sender, GestureEventArgs e)
        {
            Debug.WriteLine("Tapped");

            var state = mediaElement1.CurrentState;

            if (MediaElementState.Closed == state)
            {
                ApplicationBar.IsVisible = true;
                SystemTray.IsVisible = true;
            }
            else
            {
                ApplicationBar.IsVisible = !ApplicationBar.IsVisible;
                SystemTray.IsVisible = ApplicationBar.IsVisible;
            }
        }

        void PhoneApplicationPageLoaded(object sender, RoutedEventArgs e)
        {
            SystemTray.IsVisible = ApplicationBar.IsVisible;
        }

        void about_Click(object sender, EventArgs e)
        {
            NavigateTo("/Views/About.xaml");
        }

        void settings_Click(object sender, EventArgs e)
        {
            NavigateTo("/Views/Settings.xaml");
        }

        static void NavigateTo(string viewsAboutXaml)
        {
            var root = Application.Current.RootVisual as PhoneApplicationFrame;
            if (null == root)
                return;

            root.Navigate(new Uri(viewsAboutXaml, UriKind.Relative));
        }

        void mediaElement1_CurrentStateChanged(object sender, RoutedEventArgs e)
        {
            UpdateState();
        }

        void TsMediaManagerOnStateChange(object sender, TsMediaManagerStateEventArgs tsMediaManagerStateEventArgs)
        {
            Dispatcher.InvokeAsync(() =>
                                   {
                                       var message = tsMediaManagerStateEventArgs.Message;

                                       if (!string.IsNullOrWhiteSpace(message))
                                       {
                                           errorBox.Text = message;
                                           errorBox.Visibility = Visibility.Visible;
                                       }

                                       UpdateState();
                                   });
        }

        void UpdateState()
        {
            var state = MediaElementState.Closed;

            if (null != mediaElement1)
                state = mediaElement1.CurrentState;

            UpdateState(state);
        }

        void UpdateState(MediaElementState state)
        {
            Debug.WriteLine("MediaElement State: " + state);

            if (MediaElementState.Closed == state)
                OnStop();
            else
                stopButton.IsEnabled = true;
        }
    }
}

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
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using NasaTv;
using SM.Media;
using SM.Media.Utility;
using SM.Media.Web;
using SM.Media.Web.HttpClientReader;

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

        static readonly IApplicationInformation ApplicationInformation = ApplicationInformationFactory.Default;
        readonly HttpClientsParameters _httpClientsParameters;
        readonly PersistentSettings _settings = new PersistentSettings();
        IMediaStreamFacade _mediaStreamFacade;

        public MainPage()
        {
            InitializeComponent();

            _httpClientsParameters = new HttpClientsParameters { UserAgent = ApplicationInformation.CreateUserAgent() };

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
            stopButton.IsEnabled = true;

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

            OnStop();

            if (null == mediaElement1)
            {
                Debug.WriteLine("MainPage Play no media element");
                return;
            }

            if (MediaElementState.Closed != mediaElement1.CurrentState)
            {
                CleanupMedia();

                UpdateState();

                return;
            }

            var source = _settings.PlaylistUrl;

            InitializeMediaStream();

            try
            {
                var mss = await _mediaStreamFacade.CreateMediaStreamSourceAsync(source, CancellationToken.None);

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
                Debug.WriteLine("MainPage Play unable to create media stream source: " + ex.Message);
                return;
            }

            mediaElement1.Play();

            OnPlay();

            UpdateState();
        }

        void InitializeMediaStream()
        {
            if (null != _mediaStreamFacade)
                return;

            _mediaStreamFacade = MediaStreamFacadeSettings.Parameters.Create();

            _mediaStreamFacade.SetParameter(_httpClientsParameters);

            _mediaStreamFacade.StateChange += TsMediaManagerOnStateChange;
        }

        void CleanupMedia()
        {
            if (null != _mediaStreamFacade)
                _mediaStreamFacade.RequestStop();

            if (null != mediaElement1)
            {
                mediaElement1.Stop();
                mediaElement1.Source = null;
            }
        }

        void mediaElement1_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            errorBox.Text = e.ErrorException.Message;
            errorBox.Visibility = Visibility.Visible;

            CleanupMedia();

            playButton.IsEnabled = true;
        }

        void mediaElement1_MediaEnded(object sender, RoutedEventArgs e)
        {
            CleanupMedia();
        }

        void mediaElement1_CurrentStateChanged(object sender, RoutedEventArgs e)
        {
            UpdateState();
        }

        void stopButton_Click(object sender, EventArgs e)
        {
            Debug.WriteLine("Stop clicked");

            CleanupMedia();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            Debug.WriteLine("OnNavigatedFrom()");

            base.OnNavigatedFrom(e);

            CleanupMedia();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Debug.WriteLine("OnNavigatedTo()");

            base.OnNavigatedTo(e);

            CleanupMedia();

            UpdateState();
        }

        void PhoneApplicationPageTap(object sender, GestureEventArgs e)
        {
            Debug.WriteLine("Tapped");

            var state = null == mediaElement1 ? MediaElementState.Closed : mediaElement1.CurrentState;

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
                OnPlay();
        }
    }
}

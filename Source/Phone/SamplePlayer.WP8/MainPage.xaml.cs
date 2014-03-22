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
using System.Diagnostics;
using System.Windows.Threading;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Info;
using Microsoft.PlayerFramework;
using SM.Media.Utility;

namespace SamplePlayer.WP8
{
    public partial class MainPage : PhoneApplicationPage
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

                               var source = Sources[_count];

                               Debug.WriteLine("Switching to {0} (GC {1:F3} MiB App {2:F3}/{3:F3}/{4:F3} MiB)", source, gcMemory,
                                   DeviceStatus.ApplicationCurrentMemoryUsage.BytesToMiB(),
                                   DeviceStatus.ApplicationPeakMemoryUsage.BytesToMiB(),
                                   DeviceStatus.ApplicationMemoryUsageLimit.BytesToMiB());

                               player.Source = null == source ? null : new Uri(source);

                               if (++_count >= Sources.Length)
                                   _count = 0;

                               var interval = TimeSpan.FromSeconds(17 + GlobalPlatformServices.Default.GetRandomNumber() * 33);

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
    }
}

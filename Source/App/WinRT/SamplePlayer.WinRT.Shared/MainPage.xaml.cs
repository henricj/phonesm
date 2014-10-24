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

using System.Collections.Generic;
using System.Diagnostics;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace SamplePlayer.WinRT
{
    /// <summary>
    ///     An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        readonly IList<MediaTrack> _tracks = TrackManager.Tracks;
        int _trackIndex;

        public MainPage()
        {
            InitializeComponent();
        }

        void play_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Play clicked");

            player.Play();
        }

        void stopButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Stop clicked");

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

        void UpdateSource()
        {
            // Work around quirk.  If Source isn't set to null before
            // setting another stream, playback will be cancelled.
            player.Source = null;

            if (_trackIndex < 0)
                _trackIndex = 0;
            else if (_trackIndex >= _tracks.Count)
                _trackIndex = _tracks.Count;

            var track = _tracks[_trackIndex];

            if (null != track)
                player.Source = track.Url;
        }
    }
}

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
using System.Windows;
using System.Windows.Threading;
using Microsoft.Phone.BackgroundAudio;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;

namespace BackgroundAudio.Sample.WP7
{
    public partial class MainPage : PhoneApplicationPage
    {
        // Timer for updating the UI

        // Indexes into the array of ApplicationBar.Buttons
        const int PrevButtonIndex = 0;
        const int PlayButtonIndex = 1;
        const int PauseButtonIndex = 2;
        const int NextButtonIndex = 3;
        readonly ApplicationBarIconButton _nextButton;
        readonly ApplicationBarIconButton _pauseButton;
        readonly ApplicationBarIconButton _playButton;
        readonly ApplicationBarIconButton _prevButton;
        DispatcherTimer _timer;

        public MainPage()
        {
            InitializeComponent();

            _prevButton = ((ApplicationBarIconButton)(ApplicationBar.Buttons[PrevButtonIndex]));
            _pauseButton = ((ApplicationBarIconButton)(ApplicationBar.Buttons[PauseButtonIndex]));
            _playButton = ((ApplicationBarIconButton)(ApplicationBar.Buttons[PlayButtonIndex]));
            _nextButton = ((ApplicationBarIconButton)(ApplicationBar.Buttons[NextButtonIndex]));
        }

        void MainPage_OnLoaded(object sender, RoutedEventArgs e)
        {
            // Initialize a timer to update the UI every half-second.
            _timer = new DispatcherTimer
                     {
                         Interval = TimeSpan.FromSeconds(0.5)
                     };

            _timer.Tick += UpdateState;

            BackgroundAudioPlayer.Instance.PlayStateChanged += Instance_PlayStateChanged;

            Instance_PlayStateChanged(null, null);
        }

        /// <summary>
        ///     PlayStateChanged event handler.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Instance_PlayStateChanged(object sender, EventArgs e)
        {
            switch (BackgroundAudioPlayer.Instance.PlayerState)
            {
                case PlayState.Playing:
                    // Update the UI.
                    {
                        var track = BackgroundAudioPlayer.Instance.Track;

                        if (null != track)
                        {
                            var duration = track.Duration;

                            if (duration > TimeSpan.Zero)
                            {
                                positionIndicator.IsIndeterminate = false;
                                positionIndicator.Maximum = duration.TotalSeconds;
                            }
                        }
                    }

                    _playButton.IsEnabled = false;
                    _pauseButton.IsEnabled = true;

                    UpdateState(null, null);

                    // Start the timer for updating the UI.
                    _timer.Start();

                    break;
                case PlayState.Stopped:
                case PlayState.Paused:
                    // Update the UI.

                    _playButton.IsEnabled = true;
                    _pauseButton.IsEnabled = false;

                    UpdateState(null, null);

                    // Stop the timer for updating the UI.
                    _timer.Stop();

                    break;
                case PlayState.Unknown:
                    _playButton.IsEnabled = true;
                    _pauseButton.IsEnabled = true;

                    break;
            }

            _nextButton.IsEnabled = true;
            _prevButton.IsEnabled = true;
        }

        /// <summary>
        ///     Updates the status indicators including the State, Track title,
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void UpdateState(object sender, EventArgs e)
        {
            try
            {
                var player = BackgroundAudioPlayer.Instance;

                if (null == player)
                    return;

                txtState.Text = string.Format("State: {0}", player.PlayerState);

                var track = player.Track;

                if (null != track)
                    txtTrack.Text = string.Format("Track: {0}", track.Title);

                // Set the current position on the ProgressBar.
                positionIndicator.Value = player.Position.TotalSeconds;

                // Update the current playback position.
                var position = player.Position;
                textPosition.Text = string.Format("{0:d2}:{1:d2}:{2:d2}", position.Hours, position.Minutes, position.Seconds);

                // Update the time remaining digits.
                if (null != track)
                {
                    var timeRemaining = track.Duration - position;
                    textRemaining.Text = string.Format("-{0:d2}:{1:d2}:{2:d2}", timeRemaining.Hours, timeRemaining.Minutes, timeRemaining.Seconds);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MainPage.UpdateState() failed: " + ex.Message);
            }
        }

        /// <summary>
        ///     Click handler for the Skip Previous button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void prevButton_Click(object sender, EventArgs e)
        {
            // Show the indeterminate progress bar.
            positionIndicator.IsIndeterminate = true;

            // Disable the button so the user can't click it multiple times before 
            // the background audio agent is able to handle their request.
            _prevButton.IsEnabled = false;

            // Tell the background audio agent to skip to the previous track.
            BackgroundAudioPlayer.Instance.SkipPrevious();
        }

        /// <summary>
        ///     Click handler for the Play button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void playButton_Click(object sender, EventArgs e)
        {
            // Tell the background audio agent to play the current track.
            BackgroundAudioPlayer.Instance.Play();
        }

        /// <summary>
        ///     Click handler for the Pause button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void pauseButton_Click(object sender, EventArgs e)
        {
            // Tell the background audio agent to pause the current track.
            BackgroundAudioPlayer.Instance.Pause();
        }

        /// <summary>
        ///     Click handler for the Skip Next button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void nextButton_Click(object sender, EventArgs e)
        {
            // Show the indeterminate progress bar.
            positionIndicator.IsIndeterminate = true;

            // Disable the button so the user can't click it multiple times before 
            // the background audio agent is able to handle their request.
            _nextButton.IsEnabled = false;

            // Tell the background audio agent to skip to the next track.
            BackgroundAudioPlayer.Instance.SkipNext();
        }
    }
}

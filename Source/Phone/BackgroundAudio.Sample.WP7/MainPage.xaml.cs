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
        const int PlayButtonIndex = 0;
        const int PauseButtonIndex = 1;
        readonly ApplicationBarIconButton _pauseButton;
        readonly ApplicationBarIconButton _playButton;
        DispatcherTimer _timer;

        public MainPage()
        {
            InitializeComponent();

            _playButton = ((ApplicationBarIconButton)(ApplicationBar.Buttons[PlayButtonIndex]));
            _pauseButton = ((ApplicationBarIconButton)(ApplicationBar.Buttons[PauseButtonIndex]));
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
                    UpdateButtons(false, true);
                    UpdateState(null, null);

                    // Start the timer for updating the UI.
                    _timer.Start();
                    break;

                case PlayState.Stopped:
                case PlayState.Paused:
                    // Update the UI.
                    UpdateButtons(true, false);
                    UpdateState(null, null);

                    // Stop the timer for updating the UI.
                    _timer.Stop();
                    break;
                case PlayState.Unknown:
                    UpdateButtons(true, true);
                    break;
            }
        }

        /// <summary>
        ///     Helper method to update the state of the ApplicationBar.Buttons
        /// </summary>
        /// <param name="playBtnEnabled"></param>
        /// <param name="pauseBtnEnabled"></param>
        void UpdateButtons(bool playBtnEnabled, bool pauseBtnEnabled)
        {
            // Set the IsEnabled state of the ApplicationBar.Buttons array
            _playButton.IsEnabled = playBtnEnabled;
            _pauseButton.IsEnabled = pauseBtnEnabled;
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
    }
}

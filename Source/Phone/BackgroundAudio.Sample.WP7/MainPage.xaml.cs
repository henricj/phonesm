using System;
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
        DispatcherTimer _timer;

        // Indexes into the array of ApplicationBar.Buttons
        const int playButton = 0;
        const int pauseButton = 1;

        public MainPage()
        {
            InitializeComponent();
        }

        private void MainPage_OnLoaded(object sender, RoutedEventArgs e)
        {
            // Initialize a timer to update the UI every half-second.
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(0.5);
            _timer.Tick += new EventHandler(UpdateState);

            BackgroundAudioPlayer.Instance.PlayStateChanged += new EventHandler(Instance_PlayStateChanged);

            if (BackgroundAudioPlayer.Instance.PlayerState == PlayState.Playing)
            {
                // If audio was already playing when the app was launched, update the UI.
                positionIndicator.IsIndeterminate = false;
                positionIndicator.Maximum = BackgroundAudioPlayer.Instance.Track.Duration.TotalSeconds;
                UpdateButtons(false, true);
                UpdateState(null, null);
            }
        }

        /// <summary>
        /// PlayStateChanged event handler.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Instance_PlayStateChanged(object sender, EventArgs e)
        {
            switch (BackgroundAudioPlayer.Instance.PlayerState)
            {
                case PlayState.Playing:
                    // Update the UI.
                    positionIndicator.IsIndeterminate = false;
                    positionIndicator.Maximum = BackgroundAudioPlayer.Instance.Track.Duration.TotalSeconds;
                    UpdateButtons(false, true);
                    UpdateState(null, null);

                    // Start the timer for updating the UI.
                    _timer.Start();
                    break;

                case PlayState.Paused:
                    // Update the UI.
                    UpdateButtons(true, false);
                    UpdateState(null, null);

                    // Stop the timer for updating the UI.
                    _timer.Stop();
                    break;
            }
        }


        /// <summary>
        /// Helper method to update the state of the ApplicationBar.Buttons
        /// </summary>
        /// <param name="playBtnEnabled"></param>
        /// <param name="pauseBtnEnabled"></param>
        void UpdateButtons(bool playBtnEnabled, bool pauseBtnEnabled)
        {
            // Set the IsEnabled state of the ApplicationBar.Buttons array
            ((ApplicationBarIconButton)(ApplicationBar.Buttons[playButton])).IsEnabled = playBtnEnabled;
            ((ApplicationBarIconButton)(ApplicationBar.Buttons[pauseButton])).IsEnabled = pauseBtnEnabled;
        }


        /// <summary>
        /// Updates the status indicators including the State, Track title, 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UpdateState(object sender, EventArgs e)
        {
            txtState.Text = string.Format("State: {0}", BackgroundAudioPlayer.Instance.PlayerState);

            if (BackgroundAudioPlayer.Instance.Track != null)
            {
                txtTrack.Text = string.Format("Track: {0}", BackgroundAudioPlayer.Instance.Track.Title);

                // Set the current position on the ProgressBar.
                positionIndicator.Value = BackgroundAudioPlayer.Instance.Position.TotalSeconds;

                // Update the current playback position.
                TimeSpan position = new TimeSpan();
                position = BackgroundAudioPlayer.Instance.Position;
                textPosition.Text = String.Format("{0:d2}:{1:d2}:{2:d2}", position.Hours, position.Minutes, position.Seconds);

                // Update the time remaining digits.
                TimeSpan timeRemaining = new TimeSpan();
                timeRemaining = BackgroundAudioPlayer.Instance.Track.Duration - position;
                textRemaining.Text = String.Format("-{0:d2}:{1:d2}:{2:d2}", timeRemaining.Hours, timeRemaining.Minutes, timeRemaining.Seconds);
            }
        }

        /// <summary>
        /// Click handler for the Play button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void playButton_Click(object sender, EventArgs e)
        {
            if (BackgroundAudioPlayer.Instance.Track == null)
                BackgroundAudioPlayer.Instance.Track = new AudioTrack(null, null, null, null, null, "http://devimages.apple.com/iphone/samples/bipbop/bipbopall.m3u8", EnabledPlayerControls.All);

            // Tell the backgound audio agent to play the current track.
            BackgroundAudioPlayer.Instance.Play();
        }


        /// <summary>
        /// Click handler for the Pause button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void pauseButton_Click(object sender, EventArgs e)
        {
            // Tell the backgound audio agent to pause the current track.
            BackgroundAudioPlayer.Instance.Pause();
        }
    }
}
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

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Media;
using Windows.Media.Playback;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using SM.Media.BackgroundAudio;
using SM.Media.Utility;

namespace BackgroundAudio.Sample
{
    /// <summary>
    ///     An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        readonly MediaPlayerHandle _mediaPlayerHandle;
        readonly DispatcherTimer _timer;
        int _refreshPending;
        string _trackName;

        public MainPage()
        {
            InitializeComponent();

            _mediaPlayerHandle = new MediaPlayerHandle(Dispatcher);

            _mediaPlayerHandle.MessageReceivedFromBackground += OnMessageReceivedFromBackground;
            _mediaPlayerHandle.CurrentStateChanged += OnCurrentStateChanged;

            NavigationCacheMode = NavigationCacheMode.Required;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(0.6)
            };

            var count = 0;

            _timer.Tick += (sender, o) =>
            {
                var mediaPlayer = MediaPlayer;

                if (null == mediaPlayer)
                    return;

                try
                {
                    var position = mediaPlayer.Position;

                    txtPosition.Text = position.ToString();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("MainPage position update failed: " + ex.Message);

                    // The COM object is probably dead...
                    CleanupFailedPlayer();
                }

                if (++count < 5)
                    return;

                count = 0;

                _mediaPlayerHandle.NotifyBackground(BackgroundNotificationType.Memory);
            };
        }

        MediaPlayer MediaPlayer
        {
            get
            {
                Debug.Assert(Dispatcher.HasThreadAccess, "MediaPlayer requires the dispatcher thread");

                return _mediaPlayerHandle.MediaPlayer;
            }
        }

        bool IsRunning
        {
            get { return _mediaPlayerHandle.IsRunning; }
        }

        void CloseMediaPlayerAndUpdate()
        {
            Debug.WriteLine("MainPage.CloseMediaPlayerAndUpdate()");

            CloseMediaPlayer();

            RefreshUi(MediaPlayerState.Closed, null);
        }

        void CloseMediaPlayer()
        {
            Debug.WriteLine("MainPage.CloseMediaPlayer()");

            _timer.Stop();

            _mediaPlayerHandle.Close();

            _trackName = null;
        }

        async Task OpenMediaPlayerAsync()
        {
            Debug.WriteLine("MainPage.OpenMediaPlayer()");

            _timer.Start();

            await _mediaPlayerHandle.OpenAsync();

            var mediaPlayer = MediaPlayer;

            if (null == mediaPlayer)
            {
                Debug.WriteLine("MainPage.OpenMediaPlayer() failed");

                _timer.Stop();

                RequestRefresh();

                return;
            }

            RefreshUi(mediaPlayer.CurrentState, _trackName);
        }

        void CleanupFailedPlayer()
        {
            Debug.WriteLine("MainPage.CleanupFailedPlayer()");

            try
            {
                CloseMediaPlayerAndUpdate();

                _mediaPlayerHandle.Shutdown();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MainPage.CleanupFailedPlayer() failed: " + ex.ExtendedMessage());
            }
        }

        /// <summary>
        ///     Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">
        ///     Event data that describes how this page was reached.
        ///     This parameter is typically used to configure the page.
        /// </param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Debug.WriteLine("MainPage.OnNavigatedTo()");

            // TODO: Prepare page for display here.

            // TODO: If your application contains multiple pages, ensure that you are
            // handling the hardware Back button by registering for the
            // Windows.Phone.UI.Input.HardwareButtons.BackPressed event.
            // If you are using the NavigationHelper provided by some templates,
            // this event is handled for you.

            Application.Current.Suspending += OnSuspending;
            Application.Current.Resuming += OnResuming;

            CloseMediaPlayerAndUpdate();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            Debug.WriteLine("MainPage.OnNavigatedFrom()");

            Application.Current.Suspending -= OnSuspending;
            Application.Current.Resuming -= OnResuming;

            _mediaPlayerHandle.Suspend();

            _timer.Stop();
        }

        void OnResuming(object sender, object o)
        {
            Debug.WriteLine("MainPage.OnResuming()");

            _mediaPlayerHandle.Resume();
        }

        void OnSuspending(object sender, SuspendingEventArgs suspendingEventArgs)
        {
            //var deferral = suspendingEventArgs.SuspendingOperation.GetDeferral();

            Debug.WriteLine("MainPage.OnSuspending()");

            _mediaPlayerHandle.Suspend();

            //deferral.Complete();
        }

        void OnMessageReceivedFromBackground(object sender, MediaPlayerDataReceivedEventArgs mediaPlayerDataReceivedEventArgs)
        {
            //Debug.WriteLine("MainPage.OnMessageReceivedFromBackground()");

            long? memoryValue = null;
            ulong? appMemoryValue = null;
            ulong? appMemoryLimitValue = null;
            var updateMemory = false;
            string failMessage = null;
            string trackName = null;
            var callShutdown = false;

            foreach (var kv in mediaPlayerDataReceivedEventArgs.Data)
            {
                //Debug.WriteLine(" b->f {0}: {1}", kv.Key, kv.Value);

                try
                {
                    if (null == kv.Key)
                    {
                        Debug.WriteLine("*** MainPage.OnMessageReceivedFromBackground() null key");

                        continue; // This does happen.  It shouldn't, but it does.
                    }

                    BackgroundNotificationType type;
                    if (!Enum.TryParse(kv.Key, true, out type))
                        continue;

                    switch (type)
                    {
                        case BackgroundNotificationType.Track:
                            trackName = kv.Value as string ?? string.Empty;

                            break;
                        case BackgroundNotificationType.Fail:
                            callShutdown = true;
                            failMessage = kv.Value as string;

                            Debug.WriteLine("MainPage.OnMessageReceivedFromBackground() fail " + failMessage);

                            break;
                        case BackgroundNotificationType.Memory:
                            memoryValue = kv.Value as long?;
                            if (memoryValue.HasValue)
                                updateMemory = true;

                            break;
                        case BackgroundNotificationType.AppMemory:
                            appMemoryValue = kv.Value as ulong?;
                            if (appMemoryValue.HasValue)
                                updateMemory = true;

                            break;

                        case BackgroundNotificationType.AppMemoryLimit:
                            appMemoryLimitValue = kv.Value as ulong?;
                            if (appMemoryLimitValue.HasValue)
                                updateMemory = true;

                            break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("MainPage.OnMessageReceivedFromBackground() failed: " + ex.Message);
                }
            }

            if (null != failMessage)
                _trackName = null;

            if (null != trackName)
                _trackName = trackName;

            if (null != failMessage || null != trackName)
                RequestRefresh();

            if (updateMemory)
            {
                var memoryString = string.Format("{0:F2}MiB {1:F2}MiB/{2:F2}MiB",
                    memoryValue.BytesToMiB(), appMemoryValue.BytesToMiB(), appMemoryLimitValue.BytesToMiB());

                var awaiter = Dispatcher.RunAsync(CoreDispatcherPriority.Low, () => txtMemory.Text = memoryString);
            }

            if (callShutdown)
            {
                var awaiter2 = Dispatcher.RunAsync(CoreDispatcherPriority.Low, CleanupFailedPlayer);
            }
        }

        void OnCurrentStateChanged(object obj, object args)
        {
            Debug.WriteLine("MainPage.OnCurrentStateChanged()");

            RequestRefresh();
        }

        void RefreshUi()
        {
            Debug.WriteLine("MainPage.RefreshUi()");

            Debug.Assert(Dispatcher.HasThreadAccess, "RefreshUi requires the dispatcher thread");

            while (0 != Interlocked.Exchange(ref _refreshPending, 0))
            {
                try
                {
                    var mediaPlayer = MediaPlayer;

                    if (null == mediaPlayer)
                    {
                        txtPosition.Text = string.Empty;
                        RefreshUi(MediaPlayerState.Closed, null);

                        return;
                    }

                    MediaPlayerState? mediaPlayerState = null;

                    try
                    {
                        mediaPlayerState = mediaPlayer.CurrentState;
                    }
                    catch (Exception mediaPlayerException)
                    {
                        Debug.WriteLine("MainPage.RefreshUi() mediaPlayer failed: " + mediaPlayerException.ExtendedMessage());
                    }

                    if (mediaPlayerState.HasValue)
                        RefreshUi(mediaPlayerState.Value, _trackName);
                    else
                        CleanupFailedPlayer();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("MainPage.RefreshUi() failed: " + ex.Message);
                }
            }
        }

        void RefreshUi(MediaPlayerState currentState, string track)
        {
            Debug.WriteLine("MainPage.RefreshUi({0}, {1}) {2}", currentState, track, _mediaPlayerHandle.Id);

            txtCurrentTrack.Text = track ?? string.Empty;
            txtCurrentState.Text = currentState.ToString();

            playButton.Content = MediaPlayerState.Playing == currentState ? "| |" : ">";

            if (MediaPlayerState.Opening == currentState)
            {
                //prevButton.IsEnabled = false;
                playButton.IsEnabled = false;
                //nextButton.IsEnabled = false;
            }
            else
            {
                prevButton.IsEnabled = true;
                playButton.IsEnabled = true;
                nextButton.IsEnabled = true;
            }
        }

        void RequestRefresh()
        {
            var was = Interlocked.Exchange(ref _refreshPending, 1);

            if (0 != was)
                return;

            var awaiter = Dispatcher.RunAsync(CoreDispatcherPriority.Low, RefreshUi);
        }

        Task StartAudioAsync()
        {
            if (IsRunning)
                return TplTaskExtensions.TrueTask;

            return OpenMediaPlayerAsync();
        }

        #region Button Click Event Handlers

        void gcButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("MainPage gc");

            _mediaPlayerHandle.NotifyBackground(BackgroundNotificationType.Gc);
        }

        void wakeButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("MainPage wake");

            if (Debugger.IsAttached)
                Debugger.Break();

            RequestRefresh();
        }

        /// <summary>
        ///     Sends message to the background task to skip to the previous track.
        /// </summary>
        async void prevButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("MainPage prev");

            prevButton.IsEnabled = false;

            try
            {
                await StartAudioAsync();

                _mediaPlayerHandle.NotifyBackground(SystemMediaTransportControlsButton.Previous);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MainPage prevButton Click failed: " + ex.Message);
            }
            finally
            {
                prevButton.IsEnabled = true;
            }
        }

        async void playButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("MainPage play");

            playButton.IsEnabled = false;

            try
            {
                if (IsRunning)
                {
                    var mediaPlayer = MediaPlayer;

                    switch (mediaPlayer.CurrentState)
                    {
                        case MediaPlayerState.Playing:
                            mediaPlayer.Pause();
                            return;
                        case MediaPlayerState.Paused:
                            mediaPlayer.Play();
                            return;
                    }
                }

                await StartAudioAsync();

                _mediaPlayerHandle.NotifyBackground(SystemMediaTransportControlsButton.Play);
            }
            catch (OperationCanceledException)
            {
                CloseMediaPlayer();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MainPage playButton Click failed: " + ex.Message);
                CloseMediaPlayer();
            }
            finally
            {
                playButton.IsEnabled = true;
            }

            RequestRefresh();
        }

        async void nextButton_Click(object sender, RoutedEventArgs e)
        {
            nextButton.IsEnabled = false;

            try
            {
                await StartAudioAsync();

                _mediaPlayerHandle.NotifyBackground(SystemMediaTransportControlsButton.Next);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MainPage nextButton Click failed: " + ex.Message);
            }
            finally
            {
                nextButton.IsEnabled = true;
            }

            Debug.WriteLine("MainPage click");
        }

        void stopButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("MainPage stop");

            _mediaPlayerHandle.NotifyBackground(SystemMediaTransportControlsButton.Stop);
        }

        void killButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("MainPage kill");

            _mediaPlayerHandle.Shutdown();
        }

        #endregion Button Click Event Handlers
    }
}

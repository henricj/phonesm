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
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Foundation.Collections;
using Windows.Media;
using Windows.Media.Playback;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace BackgroundAudio.Sample
{
    /// <summary>
    ///     An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        readonly Guid _id = Guid.NewGuid();
        Guid? _backgroundId;
        TaskCompletionSource<object> _backgroundRunningCompletionSource = new TaskCompletionSource<object>();
        MediaPlayer _mediaPlayer;
        int _refreshPending;
        string _trackName;

        public MainPage()
        {
            InitializeComponent();

            NavigationCacheMode = NavigationCacheMode.Required;
        }

        MediaPlayer MediaPlayer
        {
            get { return _mediaPlayer; }
            set
            {
                Debug.Assert(Dispatcher.HasThreadAccess, "MediaPlayer requires the dispatcher thread");

                if (ReferenceEquals(value, _mediaPlayer))
                    return;

                BackgroundId = null;

                BackgroundMediaPlayer.MessageReceivedFromBackground -= OnMessageReceivedFromBackground;

                if (null != _mediaPlayer)
                    _mediaPlayer.CurrentStateChanged -= OnCurrentStateChanged;

                _mediaPlayer = value;

                if (null != _mediaPlayer)
                {
                    _mediaPlayer.CurrentStateChanged += OnCurrentStateChanged;
                    BackgroundMediaPlayer.MessageReceivedFromBackground += OnMessageReceivedFromBackground;

                    RefreshUi(_mediaPlayer.CurrentState, _trackName);

                    PingBackground();
                }
                else
                {
                    _trackName = null;
                    RefreshUi(MediaPlayerState.Closed, null);
                }
            }
        }

        Guid? BackgroundId
        {
            get { return _backgroundId; }
            set
            {
                Debug.Assert(Dispatcher.HasThreadAccess, "BackgroundId requires the dispatcher thread");

                if (_backgroundId == value)
                    return;

                _backgroundId = value;

                if (IsRunning)
                    _backgroundRunningCompletionSource.TrySetResult(null);
                else
                {
                    if (_backgroundRunningCompletionSource.Task.IsCompleted)
                        _backgroundRunningCompletionSource = new TaskCompletionSource<object>();
                }
            }
        }

        bool IsRunning
        {
            get
            {
                Debug.Assert(Dispatcher.HasThreadAccess, "IsRunning requires the dispatcher thread");

                return null != _mediaPlayer && _backgroundId.HasValue;
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

            MediaPlayer = null;

            PingBackground();
        }

        void PingBackground()
        {
            Debug.WriteLine("MainPage.PingBackground()");

            try
            {
                NotifyBackground("ping", _id);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MainPage.PingBackground() failed: " + ex.Message);
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            Debug.WriteLine("MainPage.OnNavigatedFrom()");

            Application.Current.Suspending -= OnSuspending;
            Application.Current.Resuming -= OnResuming;

            BackgroundMediaPlayer.MessageReceivedFromBackground -= OnMessageReceivedFromBackground;

            Suspend();
        }

        void OnResuming(object sender, object o)
        {
            Debug.WriteLine("MainPage.OnResuming()");

            _backgroundRunningCompletionSource = new TaskCompletionSource<object>();

            BackgroundMediaPlayer.MessageReceivedFromBackground += OnMessageReceivedFromBackground;

            MediaPlayer = null;

            NotifyBackground("resume", _id, true);
        }

        void OnSuspending(object sender, SuspendingEventArgs suspendingEventArgs)
        {
            var deferral = suspendingEventArgs.SuspendingOperation.GetDeferral();

            Debug.WriteLine("MainPage.OnSuspending()");

            Suspend();

            deferral.Complete();
        }

        void Suspend()
        {
            try
            {
                NotifyBackground("suspend", _id);

                BackgroundId = null;

                BackgroundMediaPlayer.MessageReceivedFromBackground -= OnMessageReceivedFromBackground;

                _backgroundRunningCompletionSource.TrySetCanceled();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MainPage.Suspend() failed: " + ex.Message);
            }
        }

        void OnMessageReceivedFromBackground(object sender, MediaPlayerDataReceivedEventArgs mediaPlayerDataReceivedEventArgs)
        {
            Debug.WriteLine("MainPage.OnMessageReceivedFromBackground()");

            foreach (var kv in mediaPlayerDataReceivedEventArgs.Data)
            {
                Debug.WriteLine(" b->f {0}: {1}", kv.Key, kv.Value);

                try
                {
                    switch (kv.Key.ToLowerInvariant())
                    {
                        case "ping":
                            NotifyBackground("pong", _id);
                            break;
                        case "pong":
                        case "start":
                            var backgroundIdValue = kv.Value as Guid?;

                            if (backgroundIdValue.HasValue)
                            {
                                var backgroundId = backgroundIdValue.Value;
                                var mediaPlayer = BackgroundMediaPlayer.Current;

                                var awaiter = Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                    () =>
                                    {
                                        MediaPlayer = mediaPlayer;
                                        BackgroundId = backgroundId;
                                    });
                            }

                            break;
                        case "track":
                            _trackName = kv.Value as string;
                            RequestRefresh();
                            break;
                        case "fail":
                            {
                                var awaiter = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => MediaPlayer = null);
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("MainPage.OnMessageReceivedFromBackground() failed: " + ex.Message);
                }
            }
        }

        void OnCurrentStateChanged(MediaPlayer sender, object args)
        {
            Debug.WriteLine("MainPage.OnCurrentStateChanged()");

            RequestRefresh();
        }

        void RefreshUi()
        {
            Debug.WriteLine("MainPage.RefreshUi() " + _id);

            Debug.Assert(Dispatcher.HasThreadAccess, "RefreshUi requires the dispatcher thread");

            while (0 != Interlocked.Exchange(ref _refreshPending, 0))
            {
                try
                {
                    var mediaPlayer = MediaPlayer;

                    if (null == mediaPlayer)
                    {
                        RefreshUi(MediaPlayerState.Closed, null);
                        return;
                    }

                    RefreshUi(mediaPlayer.CurrentState, _trackName);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("MainPage.RefreshUi() failed: " + ex.Message);
                }
            }
        }

        void RefreshUi(MediaPlayerState currentState, string track)
        {
            Debug.WriteLine("MainPage.RefreshUi({0}, {1}) {2}", currentState, track, _id);

            txtCurrentTrack.Text = track ?? string.Empty;
            txtCurrentState.Text = currentState.ToString();

            playButton.Content = MediaPlayerState.Playing == currentState ? "| |" : ">";

            if (MediaPlayerState.Opening == currentState)
            {
                prevButton.IsEnabled = false;
                playButton.IsEnabled = false;
                nextButton.IsEnabled = false;
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

        void NotifyBackground(string key, object value = null, bool ping = false)
        {
            Debug.WriteLine("MainPage.NotifyBackground() " + _id);

            try
            {
                var message = new ValueSet { { key, value }, { "Id", _id } };

                if (ping)
                    message.Add("ping", _id);

                BackgroundMediaPlayer.SendMessageToBackground(message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MainPage.NotifyBackground() failed: " + ex.Message);
                MediaPlayer = null;
            }
        }

        #region Button Click Event Handlers

        /// <summary>
        ///     Sends message to the background task to skip to the previous track.
        /// </summary>
        void prevButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("MainPage prev");

            NotifyBackground("smtc", SystemMediaTransportControlsButton.Previous.ToString());

            prevButton.IsEnabled = false;
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
                        case MediaPlayerState.Closed:
                            break;
                    }
                }

                await StartAudioAsync();
            }
            catch (OperationCanceledException)
            {
                MediaPlayer = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MainPage playButton Click failed: " + ex.Message);
                MediaPlayer = null;
            }
            finally
            {
                playButton.IsEnabled = true;
            }
        }

        async Task StartAudioAsync()
        {
            if (!IsRunning)
                MediaPlayer = BackgroundMediaPlayer.Current;

            var task = _backgroundRunningCompletionSource.Task;

            if (!task.IsCompleted)
            {
                try
                {
                    if (task != await Task.WhenAny(task, Task.Delay(2000)).ConfigureAwait(false))
                        throw new TimeoutException("Background task did not start");
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            NotifyBackground("smtc", SystemMediaTransportControlsButton.Play.ToString());
        }

        void nextButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("MainPage click");

            NotifyBackground("smtc", SystemMediaTransportControlsButton.Next.ToString());

            nextButton.IsEnabled = false;
        }

        void killButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("MainPage kill");

            NotifyBackground("smtc", SystemMediaTransportControlsButton.Stop.ToString());
            MediaPlayer = null;

            //BackgroundMediaPlayer.Shutdown();
        }

        #endregion Button Click Event Handlers
    }
}

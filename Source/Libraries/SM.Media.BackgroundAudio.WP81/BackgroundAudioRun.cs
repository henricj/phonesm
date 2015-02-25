// -----------------------------------------------------------------------
//  <copyright file="BackgroundAudioRun.cs" company="Henric Jungheim">
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
using Windows.ApplicationModel.Background;
using Windows.Foundation.Collections;
using Windows.Media;
using Windows.Media.Playback;
using Windows.System;
using SM.Media.Utility;

namespace SM.Media.BackgroundAudio
{
    sealed class BackgroundAudioRun : IDisposable
    {
        readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        readonly TaskCompletionSource<object> _completionSource = new TaskCompletionSource<object>();
        readonly ForegroundNotifier _foregroundNotifier;
        readonly Guid _id;
        readonly AsyncManualResetEvent _isRunningEvent = new AsyncManualResetEvent();
        Guid _appId;
        MediaPlayerManager _mediaPlayerManager;
        SystemMediaTransportControls _systemMediaTransportControls;

        public BackgroundAudioRun(Guid id)
        {
            _id = id;
            _foregroundNotifier = new ForegroundNotifier(id);
        }

        #region IDisposable Members

        public void Dispose()
        {
            Debug.WriteLine("BackgroundAudioRun.Dispose() " + _id);

            if (null != _mediaPlayerManager)
                Debug.WriteLine("BackgroundAudioRun.Dispose() " + _id + ": _mediaPlayerManager is not null");
        }

        #endregion

        public async Task ExecuteAsync()
        {
            try
            {
                _systemMediaTransportControls = SystemMediaTransportControls.GetForCurrentView();

                var smtc = _systemMediaTransportControls;

                var isOk = false;

                var mediaPlayer = BackgroundMediaPlayer.Current;

                try
                {
                    mediaPlayer.CurrentStateChanged += CurrentOnCurrentStateChanged;
                    BackgroundMediaPlayer.MessageReceivedFromForeground += BackgroundMediaPlayerOnMessageReceivedFromForeground;

                    isOk = true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("BackgroundAudioRun.Run initialization failed: " + ex.ExtendedMessage());
                }

                MediaPlayerManager mediaPlayerManager = null;

                if (isOk)
                {
                    try
                    {
                        mediaPlayerManager = new MediaPlayerManager(mediaPlayer, _cancellationTokenSource.Token);

                        mediaPlayerManager.TrackChanged += MediaPlayerManagerOnTrackChanged;
                        mediaPlayerManager.Failed += MediaPlayerManagerOnFailed;

                        _mediaPlayerManager = mediaPlayerManager;

                        smtc.ButtonPressed += SystemMediaTransportControlsOnButtonPressed;
                        smtc.PropertyChanged += SystemMediaTransportControlsOnPropertyChanged;

                        smtc.IsEnabled = true;
                        smtc.IsPauseEnabled = true;
                        smtc.IsPlayEnabled = true;
                        smtc.IsNextEnabled = true;
                        smtc.IsPreviousEnabled = true;

                        _foregroundNotifier.Notify("start", _id);

                        _isRunningEvent.Set();

                        await _completionSource.Task.ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("BackgroundAudioRun.ExecuteAsync() playback failed: " + ex.ExtendedMessage());
                    }
                }

                _mediaPlayerManager = null;

                _isRunningEvent.Reset();

                smtc.PropertyChanged -= SystemMediaTransportControlsOnPropertyChanged;
                smtc.ButtonPressed -= SystemMediaTransportControlsOnButtonPressed;

                if (null != mediaPlayerManager)
                {
                    mediaPlayerManager.TrackChanged -= MediaPlayerManagerOnTrackChanged;
                    mediaPlayerManager.Failed -= MediaPlayerManagerOnFailed;

                    await mediaPlayerManager.CloseAsync().ConfigureAwait(false);

                    mediaPlayerManager.Dispose();
                }

                BackgroundMediaPlayer.MessageReceivedFromForeground -= BackgroundMediaPlayerOnMessageReceivedFromForeground;
                mediaPlayer.CurrentStateChanged -= CurrentOnCurrentStateChanged;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("BackgroundAudioRun.ExecuteAsync() failed: " + ex.ExtendedMessage());
            }
        }

        public void Cancel()
        {
            _cancellationTokenSource.Cancel();
        }

        public void OnCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            Debug.WriteLine("BackgroundAudioRun.OnCanceled() " + _id + " reason " + reason);

            _completionSource.TrySetResult(null);

            try
            {
                Cancel();

                BackgroundMediaPlayer.Shutdown();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("BackgroundAudioRun.OnCanceled() failed: " + ex.ExtendedMessage());
            }
        }

        public void OnTaskCompleted(BackgroundTaskRegistration sender, BackgroundTaskCompletedEventArgs args)
        {
            Debug.WriteLine("BackgroundAudioRun.TaskOnCompleted() " + _id);

            try
            {
                args.CheckResult();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("BackgroundAudioRun.TaskOnCompleted() " + _id + " failed: " + ex.Message);
            }

            _completionSource.TrySetResult(null);
        }

        void MediaPlayerManagerOnFailed(MediaPlayerManager sender, string message)
        {
            Debug.WriteLine("BackgroundAudioRun.MediaPlayerManagerOnFailed() " + _id + " exception " + message);

            try
            {
                var smtc = _systemMediaTransportControls;

                smtc.PlaybackStatus = MediaPlaybackStatus.Stopped;
                smtc.DisplayUpdater.ClearAll();
                smtc.DisplayUpdater.Type = MediaPlaybackType.Music;
                smtc.DisplayUpdater.Update();

                var valueSet = new ValueSet
                {
                    { "track", _mediaPlayerManager.TrackName }
                };

                if (!string.IsNullOrEmpty(message))
                    valueSet["fail"] = message;

                _foregroundNotifier.Notify(valueSet);
            }
            catch (Exception ex2)
            {
                Debug.WriteLine("BackgroundAudioRun.MediaPlayerManagerOnFailed() failed: " + ex2.ExtendedMessage());
            }
        }

        void MediaPlayerManagerOnTrackChanged(MediaPlayerManager sender, string trackName)
        {
            Debug.WriteLine("BackgroundAudioRun.MediaPlayerManagerOnTrackChanged() " + _id + " track " + trackName);

            try
            {
                var smtc = _systemMediaTransportControls;

                smtc.PlaybackStatus = MediaPlaybackStatus.Playing;

                if (string.IsNullOrWhiteSpace(trackName))
                {
                    smtc.DisplayUpdater.ClearAll();
                    smtc.DisplayUpdater.Type = MediaPlaybackType.Music;
                }
                else
                {
                    smtc.DisplayUpdater.Type = MediaPlaybackType.Music;
                    smtc.DisplayUpdater.MusicProperties.Title = trackName;
                }

                smtc.DisplayUpdater.Update();

                _foregroundNotifier.Notify("track", trackName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("BackgroundAudioRun.MediaPlayerManagerOnTrackChanged() failed: " + ex.ExtendedMessage());
            }
        }

        void BackgroundMediaPlayerOnMessageReceivedFromForeground(object sender, MediaPlayerDataReceivedEventArgs mediaPlayerDataReceivedEventArgs)
        {
            //Debug.WriteLine("BackgroundAudioRun.BackgroundMediaPlayerOnMessageReceivedFromForeground() " + _id);

            object idValue;
            if (mediaPlayerDataReceivedEventArgs.Data.TryGetValue("id", out idValue))
            {
                var id = idValue as Guid?;

                if (id.HasValue && id.Value != _id)
                {
                    Debug.WriteLine("BackgroundAudioRun.BackgroundMediaPlayerOnMessageReceivedFromForeground() " + _id);
                    return;
                }
            }

            foreach (var kv in mediaPlayerDataReceivedEventArgs.Data)
            {
                //Debug.WriteLine(" f->b {0}: {1}", kv.Key, kv.Value);

                if (null == kv.Key)
                {
                    Debug.WriteLine("*** BackgroundAudioRun.BackgroundMediaPlayerOnMessageReceivedFromForeground() null key");

                    continue; // This does happen.  It shouldn't, but it does.
                }

                try
                {
                    switch (kv.Key.ToLowerInvariant())
                    {
                        case "resume":
                            var appId = kv.Value as Guid?;
                            if (appId.HasValue)
                                _appId = appId.Value;
                            break;
                        case "suspend":
                            break;
                        case "ping":
                            _foregroundNotifier.Notify("pong", _id);
                            break;
                        case "smtc":
                            SystemMediaTransportControlsButton button;

                            if (Enum.TryParse((string)kv.Value, true, out button))
                            {
                                var mediaPlayerManager = _mediaPlayerManager;

                                if (null != mediaPlayerManager)
                                    HandleSmtcButton(mediaPlayerManager, button);
                            }
                            break;
                        case "memory":
                            NotifyForegroundMemory();
                            break;
                        case "gc":
                            var task = Task.Run(() =>
                            {
                                MemoryDiagnostics.DumpMemory();
                                Debug.WriteLine("Force GC: {0:F2}MiB", GC.GetTotalMemory(false).BytesToMiB());

                                GC.Collect();
                                GC.WaitForPendingFinalizers();
                                GC.Collect();

                                Debug.WriteLine("Forced GC: {0:F2}MiB", GC.GetTotalMemory(true).BytesToMiB());
                                MemoryDiagnostics.DumpMemory();

                                NotifyForegroundMemory();
                            });

                            TaskCollector.Default.Add(task, "Forced GC");

                            break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("BackgroundAudioRun.BackgroundMediaPlayerOnMessageReceivedFromForeground() " + _id + " failed: " + ex.Message);
                }
            }
        }

        void NotifyForegroundMemory()
        {
            _foregroundNotifier.Notify(new ValueSet
            {
                { "memory", GC.GetTotalMemory(false) },
                { "appMemory", MemoryManager.AppMemoryUsage },
                { "appMemoryLimit", MemoryManager.AppMemoryUsageLimit },
            });
        }

        void CurrentOnCurrentStateChanged(MediaPlayer sender, object args)
        {
            Debug.WriteLine("BackgroundAudioRun.CurrentOnCurrentStateChanged() " + _id + " state " + sender.CurrentState);

            switch (sender.CurrentState)
            {
                case MediaPlayerState.Playing:
                    _systemMediaTransportControls.PlaybackStatus = MediaPlaybackStatus.Playing;
                    break;
                case MediaPlayerState.Paused:
                    _systemMediaTransportControls.PlaybackStatus = MediaPlaybackStatus.Paused;
                    break;
            }
        }

        void SystemMediaTransportControlsOnButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            Debug.WriteLine("BackgroundAudioRun.SystemMediaTransportControlsOnButtonPressed() " + _id + " button " + args.Button);

            var mediaPlayerManager = _mediaPlayerManager;

            if (null == mediaPlayerManager)
                return;

            HandleSmtcButton(mediaPlayerManager, args.Button);
        }

        void HandleSmtcButton(MediaPlayerManager mediaPlayerManager, SystemMediaTransportControlsButton button)
        {
            Debug.WriteLine("BackgroundAudioRun.HandleSmtcButton() " + _id + " button " + button);

            try
            {
                switch (button)
                {
                    case SystemMediaTransportControlsButton.Play:
                        mediaPlayerManager.Play();
                        break;
                    case SystemMediaTransportControlsButton.Pause:
                        mediaPlayerManager.Pause();
                        break;
                    case SystemMediaTransportControlsButton.Next:
                        _systemMediaTransportControls.PlaybackStatus = MediaPlaybackStatus.Changing;
                        mediaPlayerManager.Next();
                        break;
                    case SystemMediaTransportControlsButton.Previous:
                        _systemMediaTransportControls.PlaybackStatus = MediaPlaybackStatus.Changing;
                        mediaPlayerManager.Previous();
                        break;
                    case SystemMediaTransportControlsButton.Stop:
                        mediaPlayerManager.Stop();
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("BackgroundAudioRun.HandleSmtcButton() failed " + ex.ExtendedMessage());
            }
        }

        void SystemMediaTransportControlsOnPropertyChanged(SystemMediaTransportControls sender, SystemMediaTransportControlsPropertyChangedEventArgs args)
        {
            Debug.WriteLine("BackgroundAudioRun.SystemMediaTransportControlsOnPropertyChanged() " + _id);
        }
    }
}

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
        static readonly TimeSpan WatchdogTimeout = TimeSpan.FromSeconds(30);
        readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        readonly TaskCompletionSource<object> _completionSource = new TaskCompletionSource<object>();
        readonly ForegroundNotifier _foregroundNotifier;
        readonly Guid _id;
        readonly ValueSetWorkerQueue _notificationQueue;
        readonly Timer _timer;
        readonly Timer _watchdogTimer;
        Guid? _appId;
        TaskCompletionSource<Guid> _challengeCompletionSource;
        Guid? _challengeToken;
        MediaPlayerManager _mediaPlayerManager;
        MetadataHandler _metadataHandler;
        TimeSpan _nextEvent;
        SystemMediaTransportControls _systemMediaTransportControls;
        int _watchdogBarks;

        public BackgroundAudioRun(Guid id)
        {
            _id = id;
            _foregroundNotifier = new ForegroundNotifier(id);
            _timer = new Timer(_ =>
            {
                var metadataHandler = _metadataHandler;

                if (null == metadataHandler)
                    return;

                metadataHandler.Refresh();
            }, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

            _notificationQueue = new ValueSetWorkerQueue(vs =>
            {
                HandleNotification(vs);

                return TplTaskExtensions.CompletedTask;
            });

            _watchdogTimer = new Timer(
                _ =>
                {
                    Debug.WriteLine("BackgroundAudioRun watchdog");

                    var barks = 1;

                    try
                    {
                        var foregroundId = BackgroundSettings.ForegroundId;

                        if (!foregroundId.HasValue || _completionSource.Task.IsCompleted)
                        {
                            Interlocked.Exchange(ref _watchdogBarks, 0);

                            StopWatchdog();

                            return;
                        }

                        barks = Interlocked.Increment(ref _watchdogBarks);

                        if (barks > 3)
                        {
                            Debug.WriteLine("BackgroundAudioRun watchdog exiting");

                            _completionSource.TrySetCanceled();

                            Cancel();

                            return;
                        }

                        _foregroundNotifier.Notify(BackgroundNotificationType.Ping);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("BackgroundAudioRun watchdog failed: " + ex.ExtendedMessage());
                    }

                    RequestWatchdog(TimeSpan.FromTicks(WatchdogTimeout.Ticks >> barks));
                },
                null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        #region IDisposable Members

        public void Dispose()
        {
            Debug.WriteLine("BackgroundAudioRun.Dispose() " + _id);

            if (null != _mediaPlayerManager)
                Debug.WriteLine("BackgroundAudioRun.Dispose() " + _id + ": _mediaPlayerManager is not null");

            _timer.Dispose();

            _watchdogTimer.Dispose();
        }

        #endregion

        public async Task ExecuteAsync()
        {
            Debug.WriteLine("BackgroundAudioRun.ExecuteAsync()");

            try
            {
                _systemMediaTransportControls = SystemMediaTransportControls.GetForCurrentView();

                var smtc = _systemMediaTransportControls;

                var isOk = false;

                BackgroundMediaPlayer.MessageReceivedFromForeground += BackgroundMediaPlayerOnMessageReceivedFromForeground;

                var mediaPlayer = BackgroundMediaPlayer.Current;

                _metadataHandler = new MetadataHandler(_systemMediaTransportControls, _foregroundNotifier,
                    () => mediaPlayer.Position,
                    position => UpdateMediaPlayerEvents(mediaPlayer, position),
                    _cancellationTokenSource.Token);

                try
                {
                    mediaPlayer.CurrentStateChanged += OnCurrentStateChanged;
                    mediaPlayer.PlaybackMediaMarkerReached += OnPlaybackMediaMarkerReached;

                    BackgroundSettings.SetBackgroundId(_id);

                    isOk = true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("BackgroundAudioRun.ExecuteAsync() initialization failed: " + ex.ExtendedMessage());
                }

                MediaPlayerManager mediaPlayerManager = null;

                if (isOk)
                {
                    try
                    {
                        mediaPlayerManager = new MediaPlayerManager(mediaPlayer, _metadataHandler, _cancellationTokenSource.Token);

                        mediaPlayerManager.TrackChanged += MediaPlayerManagerOnTrackChanged;
                        mediaPlayerManager.Failed += MediaPlayerManagerOnFailed;
                        mediaPlayerManager.Ended += MediaPlayerManagerOnEnded;

                        _mediaPlayerManager = mediaPlayerManager;

                        smtc.ButtonPressed += SystemMediaTransportControlsOnButtonPressed;
                        smtc.PropertyChanged += SystemMediaTransportControlsOnPropertyChanged;

                        smtc.IsEnabled = true;
                        smtc.IsPauseEnabled = true;
                        smtc.IsPlayEnabled = true;
                        smtc.IsNextEnabled = true;
                        smtc.IsPreviousEnabled = true;

                        SyncNotification();

                        Debug.WriteLine("BackgroundAudioRun.ExecuteAsync() running");

                        await _completionSource.Task.ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("BackgroundAudioRun.ExecuteAsync() playback failed: " + ex.ExtendedMessage());
                    }
                }

                Debug.WriteLine("BackgroundAudioRun.ExecuteAsync() done running");

                BackgroundSettings.RemoveBackgroundId(_id);

                try
                {
                    _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                }
                catch (Exception)
                {
                    // Guard against race with cleanup
                }

                _mediaPlayerManager = null;

                smtc.PropertyChanged -= SystemMediaTransportControlsOnPropertyChanged;
                smtc.ButtonPressed -= SystemMediaTransportControlsOnButtonPressed;

                if (null != mediaPlayerManager)
                {
                    mediaPlayerManager.TrackChanged -= MediaPlayerManagerOnTrackChanged;
                    mediaPlayerManager.Failed -= MediaPlayerManagerOnFailed;
                    mediaPlayerManager.Ended -= MediaPlayerManagerOnEnded;

                    await mediaPlayerManager.CloseAsync().ConfigureAwait(false);

                    mediaPlayerManager.Dispose();
                }

                if (_appId.HasValue)
                    _foregroundNotifier.Notify(BackgroundNotificationType.Stop);

                mediaPlayer.CurrentStateChanged -= OnCurrentStateChanged;
                mediaPlayer.PlaybackMediaMarkerReached -= OnPlaybackMediaMarkerReached;
                BackgroundMediaPlayer.MessageReceivedFromForeground -= BackgroundMediaPlayerOnMessageReceivedFromForeground;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("BackgroundAudioRun.ExecuteAsync() failed: " + ex.ExtendedMessage());
            }
            finally
            {
                Debug.WriteLine("BackgroundAudioRun.ExecuteAsync() completed");
            }
        }

        void SyncNotification()
        {
            Debug.WriteLine("BackgroundAudioRun.SyncNotification()");

            var foregroundId = BackgroundSettings.ForegroundId;

            if (!foregroundId.HasValue)
                return;

            _challengeCompletionSource = new TaskCompletionSource<Guid>();
            _challengeToken = Guid.NewGuid();

            Debug.WriteLine("BackgroundAudioRun.SyncNotification() sending start to foreground");

            _foregroundNotifier.Notify(BackgroundNotificationType.Start);

            _foregroundNotifier.Notify(BackgroundNotificationType.Ping, _challengeToken);

            _watchdogTimer.Change(WatchdogTimeout, Timeout.InfiniteTimeSpan);
        }

        void OnPlaybackMediaMarkerReached(MediaPlayer sender, PlaybackMediaMarkerReachedEventArgs args)
        {
            Debug.WriteLine("BackgroundAudioRun.OnPlaybackMediaMarkerReached() " + args.PlaybackMediaMarker.Time);

            _metadataHandler.Refresh();
        }

        void UpdateMediaPlayerEvents(MediaPlayer mediaPlayer, TimeSpan position)
        {
            Debug.WriteLine("BackgroundAudioRun.UpdateMediaPlayerEvents() " + position);

            // How can one get the PlaybackMediaMarkerReached event to fire?
            //mediaPlayer.PlaybackMediaMarkers.Clear();
            //mediaPlayer.PlaybackMediaMarkers.Insert(new PlaybackMediaMarker(position));

            _nextEvent = position;

            UpdateTimer(mediaPlayer);
        }

        void UpdateTimer(MediaPlayer mediaPlayer)
        {
            Debug.WriteLine("BackgroundAudioRun.UpdateTimer()");

            try
            {
                var playerPosition = mediaPlayer.Position;

                var nextEvent = _nextEvent;

                if (nextEvent <= playerPosition)
                {
                    _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                    _metadataHandler.Refresh();

                    return;
                }

                var state = mediaPlayer.CurrentState;

                if (MediaPlayerState.Playing == state)
                    _timer.Change(nextEvent - playerPosition + TimeSpan.FromSeconds(0.5), Timeout.InfiniteTimeSpan);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("BackgroundAudioRun.UpdateTimer() failed: " + ex.ExtendedMessage());
            }
        }

        public void Cancel()
        {
            _cancellationTokenSource.Cancel();
        }

        public void OnCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            Debug.WriteLine("BackgroundAudioRun.OnCanceled() " + _id + " reason " + reason);

            try
            {
                var mediaPlayerManager = _mediaPlayerManager;

                if (null == mediaPlayerManager)
                    BackgroundSettings.Position = null;
                else
                {
                    var mediaPlayer = _mediaPlayerManager.MediaPlayer;

                    if (null != mediaPlayer && mediaPlayer.CanSeek)
                    {
                        var position = mediaPlayer.Position;

                        BackgroundSettings.Position = position;
                    }
                    else
                        BackgroundSettings.Position = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("BackgroundAudioRun.OnCanceled() position store failed: " + ex.ExtendedMessage());
            }

            _completionSource.TrySetResult(null);

            try
            {
                Cancel();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("BackgroundAudioRun.OnCanceled() cancel failed: " + ex.ExtendedMessage());
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

        void MediaPlayerManagerOnFailed(object o, string message)
        {
            Debug.WriteLine("BackgroundAudioRun.MediaPlayerManagerOnFailed() " + _id + " exception " + message);

            try
            {
                _metadataHandler.Reset();

                var smtc = _systemMediaTransportControls;

                smtc.PlaybackStatus = MediaPlaybackStatus.Stopped;

                var valueSet = new ValueSet();

                if (null != _mediaPlayerManager)
                    valueSet.Add(BackgroundNotificationType.Track, _mediaPlayerManager.TrackName);

                if (!string.IsNullOrEmpty(message))
                    valueSet.Add(BackgroundNotificationType.Fail);

                if (_appId.HasValue)
                    _foregroundNotifier.Notify(valueSet);
            }
            catch (Exception ex2)
            {
                Debug.WriteLine("BackgroundAudioRun.MediaPlayerManagerOnFailed() failed: " + ex2.ExtendedMessage());
            }

            _completionSource.TrySetResult(null);
        }

        void MediaPlayerManagerOnEnded(object o, object args)
        {
            Debug.WriteLine("BackgroundAudioRun.MediaPlayerManagerOnEnded() " + _id);
        }

        void MediaPlayerManagerOnTrackChanged(object obj, string trackName)
        {
            Debug.WriteLine("BackgroundAudioRun.MediaPlayerManagerOnTrackChanged() " + _id + " track " + trackName);

            try
            {
                _metadataHandler.DefaultTitle = trackName;

                _metadataHandler.Refresh();

                if (_appId.HasValue)
                    _foregroundNotifier.Notify(BackgroundNotificationType.Track, trackName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("BackgroundAudioRun.MediaPlayerManagerOnTrackChanged() failed: " + ex.ExtendedMessage());
            }
        }

        void BackgroundMediaPlayerOnMessageReceivedFromForeground(object sender, MediaPlayerDataReceivedEventArgs mediaPlayerDataReceivedEventArgs)
        {
            //Debug.WriteLine("BackgroundAudioRun.BackgroundMediaPlayerOnMessageReceivedFromForeground() " + _id);

            if (_completionSource.Task.IsCompleted)
                return;

            _notificationQueue.Submit(mediaPlayerDataReceivedEventArgs.Data);
        }

        void HandleNotification(ValueSet valueSet)
        {
            //Debug.WriteLine("BackgroundAudioRun.HandleNotification() " + _id);

            if (_completionSource.Task.IsCompleted)
                return;

            try
            {
                object idValue;

                if (valueSet.TryGetValue(BackgroundNotificationType.Id, out idValue))
                {
                    var id = idValue as Guid?;

                    if (id.HasValue && _appId.HasValue && id.Value != _appId.Value)
                    {
                        Debug.WriteLine("BackgroundAudioRun.HandleNotification() " + _id + " != " + id.Value);
                        return;
                    }
                }
                else
                {
                    Debug.WriteLine("BackgroundAudioRun.HandleNotification() no id " + _id);
                    return;
                }

                Guid? appId = null;
                var isStart = false;
                var isStop = false;

                foreach (var kv in valueSet)
                {
                    //Debug.WriteLine(" f->b {0}: {1}", kv.Key, kv.Value);

                    if (null == kv.Key)
                    {
                        Debug.WriteLine("*** BackgroundAudioRun.HandleNotification() null key");

                        continue; // This does happen.  It shouldn't, but it does.
                    }

                    BackgroundNotificationType type;
                    if (!Enum.TryParse(kv.Key, true, out type))
                        continue;

                    switch (type)
                    {
                        case BackgroundNotificationType.Start:
                        case BackgroundNotificationType.Resume:
                            isStart = true;
                            break;
                        case BackgroundNotificationType.Stop:
                        case BackgroundNotificationType.Suspend:
                            isStop = true;
                            break;
                        case BackgroundNotificationType.Ping:
                            if (_appId.HasValue)
                                _foregroundNotifier.Notify(BackgroundNotificationType.Pong, kv.Value);
                            break;
                        case BackgroundNotificationType.Pong:
                        {
                            var challenge = kv.Value as Guid?;

                            if (challenge.HasValue && challenge.Value == _challengeToken.Value)
                                _challengeCompletionSource.TrySetResult(challenge.Value);
                        }
                            break;
                        case BackgroundNotificationType.Smtc:
                            if (_appId.HasValue)
                            {
                                SystemMediaTransportControlsButton button;
                                if (Enum.TryParse((string)kv.Value, true, out button))
                                {
                                    var mediaPlayerManager = _mediaPlayerManager;

                                    if (null != mediaPlayerManager)
                                        HandleSmtcButton(mediaPlayerManager, button);
                                }
                            }
                            break;
                        case BackgroundNotificationType.Memory:
                            NotifyForegroundMemory();
                            break;
                        case BackgroundNotificationType.Gc:
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
                        case BackgroundNotificationType.Id:
                            var id = kv.Value as Guid?;

                            if (id.HasValue)
                                appId = id;

                            break;
                    }
                }

                if (!appId.HasValue)
                    return;

                if (_appId.HasValue)
                {
                    if (appId.Value != _appId.Value)
                        return;

                    if (isStop)
                        _appId = null;
                }
                else
                {
                    if (isStart)
                        _appId = appId.Value;
                    else
                        return;
                }

                if (isStop)
                    StopWatchdog();
                else if (!isStart)
                    ResetWatchdog();

                if (isStart)
                    SyncNotification();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("BackgroundAudioRun.HandleNotification() " + _id + " failed: " + ex.Message);
            }
        }

        void NotifyForegroundMemory()
        {
            _foregroundNotifier.Notify(new ValueSet
            {
                { BackgroundNotificationType.Memory.ToString(), GC.GetTotalMemory(false) },
                { BackgroundNotificationType.AppMemory.ToString(), MemoryManager.AppMemoryUsage },
                { BackgroundNotificationType.AppMemoryLimit.ToString(), MemoryManager.AppMemoryUsageLimit },
            });
        }

        void OnCurrentStateChanged(MediaPlayer sender, object args)
        {
            Debug.WriteLine("BackgroundAudioRun.OnCurrentStateChanged() " + _id + " state " + sender.CurrentState);

            switch (sender.CurrentState)
            {
                case MediaPlayerState.Playing:
                    _systemMediaTransportControls.PlaybackStatus = MediaPlaybackStatus.Playing;
                    UpdateTimer(sender);
                    break;
                case MediaPlayerState.Paused:
                    _systemMediaTransportControls.PlaybackStatus = MediaPlaybackStatus.Paused;
                    break;
                case MediaPlayerState.Opening:
                    _systemMediaTransportControls.PlaybackStatus = MediaPlaybackStatus.Changing;
                    break;
                case MediaPlayerState.Buffering:
                    _systemMediaTransportControls.PlaybackStatus = MediaPlaybackStatus.Playing;
                    break;
                case MediaPlayerState.Stopped:
                    _systemMediaTransportControls.PlaybackStatus = MediaPlaybackStatus.Stopped;
                    break;
                case MediaPlayerState.Closed:
                    _systemMediaTransportControls.PlaybackStatus = MediaPlaybackStatus.Closed;
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

        void StopWatchdog()
        {
            Debug.WriteLine("BackgroundAudioRun.StopWatchdog() " + _id);

            Interlocked.Exchange(ref _watchdogBarks, 0);

            _watchdogTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        void ResetWatchdog()
        {
            //Debug.WriteLine("BackgroundAudioRun.ResetWatchdog() for " + timeout + " " + _id);

            Interlocked.Exchange(ref _watchdogBarks, 0);

            RequestWatchdog(WatchdogTimeout);
        }

        void RequestWatchdog(TimeSpan timeout)
        {
            //Debug.WriteLine("BackgroundAudioRun.RequestWatchdog() for " + timeout + " " + _id);

            _watchdogTimer.Change(timeout, Timeout.InfiniteTimeSpan);
        }
    }
}

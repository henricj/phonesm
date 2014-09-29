// -----------------------------------------------------------------------
//  <copyright file="SmMediaBackgroundAudioTask.cs" company="Henric Jungheim">
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
using Windows.ApplicationModel.Background;
using Windows.Foundation.Collections;
using Windows.Media;
using Windows.Media.Playback;
using Windows.System;
using SM.Media.Utility;

namespace SM.Media.BackgroundAudio
{
    public sealed class SmMediaBackgroundAudioTask : IBackgroundTask
    {
        readonly AsyncManualResetEvent _isRunningEvent = new AsyncManualResetEvent();
        BackgroundTaskDeferral _deferral;
        Guid _id;
        MediaPlayerManager _mediaPlayerManager;
        SystemMediaTransportControls _systemMediaTransportControls;
        Guid _appId;

#if DEBUG
        readonly Timer _memoryPoll = new Timer(
            _ => Debug.WriteLine("<{0:F}MiB/{1:F}MiB>",
                MemoryManager.AppMemoryUsage.BytesToMiB(),
                MemoryManager.AppMemoryUsageLimit.BytesToMiB()),
                null, Timeout.Infinite, Timeout.Infinite);
#endif

        [Conditional("DEBUG")]
        void StartPoll()
        {
#if DEBUG
            _memoryPoll.Change(TimeSpan.Zero, TimeSpan.FromSeconds(15));
#endif
        }

        [Conditional("DEBUG")]
        void StopPoll()
        {
#if DEBUG
            _memoryPoll.Change(Timeout.Infinite, Timeout.Infinite);
#endif
        }


        #region IBackgroundTask Members

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            _id = taskInstance.InstanceId;

            Debug.WriteLine("SmMediaBackgroundAudioTask.Run() " + taskInstance.Task.Name + " instance " + _id);

            _deferral = taskInstance.GetDeferral();

            _systemMediaTransportControls = SystemMediaTransportControls.GetForCurrentView();

            var smtc = _systemMediaTransportControls;

            smtc.ButtonPressed += SystemMediaTransportControlsOnButtonPressed;
            smtc.PropertyChanged += SystemMediaTransportControlsOnPropertyChanged;

            smtc.IsEnabled = true;
            smtc.IsPauseEnabled = true;
            smtc.IsPlayEnabled = true;
            smtc.IsNextEnabled = true;
            smtc.IsPreviousEnabled = true;

            taskInstance.Canceled += TaskInstanceOnCanceled;
            taskInstance.Task.Completed += TaskOnCompleted;

            BackgroundMediaPlayer.Current.CurrentStateChanged += CurrentOnCurrentStateChanged;
            BackgroundMediaPlayer.MessageReceivedFromForeground += BackgroundMediaPlayerOnMessageReceivedFromForeground;

            _mediaPlayerManager = new MediaPlayerManager(BackgroundMediaPlayer.Current);

            _mediaPlayerManager.TrackChanged += MediaPlayerManagerOnTrackChanged;
            _mediaPlayerManager.Failed += MediaPlayerManagerOnFailed;

            NotifyForeground("start", _id);

            _isRunningEvent.Set();

            StartPoll();
        }

        void MediaPlayerManagerOnFailed(MediaPlayerManager sender, Exception ex)
        {
            var message = null == ex ? "<none>" : ex.Message;

            Debug.WriteLine("SmMediaBackgroundAudioTask.MediaPlayerManagerOnFailed() " + _id + " exception " + message);

            NotifyForeground("fail", message);

            Shutdown();
        }

        void MediaPlayerManagerOnTrackChanged(MediaPlayerManager sender, string trackName)
        {
            Debug.WriteLine("SmMediaBackgroundAudioTask.MediaPlayerManagerOnTrackChanged() " + _id + " track " + trackName);

            var smtc = _systemMediaTransportControls;

            smtc.PlaybackStatus = MediaPlaybackStatus.Playing;
            smtc.DisplayUpdater.Type = MediaPlaybackType.Music;
            smtc.DisplayUpdater.MusicProperties.Title = trackName;
            smtc.DisplayUpdater.Update();

            NotifyForeground("track", trackName);
        }

        #endregion

        void NotifyForeground(string key, object value = null)
        {
            Debug.WriteLine("SmMediaBackgroundAudioTask.NotifyForeground() " + _id);

            try
            {
                BackgroundMediaPlayer.SendMessageToForeground(new ValueSet { { key, value }, { "Id", _id } });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SmMediaBackgroundAudioTask.NotifyForeground() failed: " + ex.Message);
            }
        }

        void BackgroundMediaPlayerOnMessageReceivedFromForeground(object sender, MediaPlayerDataReceivedEventArgs mediaPlayerDataReceivedEventArgs)
        {
            Debug.WriteLine("SmMediaBackgroundAudioTask.BackgroundMediaPlayerOnMessageReceivedFromForeground() " + _id);

            object idValue;
            if (mediaPlayerDataReceivedEventArgs.Data.TryGetValue("id", out idValue))
            {
                var id = idValue as Guid?;

                if (id.HasValue && id.Value != _id)
                {
                    Debug.WriteLine("SmMediaBackgroundAudioTask.BackgroundMediaPlayerOnMessageReceivedFromForeground() " + _id);
                    return;
                }
            }

            foreach (var kv in mediaPlayerDataReceivedEventArgs.Data)
            {
                Debug.WriteLine(" f->b {0}: {1}", kv.Key, kv.Value);

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
                            NotifyForeground("pong", _id);
                            break;
                        case "smtc":
                            SystemMediaTransportControlsButton button;

                            if (Enum.TryParse((string)kv.Value, true, out button))
                            {
                                HandleSmtcButton(button);
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("SmMediaBackgroundAudioTask.BackgroundMediaPlayerOnMessageReceivedFromForeground() " + _id + " failed: " + ex.Message);
                }
            }
        }

        void CurrentOnCurrentStateChanged(MediaPlayer sender, object args)
        {
            Debug.WriteLine("SmMediaBackgroundAudioTask.CurrentOnCurrentStateChanged() " + _id + " state " + sender.CurrentState);

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

        void TaskOnCompleted(BackgroundTaskRegistration sender, BackgroundTaskCompletedEventArgs args)
        {
            Debug.WriteLine("SmMediaBackgroundAudioTask.TaskOnCompleted() " + _id);

            try
            {
                args.CheckResult();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SmMediaBackgroundAudioTask.TaskOnCompleted() " + _id + " failed: " + ex.Message);
            }

            StopPoll();

            _deferral.Complete();

            var mpm = _mediaPlayerManager;
            if (null != mpm)
            {
                _mediaPlayerManager = null;
                mpm.Dispose();
            }
        }

        void TaskInstanceOnCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            Debug.WriteLine("SmMediaBackgroundAudioTask.TaskInstanceOnCanceled() " + _id + " reason " + reason);

            Shutdown();

            var mediaPlayerManager = _mediaPlayerManager;

            _mediaPlayerManager = null;

            mediaPlayerManager.DisposeSafe();

            _deferral.Complete();

            Debug.WriteLine("SmMediaBackgroundAudioTask.TaskInstanceOnCanceled() " + _id + " done");
        }

        void Shutdown()
        {
            Debug.WriteLine("SmMediaBackgroundAudioTask.Shutdown() " + _id);

            if (!_isRunningEvent.WaitAsync().IsCompleted)
                return;

            try
            {
                _systemMediaTransportControls.ButtonPressed -= SystemMediaTransportControlsOnButtonPressed;
                _systemMediaTransportControls.PropertyChanged -= SystemMediaTransportControlsOnPropertyChanged;

                _isRunningEvent.Reset();

                BackgroundMediaPlayer.Shutdown();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SmMediaBackgroundAudioTask.Shutdown() " + _id + " failed: " + ex.Message);
            }
        }

        async void SystemMediaTransportControlsOnButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            Debug.WriteLine("SmMediaBackgroundAudioTask.SystemMediaTransportControlsOnButtonPressed() " + _id + " button " + args.Button);

            var runningTask = _isRunningEvent.WaitAsync();
            if (!runningTask.IsCompleted && await Task.WhenAny(runningTask, Task.Delay(2000)) != runningTask)
                throw new TimeoutException("Audio is not running");

            HandleSmtcButton(args.Button);
        }

        void HandleSmtcButton(SystemMediaTransportControlsButton button)
        {
            Debug.WriteLine("SmMediaBackgroundAudioTask.HandleSmtcButton() " + _id + " button " + button);

            switch (button)
            {
                case SystemMediaTransportControlsButton.Play:
                    _mediaPlayerManager.Play();
                    break;
                case SystemMediaTransportControlsButton.Pause:
                    _mediaPlayerManager.Pause();
                    break;
                case SystemMediaTransportControlsButton.Next:
                    _systemMediaTransportControls.PlaybackStatus = MediaPlaybackStatus.Changing;
                    _mediaPlayerManager.Next();
                    break;
                case SystemMediaTransportControlsButton.Previous:
                    _systemMediaTransportControls.PlaybackStatus = MediaPlaybackStatus.Changing;
                    _mediaPlayerManager.Previous();
                    break;
                case SystemMediaTransportControlsButton.Stop:
                    _mediaPlayerManager.Stop();
                    break;
            }
        }

        void SystemMediaTransportControlsOnPropertyChanged(SystemMediaTransportControls sender, SystemMediaTransportControlsPropertyChangedEventArgs args)
        {
            Debug.WriteLine("SmMediaBackgroundAudioTask.SystemMediaTransportControlsOnPropertyChanged() " + _id);
        }
    }
}

// -----------------------------------------------------------------------
//  <copyright file="MediaPlayerHandle.cs" company="Henric Jungheim">
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
using Windows.Foundation.Collections;
using Windows.Media;
using Windows.Media.Playback;
using Windows.UI.Core;
using SM.Media.BackgroundAudio;
using SM.Media.Utility;

namespace BackgroundAudio.Sample
{
    sealed class MediaPlayerHandle : IDisposable
    {
        readonly AsyncLock _asyncLock = new AsyncLock();
        readonly CoreDispatcher _dispatcher;
        readonly Guid _id = Guid.NewGuid();
        readonly IBackgroundMediaNotifier _notifier;
        readonly BackgroundSubscriptionHandle _subscriptionHandle;
        MediaPlayerSession _mediaPlayerSession;

        public MediaPlayerHandle(CoreDispatcher dispatcher)
        {
            BackgroundSettings.RemoveForegroundId();

            _dispatcher = dispatcher;

            _notifier = new BackgroundNotifier(_id);

            _subscriptionHandle = new BackgroundSubscriptionHandle(OnMessageReceivedFromBackground);

            BackgroundSettings.SetForegroundId(_id);
        }

        public Guid Id
        {
            get { return _id; }
        }

        public MediaPlayer MediaPlayer
        {
            get
            {
                Debug.Assert(_dispatcher.HasThreadAccess, "MediaPlayer requires the dispatcher thread");

                if (!_subscriptionHandle.IsSubscribed)
                    return null;

                var mediaPlayerSession = _mediaPlayerSession;

                return null == mediaPlayerSession ? null : mediaPlayerSession.MediaPlayer;
            }
        }

        public bool IsRunning
        {
            get
            {
                Debug.Assert(_dispatcher.HasThreadAccess, "IsRunning requires the dispatcher thread");

                if (!_subscriptionHandle.IsSubscribed)
                    return false;

                var mediaPlayerSession = _mediaPlayerSession;

                return null != mediaPlayerSession && mediaPlayerSession.IsRunning;
            }
        }

        #region IDisposable Members

        public void Dispose()
        {
            _subscriptionHandle.Unsubscribe();

            BackgroundSettings.RemoveForegroundId(_id);

            Close();

            _asyncLock.Dispose();

            _subscriptionHandle.Dispose();
        }

        #endregion

        public event EventHandler<object> CurrentStateChanged;
        public event EventHandler<MediaPlayerDataReceivedEventArgs> MessageReceivedFromBackground;

        public async Task<MediaPlayerSession> OpenAsync()
        {
            Debug.WriteLine("MediaPlayerHandle.OpenAsync()");

            for (var shutdownRetry = 0; shutdownRetry < 2; ++shutdownRetry)
            {
                MediaPlayerSession mediaPlayerSession = null;

                for (var retry = 0; retry < 3; ++retry)
                {
                    using (await _asyncLock.LockAsync(CancellationToken.None))
                    {
                        mediaPlayerSession = _mediaPlayerSession;

                        if (null != mediaPlayerSession)
                            return mediaPlayerSession;

                        try
                        {
                            _subscriptionHandle.Subscribe();

                            var player = BackgroundMediaPlayer.Current;

                            Guid? backgroundId = null;

                            for (var loadRetry = 0; loadRetry < 4; ++loadRetry)
                            {
                                await Task.Delay(100 * (1 + loadRetry)).ConfigureAwait(false);

                                backgroundId = BackgroundSettings.BackgroundId;

                                if (backgroundId.HasValue)
                                    break;
                            }

                            if (backgroundId.HasValue)
                            {
                                mediaPlayerSession = new MediaPlayerSession(player, backgroundId.Value, _notifier, OnCurrentStateChanged);

                                _mediaPlayerSession = mediaPlayerSession;

                                if (await mediaPlayerSession.OpenAsync(OnCurrentStateChanged).ConfigureAwait(false))
                                    return mediaPlayerSession;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("MediaPlayerHandle.OpenAsync() failed: " + ex.ExtendedMessage());
                        }

                        _mediaPlayerSession = null;

                        _subscriptionHandle.Unsubscribe();
                    }

                    await Task.Delay(150 * (1 + retry)).ConfigureAwait(false);
                }

                if (null != mediaPlayerSession)
                    mediaPlayerSession.Dispose();

                Shutdown();

                await Task.Delay(450 * (1 + shutdownRetry)).ConfigureAwait(false);
            }

            return null;
        }

        public void Shutdown()
        {
            Debug.WriteLine("MediaPlayerHandle.Shutdown()");

            BackgroundSettings.RemoveBackgroundId();

            _subscriptionHandle.Unsubscribe();

            Close();

            BackgroundMediaPlayer.Shutdown();
        }

        async void OnMessageReceivedFromBackground(object sender, MediaPlayerDataReceivedEventArgs mediaPlayerDataReceivedEventArgs)
        {
            //Debug.WriteLine("MediaPlayerHandle.OnMessageReceivedFromBackground()");

            if (!_subscriptionHandle.IsSubscribed)
                return;

            Guid? backgroundId = null;
            object challenge = null;
            var stop = false;
            var start = false;

            foreach (var kv in mediaPlayerDataReceivedEventArgs.Data)
            {
                //Debug.WriteLine(" b->f {0}: {1}", kv.Key, kv.Value);

                try
                {
                    if (null == kv.Key)
                    {
                        Debug.WriteLine("*** MediaPlayerHandle.OnMessageReceivedFromBackground() null key");

                        continue; // This does happen.  It shouldn't, but it does.
                    }

                    BackgroundNotificationType type;
                    if (!Enum.TryParse(kv.Key, true, out type))
                        continue;

                    switch (type)
                    {
                        case BackgroundNotificationType.Ping:
                            _notifier.Notify(BackgroundNotificationType.Pong, kv.Value);
                            break;
                        case BackgroundNotificationType.Pong:
                            challenge = kv.Value;
                            break;
                        case BackgroundNotificationType.Start:
                            start = true;
                            break;
                        case BackgroundNotificationType.Fail:
                        case BackgroundNotificationType.Stop:
                            stop = true;
                            break;
                        case BackgroundNotificationType.Id:
                            var backgroundIdValue = kv.Value as Guid?;

                            if (backgroundIdValue.HasValue)
                                backgroundId = backgroundIdValue;

                            break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("MediaPlayerHandle.OnMessageReceivedFromBackground() failed: " + ex.Message);
                }
            }

            var mediaPlayerSession = _mediaPlayerSession;

            if (null != mediaPlayerSession && mediaPlayerSession.IsRunning)
            {
                var handler = MessageReceivedFromBackground;

                if (null != handler)
                    handler(sender, mediaPlayerDataReceivedEventArgs);
            }

            if (!backgroundId.HasValue)
                return;

            if (start)
                await StartBackgroundIdAsync(backgroundId.Value, challenge).ConfigureAwait(false);
            else if (stop)
                await CloseSessionAsync(backgroundId.Value).ConfigureAwait(false);
            else
                UpdateBackgroundId(backgroundId.Value, challenge);
        }

        async Task CloseSessionAsync(Guid backgroundId)
        {
            Debug.WriteLine("MediaPlayerHandle.CloseSession() " + backgroundId);

            var mediaPlayerSession = _mediaPlayerSession;

            if (null == mediaPlayerSession)
                return;

            using (await _asyncLock.LockAsync(CancellationToken.None))
            {
                if (!mediaPlayerSession.BackgroundId.HasValue || backgroundId != mediaPlayerSession.BackgroundId)
                    return;

                if (ReferenceEquals(_mediaPlayerSession, mediaPlayerSession))
                    _mediaPlayerSession = null;

                mediaPlayerSession.Dispose();

                _subscriptionHandle.Unsubscribe();
            }
        }

        async Task StartBackgroundIdAsync(Guid backgroundId, object challenge)
        {
            //Debug.WriteLine("MediaPlayerHandle.StartBackgroundIdAsync() " + backgroundId);

            try
            {
                if (null == _mediaPlayerSession)
                {
                    _subscriptionHandle.Subscribe();

                    await OpenAsync().ConfigureAwait(false);
                }
                else
                    UpdateBackgroundId(backgroundId, challenge);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MediaPlayerHandle.UpdateBackgroundIdAsync() failed: " + ex.ExtendedMessage());
            }
        }

        void UpdateBackgroundId(Guid backgroundId, object challenge)
        {
            //Debug.WriteLine("MediaPlayerHandle.UpdateBackgroundId() " + backgroundId);

            var mediaPlayerSession = _mediaPlayerSession;

            if (null != mediaPlayerSession)
            {
                if (mediaPlayerSession.TrySetBackgroundId(backgroundId, challenge))
                {
                    //Debug.WriteLine("MediaPlayerHandle.UpdateBackgroundId() matched " + backgroundId);
                }
            }
        }

        public void Close()
        {
            Debug.WriteLine("MediaPlayerHandle.Close()");

            try
            {
                var mediaPlayerSession = Interlocked.Exchange(ref _mediaPlayerSession, null);

                if (null == mediaPlayerSession)
                    return;

                mediaPlayerSession.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MediaPlayerHandle.Close() failed: " + ex.ExtendedMessage());
            }
        }

        void OnCurrentStateChanged(MediaPlayer sender, object args)
        {
            var handler = CurrentStateChanged;

            if (null != handler)
                handler(sender, args);
        }

        public void NotifyBackground(BackgroundNotificationType type, object value = null, bool ping = false)
        {
            //Debug.WriteLine("MediaPlayerHandle.NotifyBackground() " + _id + ": " + type);

            if (!_subscriptionHandle.IsSubscribed)
                return;

            var key = type.ToString();

            try
            {
                var message = new ValueSet();

                if (null != key)
                    message.Add(key, value);

                if (ping)
                    message.Add("ping", null);

                _notifier.Notify(message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MediaPlayerHandle.NotifyBackground() failed: " + ex.ExtendedMessage());
                Close();
            }
        }

        public void Suspend()
        {
            Debug.WriteLine("MediaPlayerHandle.Suspend()");

            try
            {
                BackgroundSettings.RemoveForegroundId(_id);

                if (_subscriptionHandle.IsSubscribed)
                    _notifier.Notify(BackgroundNotificationType.Suspend);

                Close();

                _subscriptionHandle.Unsubscribe();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MediaPlayerHandle.Suspend() failed: " + ex.ExtendedMessage());
            }
        }

        public async Task ResumeAsync()
        {
            Debug.WriteLine("MediaPlayerHandle.ResumeAsync()");

            Debug.Assert(_dispatcher.HasThreadAccess, "ResumeAsync requires the dispatcher thread");

            try
            {
                BackgroundSettings.SetForegroundId(_id);

                var backgroundId = BackgroundSettings.BackgroundId;

                if (!backgroundId.HasValue)
                    return;

                await OpenAsync().ConfigureAwait(false);

                if (_subscriptionHandle.IsSubscribed)
                    _notifier.Notify(BackgroundNotificationType.Resume);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MediaPlayerHandle.ResumeAsync() failed: " + ex.ExtendedMessage());
            }
        }

        public void Fail()
        {
            Debug.WriteLine("MediaPlayerHandle.Fail()");

            try
            {
                BackgroundSettings.RemoveForegroundId(_id);

                var backgroundId = BackgroundSettings.BackgroundId;

                if (backgroundId.HasValue)
                    _notifier.Notify(BackgroundNotificationType.Fail);

                Close();

                _subscriptionHandle.Unsubscribe();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MediaPlayerHandle.Fail() failed: " + ex.ExtendedMessage());
            }
        }
    }

    static class MediaPlayerHandleExtensions
    {
        public static void NotifyBackground(this MediaPlayerHandle handle, SystemMediaTransportControlsButton button)
        {
            handle.NotifyBackground(BackgroundNotificationType.Smtc, button.ToString());
        }
    }
}

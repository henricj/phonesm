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
        MediaPlayerSession _mediaPlayerSession;

        public MediaPlayerHandle(CoreDispatcher dispatcher)
        {
            _dispatcher = dispatcher;

            _notifier = new BackgroundNotifier(_id);

            BackgroundMediaPlayer.MessageReceivedFromBackground += OnMessageReceivedFromBackground;
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

                var mediaPlayerSession = _mediaPlayerSession;

                return null == mediaPlayerSession ? null : mediaPlayerSession.MediaPlayer;
            }
        }

        public bool IsRunning
        {
            get
            {
                Debug.Assert(_dispatcher.HasThreadAccess, "IsRunning requires the dispatcher thread");

                var mediaPlayerSession = _mediaPlayerSession;

                return null != mediaPlayerSession && mediaPlayerSession.IsRunning;
            }
        }

        #region IDisposable Members

        public void Dispose()
        {
            BackgroundMediaPlayer.MessageReceivedFromBackground -= OnMessageReceivedFromBackground;

            Close();

            _asyncLock.Dispose();
        }

        #endregion

        public event EventHandler<object> CurrentStateChanged;
        public event EventHandler<MediaPlayerDataReceivedEventArgs> MessageReceivedFromBackground;

        public async Task<MediaPlayerSession> OpenAsync()
        {
            Debug.WriteLine("MediaPlayerHandle.OpenAsync()");

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
                        mediaPlayerSession = new MediaPlayerSession(BackgroundMediaPlayer.Current, _notifier, OnCurrentStateChanged, OnMessageReceivedFromBackground);

                        _mediaPlayerSession = mediaPlayerSession;

                        if (await mediaPlayerSession.OpenAsync(OnCurrentStateChanged).ConfigureAwait(false))
                            return mediaPlayerSession;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("MediaPlayerHandle.OpenAsync() failed: " + ex.ExtendedMessage());
                    }

                    _mediaPlayerSession = null;

                    ResetNotificationSubscription();
                }
            }

            if (null != mediaPlayerSession)
                mediaPlayerSession.Dispose();

            BackgroundMediaPlayer.Shutdown();

            return null;
        }

        async void OnMessageReceivedFromBackground(object sender, MediaPlayerDataReceivedEventArgs mediaPlayerDataReceivedEventArgs)
        {
            //Debug.WriteLine("MediaPlayerHandle.OnMessageReceivedFromBackground()");
            Guid? backgroundId = null;
            object challenge = null;
            var stop = false;

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

                    switch (kv.Key.ToLowerInvariant())
                    {
                        case "ping":
                            _notifier.Notify("pong", kv.Value);
                            break;
                        case "pong":
                            challenge = kv.Value;
                            break;
                        case "start":
                            break;
                        case "fail":
                        case "stop":
                            stop = true;
                            break;
                        case "id":
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

            if (backgroundId.HasValue)
            {
                if (stop)
                    await CloseSessionAsync(backgroundId.Value).ConfigureAwait(false);
                else
                    await UpdateBackgroundIdAsync(backgroundId.Value, challenge).ConfigureAwait(false);
            }

            var handler = MessageReceivedFromBackground;

            if (null != handler)
                handler(sender, mediaPlayerDataReceivedEventArgs);
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

                ResetNotificationSubscription();
            }
        }

        async Task UpdateBackgroundIdAsync(Guid backgroundId, object challenge)
        {
            Debug.WriteLine("MediaPlayerHandle.UpdateBackgroundIdAsync() " + backgroundId);

            var mediaPlayerSession = _mediaPlayerSession;

            try
            {
                if (null == mediaPlayerSession)
                {
                    ResetNotificationSubscription();

                    mediaPlayerSession = await OpenAsync().ConfigureAwait(false);
                }

                if (null != mediaPlayerSession)
                {
                    if (mediaPlayerSession.TrySetBackgroundId(backgroundId, challenge))
                        Debug.WriteLine("MediaPlayerHandle.UpdateBackgroundIdAsync() matched " + backgroundId);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MediaPlayerHandle.UpdateBackgroundIdAsync() failed: " + ex.ExtendedMessage());
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

        public void NotifyBackground(string key = null, object value = null, bool ping = false)
        {
            //Debug.WriteLine("MediaPlayerHandle.NotifyBackground() " + _id + ": " + key);

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
            try
            {
                _notifier.Notify("suspend");

                Close();

                BackgroundMediaPlayer.MessageReceivedFromBackground -= OnMessageReceivedFromBackground;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MediaPlayerHandle.Suspend() failed: " + ex.ExtendedMessage());
            }
        }

        public void Resume()
        {
            var awaiter = _dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                async () =>
                {
                    try
                    {
                        BackgroundMediaPlayer.MessageReceivedFromBackground += OnMessageReceivedFromBackground;

                        await OpenAsync();

                        _notifier.Notify("resume");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("MediaPlayerHandle.Suspend() failed: " + ex.ExtendedMessage());
                    }
                });
        }

        public void ResetNotificationSubscription()
        {
            Debug.WriteLine("MediaPlayerHandle.ResetNotificationSubscription()");

            BackgroundMediaPlayer.MessageReceivedFromBackground -= OnMessageReceivedFromBackground;
            BackgroundMediaPlayer.MessageReceivedFromBackground += OnMessageReceivedFromBackground;
        }
    }

    static class MediaPlayerHandleExtensions
    {
        public static void NotifyBackground(this MediaPlayerHandle handle, SystemMediaTransportControlsButton button)
        {
            handle.NotifyBackground("smtc", button.ToString());
        }
    }
}

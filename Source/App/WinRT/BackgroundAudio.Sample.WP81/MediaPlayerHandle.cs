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
using System.Threading.Tasks;
using Windows.Foundation;
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
        readonly CoreDispatcher _dispatcher;
        readonly Guid _id = Guid.NewGuid();
        readonly IBackgroundMediaNotifier _notifier;
        MediaPlayerSession _mediaPlayerSession;

        public MediaPlayerHandle(CoreDispatcher dispatcher)
        {
            _dispatcher = dispatcher;

            _notifier = new BackgroundNotifier(_id);
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

                return null == _mediaPlayerSession ? null : _mediaPlayerSession.MediaPlayer;
            }
        }

        public bool IsRunning
        {
            get
            {
                Debug.Assert(_dispatcher.HasThreadAccess, "IsRunning requires the dispatcher thread");

                return null != _mediaPlayerSession && _mediaPlayerSession.IsRunning;
            }
        }

        #region IDisposable Members

        public void Dispose()
        {
            Close();
        }

        #endregion

        public event TypedEventHandler<MediaPlayer, object> CurrentStateChanged;
        public event EventHandler<MediaPlayerDataReceivedEventArgs> MessageReceivedFromBackground;

        public async Task OpenAsync()
        {
            Debug.WriteLine("MediaPlayerHandle.OpenAsync()");

            Debug.Assert(_dispatcher.HasThreadAccess, "MediaPlayer requires the dispatcher thread");

            MediaPlayerSession mediaPlayerSession = null;

            try
            {
                mediaPlayerSession = new MediaPlayerSession(BackgroundMediaPlayer.Current, _notifier, OnCurrentStateChanged, OnMessageReceivedFromBackground);

                _mediaPlayerSession = mediaPlayerSession;

                await mediaPlayerSession.OpenAsync(OnCurrentStateChanged).ConfigureAwait(false);

                return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MediaPlayerHandle.OpenAsync() failed: " + ex.ExtendedMessage());
            }

            _mediaPlayerSession = null;

            if (null != mediaPlayerSession)
                mediaPlayerSession.Dispose();

            BackgroundMediaPlayer.Shutdown();
        }

        void OnMessageReceivedFromBackground(object sender, MediaPlayerDataReceivedEventArgs mediaPlayerDataReceivedEventArgs)
        {
            //Debug.WriteLine("MediaPlayerHandle.OnMessageReceivedFromBackground()");
            Guid? backgroundId = null;

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
                        case "start":
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
                var awaiter = _dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    async () =>
                    {
                        var mediaPlayerSession = _mediaPlayerSession;

                        try
                        {
                            if (null != mediaPlayerSession)
                            {
                                if (mediaPlayerSession.TrySetRemoteId(backgroundId.Value))
                                    return;

                                _mediaPlayerSession = null;

                                mediaPlayerSession.Dispose();
                            }

                            await OpenAsync().ConfigureAwait(false);

                            mediaPlayerSession = _mediaPlayerSession;

                            if (null != mediaPlayerSession && mediaPlayerSession.TrySetRemoteId(backgroundId.Value))
                                return;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("MediaPlayerHandle.OnMessageReceived() failed: " + ex.ExtendedMessage());
                        }

                        // We can get here if there is no response to the ping.

                        try
                        {
                            mediaPlayerSession = _mediaPlayerSession;

                            if (null != mediaPlayerSession)
                            {
                                _mediaPlayerSession = null;
                                mediaPlayerSession.Dispose();
                            }

                            BackgroundMediaPlayer.Shutdown();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("MediaPlayerHandle.OnMessageReceived() cleanup failed: " + ex.ExtendedMessage());
                        }
                    });
            }

            var handler = MessageReceivedFromBackground;

            if (null != handler)
                handler(sender, mediaPlayerDataReceivedEventArgs);
        }

        public void Close()
        {
            Debug.WriteLine("MediaPlayerHandle.Close()");

            try
            {
                Debug.Assert(_dispatcher.HasThreadAccess, "MediaPlayer requires the dispatcher thread");

                var mediaPlayerSession = _mediaPlayerSession;

                if (null == mediaPlayerSession)
                    return;

                _mediaPlayerSession = null;

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
                        await OpenAsync();

                        _notifier.Notify("resume");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("MediaPlayerHandle.Suspend() failed: " + ex.ExtendedMessage());
                    }
                });
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

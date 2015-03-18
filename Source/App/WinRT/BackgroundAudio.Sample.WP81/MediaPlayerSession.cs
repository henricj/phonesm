// -----------------------------------------------------------------------
//  <copyright file="MediaPlayerSession.cs" company="Henric Jungheim">
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
using Windows.Media.Playback;
using SM.Media.BackgroundAudio;
using SM.Media.Utility;

namespace BackgroundAudio.Sample
{
    sealed class MediaPlayerSession : IDisposable
    {
        static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(Debugger.IsAttached ? 30 : 3);
        public readonly MediaPlayer MediaPlayer;
        readonly TaskCompletionSource<Guid> _backgroundRunningCompletionSource = new TaskCompletionSource<Guid>();
        readonly IBackgroundMediaNotifier _notifier;
        readonly Action<MediaPlayer, object> _onCurrentStateChanged;
        readonly Action<object, MediaPlayerDataReceivedEventArgs> _onMessage;
        int _disposed;

        public MediaPlayerSession(MediaPlayer mediaPlayer, IBackgroundMediaNotifier notifier,
            Action<MediaPlayer, object> onCurrentStateChanged,
            Action<object, MediaPlayerDataReceivedEventArgs> onMessage)
        {
            if (null == mediaPlayer)
                throw new ArgumentNullException("mediaPlayer");
            if (null == notifier)
                throw new ArgumentNullException("notifier");
            if (null == onCurrentStateChanged)
                throw new ArgumentNullException("onCurrentStateChanged");
            if (null == onMessage)
                throw new ArgumentNullException("onMessage");

            MediaPlayer = mediaPlayer;
            _notifier = notifier;
            _onCurrentStateChanged = onCurrentStateChanged;
            _onMessage = onMessage;

            SubscribeMediaPlayer();
        }

        public bool IsRunning
        {
            get
            {
                return TaskStatus.RanToCompletion == _backgroundRunningCompletionSource.Task.Status
                       && MediaPlayerState.Closed != MediaPlayer.CurrentState;
            }
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (0 != Interlocked.Exchange(ref _disposed, 1))
                return;

            _backgroundRunningCompletionSource.TrySetCanceled();

            UnsubscribeMediaPlayer();
        }

        #endregion

        void OnCurrentStateChanged(MediaPlayer sender, object args)
        {
            Debug.Assert(ReferenceEquals(sender, MediaPlayer), "MediaPlayer mismatch");

            _onCurrentStateChanged(sender, args);
        }

        void OnMessageReceivedFromBackground(object sender, MediaPlayerDataReceivedEventArgs e)
        {
            _onMessage(sender, e);
        }

        void SubscribeMediaPlayer()
        {
            Debug.WriteLine("MediaPlayerSession.SubscribeMediaPlayer()");

            try
            {
                BackgroundMediaPlayer.MessageReceivedFromBackground += OnMessageReceivedFromBackground;
                MediaPlayer.CurrentStateChanged += OnCurrentStateChanged;
            }
            catch (Exception ex)
            {
                // The COM object is probably dead...
                Debug.WriteLine("MediaPlayerSession.SubscribeMediaPlayer() unable to register event: " + ex.ExtendedMessage());
            }
        }

        void UnsubscribeMediaPlayer()
        {
            Debug.WriteLine("MediaPlayerSession.UnsubscribeMediaPlayer()");

            try
            {
                BackgroundMediaPlayer.MessageReceivedFromBackground -= OnMessageReceivedFromBackground;
                MediaPlayer.CurrentStateChanged -= OnCurrentStateChanged;
            }
            catch (Exception ex)
            {
                // The COM object is probably dead...
                Debug.WriteLine("MediaPlayerSession.UnsubscribeMediaPlayer() unable to deregister event: " + ex.ExtendedMessage());
            }
        }

        public bool TrySetRemoteId(Guid guid)
        {
            //Debug.WriteLine("MediaPlayerSession.TrySetRemoteId() " + guid);

            if (TaskStatus.RanToCompletion == _backgroundRunningCompletionSource.Task.Status)
                return guid == _backgroundRunningCompletionSource.Task.Result;

            return _backgroundRunningCompletionSource.TrySetResult(guid);
        }

        public async Task OpenAsync(Action<MediaPlayer, object> onCurrentStateChanged)
        {
            if (null == onCurrentStateChanged)
                throw new ArgumentNullException("onCurrentStateChanged");

            _notifier.Notify("ping");

            var cts = new CancellationTokenSource(StartTimeout);

            using (cts.Token.Register(obj => ((TaskCompletionSource<Guid>)obj).TrySetCanceled(), _backgroundRunningCompletionSource))
            {
                await _backgroundRunningCompletionSource.Task.ConfigureAwait(false);
            }
        }
    }
}

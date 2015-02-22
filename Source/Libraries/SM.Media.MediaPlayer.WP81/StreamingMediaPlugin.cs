// -----------------------------------------------------------------------
//  <copyright file="StreamingMediaPlugin.cs" company="Henric Jungheim">
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
using Windows.UI.Xaml;
using Microsoft.PlayerFramework;
using SM.Media.Content;
using SM.Media.Utility;

namespace SM.Media.MediaPlayer
{
    public class StreamingMediaPlugin : IPlugin
    {
        readonly AsyncLock _asyncLock = new AsyncLock();
        PlayState _currentPlayState;
        CancellationTokenSource _mediaLoadingCancellationTokenSource = new CancellationTokenSource();
        IMediaStreamFacade _mediaStreamFacade;
        PlayState _playState;
        Microsoft.PlayerFramework.MediaPlayer _player;
        CancellationTokenSource _unloadCancellationTokenSource = new CancellationTokenSource();

        #region IPlugin Members

        public virtual void Load()
        {
            Debug.WriteLine("StreamingMediaPlugin.Load()");

            if (_unloadCancellationTokenSource.IsCancellationRequested)
            {
                _unloadCancellationTokenSource.Dispose();
                _unloadCancellationTokenSource = new CancellationTokenSource();
            }
        }

        public virtual void Update(IMediaSource mediaSource)
        {
            Debug.WriteLine("StreamingMediaPlugin.Update()");
        }

        public virtual void Unload()
        {
            Debug.WriteLine("StreamingMediaPlugin.Unload()");

            _unloadCancellationTokenSource.Cancel();

            CleanupAsync().Wait();
        }

        public Microsoft.PlayerFramework.MediaPlayer MediaPlayer
        {
            get { return _player; }
            set
            {
                if (null != _player)
                {
                    _player.MediaLoading -= PlayerOnMediaLoading;
                    _player.MediaOpened -= PlayerOnMediaOpened;
                    _player.MediaEnding -= PlayerOnMediaEnding;
                    _player.MediaFailed -= PlayerOnMediaFailed;
                    _player.MediaEnded -= PlayerOnMediaEnded;
                    _player.MediaClosed -= PlayerOnMediaClosed;
                }

                _player = value;

                if (null != _player)
                {
                    _player.MediaLoading += PlayerOnMediaLoading;
                    _player.MediaOpened += PlayerOnMediaOpened;
                    _player.MediaEnding += PlayerOnMediaEnding;
                    _player.MediaFailed += PlayerOnMediaFailed;
                    _player.MediaEnded += PlayerOnMediaEnded;
                    _player.MediaClosed += PlayerOnMediaClosed;
                }
            }
        }

        #endregion

        void PlayerOnMediaOpened(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("StreamingMediaPlugin MediaOpened " + _playState);

            _currentPlayState = _playState;
        }

        void PlayerOnMediaClosed(object sender, RoutedEventArgs routedEventArgs)
        {
            Debug.WriteLine("StreamingMediaPlugin MediaClosed " + _currentPlayState);

            var playState = _currentPlayState;

            _currentPlayState = null;

            if (null == playState)
                return;

            _playState.OnMediaClosed();
        }

        void PlayerOnMediaEnding(object sender, MediaPlayerDeferrableEventArgs mediaPlayerDeferrableEventArgs)
        {
            Debug.WriteLine("StreamingMediaPlugin MediaEnding " + _currentPlayState);
        }

        void PlayerOnMediaEnded(object sender, MediaPlayerActionEventArgs mediaPlayerActionEventArgs)
        {
            Debug.WriteLine("StreamingMediaPlugin MediaEnded " + _currentPlayState);
        }

        void PlayerOnMediaFailed(object sender, ExceptionRoutedEventArgs exceptionRoutedEventArgs)
        {
            Debug.WriteLine("StreamingMediaPlugin MediaFailed " + _currentPlayState);

            var task = PlayerOnMediaFailedAsync();

            TaskCollector.Default.Add(task, "StreamingMediaPlugin.PlayerOnMediaFailed() PlayerOnMediaFailedAsync");
        }

        async Task PlayerOnMediaFailedAsync()
        {
            Debug.WriteLine("StreamingMediaPlugin.PlayerOnMediaFailedAsync() " + _currentPlayState);

            var playState = _currentPlayState;

            _currentPlayState = null;

            if (null != playState)
                await playState.OnMediaFailedAsync().ConfigureAwait(false);
        }

        async void PlayerOnMediaLoading(object sender, MediaPlayerDeferrableEventArgs mediaPlayerDeferrableEventArgs)
        {
            Debug.WriteLine("StreamingMediaPlugin MediaLoading");

            // ReSharper disable once PossiblyMistakenUseOfParamsMethod
            var mediaLoadingCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_unloadCancellationTokenSource.Token);

            var oldTokenSource = Interlocked.Exchange(ref _mediaLoadingCancellationTokenSource, mediaLoadingCancellationTokenSource);

            if (null != oldTokenSource)
                oldTokenSource.CancelDisposeSafe();

            var mediaLoadingEventArgs = (MediaLoadingEventArgs)mediaPlayerDeferrableEventArgs;

            var source = mediaLoadingEventArgs.Source;

            if (null == source)
                return;

            MediaPlayerDeferral deferral = null;

            try
            {
                deferral = mediaPlayerDeferrableEventArgs.DeferrableOperation.GetDeferral();

                Debug.Assert(!deferral.CancellationToken.IsCancellationRequested, "MediaPlayer cancellation token is already canceled");

                using (var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(deferral.CancellationToken, mediaLoadingCancellationTokenSource.Token))
                {
                    var cancellationToken = linkedTokenSource.Token;

                    var playState = _playState;

                    if (null != playState)
                    {
                        await playState.StopAsync(cancellationToken);

                        if (null != _playState)
                        {
                            Debug.WriteLine("StreamingMediaPlugin MediaLoading non-null _playState");

                            return;
                        }
                    }

                    if (string.Equals(source.Scheme, "stop", StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.WriteLine("StreamingMediaPlugin MediaLoading stop");
                        return;
                    }

                    var passThrough = StreamingMediaSettings.Parameters.IsPassThrough;

                    if (null != passThrough)
                    {
                        if (passThrough(source))
                        {
                            Debug.WriteLine("StreamingMediaPlugin.PlayerOnMediaLoading() passing through " + source);

                            deferral.Complete();
                            deferral = null;
                            
                            return;
                        }
                    }

                    playState = new PlayState();

                    using (await _asyncLock.LockAsync(cancellationToken).ConfigureAwait(false))
                    {
                        if (Interlocked.CompareExchange(ref _playState, playState, null) != null)
                        {
                            Debug.WriteLine("StreamingMediaPlugin MediaLoading _playState is not null");

                            playState.Dispose();

                            return;
                        }

                        var playTask = playState.PlayAsync(InitializeMediaStream(), source, cancellationToken)
                            .ContinueWith(t =>
                            {
                                var ex = t.Exception;

                                if (null != ex)
                                    Debug.WriteLine("StreamingMediaPlugin MediaLoading player failed: " + ex.Message);

                                if (Interlocked.CompareExchange(ref _playState, null, playState) != playState)
                                    Debug.WriteLine("StreamingMediaPlugin MediaLoading playState has changed");

                                playState.Dispose();
                            }, TaskContinuationOptions.ExecuteSynchronously);

                        TaskCollector.Default.Add(playTask, "StreamingMediaPlugin MediaLoading playTask");
                    }

                    mediaLoadingEventArgs.MediaStreamSource = await playState.GetMediaSourceAsync(cancellationToken).ConfigureAwait(false);
                    mediaLoadingEventArgs.Source = null;

                    //Debug.WriteLine("StreamingMediaPlugin MediaLoading deferral.Complete()");
                    deferral.Complete();
                    deferral = null;
                }
            }
            catch (OperationCanceledException)
            { }
            catch (Exception ex)
            {
                Debug.WriteLine("StreamingMediaPlugin.PlayerOnMediaLoading() failed: " + ex.Message);
            }
            finally
            {
                if (null != deferral)
                {
                    Debug.WriteLine("StreamingMediaPlugin MediaLoading deferral.Cancel()");
                    deferral.Cancel();
                }
            }
        }

        IMediaStreamFacade InitializeMediaStream()
        {
            if (null != _mediaStreamFacade && _mediaStreamFacade.IsDisposed)
                _mediaStreamFacade = null;

            if (null == _mediaStreamFacade)
                _mediaStreamFacade = CreateMediaStreamFacade();

            return _mediaStreamFacade;
        }

        protected virtual IMediaStreamFacade CreateMediaStreamFacade()
        {
            return MediaStreamFacadeSettings.Parameters.Create();
        }

        async Task CleanupAsync()
        {
            Debug.WriteLine("StreamingMediaPlugin.CleanupAsync()");

            try
            {
                var playState = _playState;

                if (null != playState)
                    await playState.CloseAsync().ConfigureAwait(false);

                using (await _asyncLock.LockAsync(CancellationToken.None).ConfigureAwait(false))
                {
                    if (_mediaStreamFacade.IsDisposed)
                        _mediaStreamFacade = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("StreamingMediaPlugin.CleanupAsync() failed: " + ex.Message);
            }
        }

        #region Nested type: PlayState

        sealed class PlayState : IDisposable
        {
            static int _idCount;
            readonly int _id = Interlocked.Increment(ref _idCount);
            readonly TaskCompletionSource<Windows.Media.Core.IMediaSource> _mediaSourceTaskCompletionSource = new TaskCompletionSource<Windows.Media.Core.IMediaSource>();
            readonly CancellationTokenSource _playingCancellationTokenSource = new CancellationTokenSource();
            readonly TaskCompletionSource<object> _playingTaskCompletionSource = new TaskCompletionSource<object>();
            IMediaStreamFacade _mediaStreamFacade;
            Task _playingTask = TplTaskExtensions.CompletedTask;
            public ContentType ContentType { get; set; }

            #region IDisposable Members

            public void Dispose()
            {
                _mediaSourceTaskCompletionSource.TrySetCanceled();

                _playingCancellationTokenSource.CancelDisposeSafe();
            }

            #endregion

            public Task PlayAsync(IMediaStreamFacade mediaStreamFacade, Uri source, CancellationToken cancellationToken)
            {
                //Debug.WriteLine("PlayState.PlayAsync() " + _id);

                _mediaStreamFacade = mediaStreamFacade;

                _playingTask = Task.Run(() => PlayerAsync(source, cancellationToken), cancellationToken);

                return _playingTask;
            }

            async Task PlayerAsync(Uri source, CancellationToken cancellationToken)
            {
                //Debug.WriteLine("PlayState.PlayerAsync() " + _id);

                try
                {
                    using (var createMediaCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_playingCancellationTokenSource.Token, cancellationToken))
                    {
                        _mediaStreamFacade.ContentType = ContentType;

                        var mss = await _mediaStreamFacade.CreateMediaStreamSourceAsync(source, createMediaCancellationTokenSource.Token).ConfigureAwait(false);

                        if (!_mediaSourceTaskCompletionSource.TrySetResult(mss))
                            _playingTaskCompletionSource.TrySetResult(null);
                    }

                    using (_playingCancellationTokenSource.Token.Register(() => _playingTaskCompletionSource.TrySetCanceled()))
                    {
                        Debug.WriteLine("PlayState.PlayerAsync() waiting for playing to complete");
                        await _playingTaskCompletionSource.Task.ConfigureAwait(false);
                    }

                    await _mediaStreamFacade.StopAsync(_playingCancellationTokenSource.Token);

                    return;
                }
                catch (OperationCanceledException)
                { }
                catch (Exception ex)
                {
                    Debug.WriteLine("PlayState.PlayerAsync() failed: " + ex.ExtendedMessage());
                }

                try
                {
                    _mediaSourceTaskCompletionSource.TrySetCanceled();

                    await _mediaStreamFacade.CloseAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("PlayState.PlayerAsync() cleanup failed: " + ex.ExtendedMessage());
                }

                //Debug.WriteLine("PlayState.PlayerAsync() completed " + _id);
            }

            public async Task<Windows.Media.Core.IMediaSource> GetMediaSourceAsync(CancellationToken cancellationToken)
            {
                //Debug.WriteLine("PlayState.GetMediaSourceAsync() " + _id);

                using (cancellationToken.Register(() => _playingCancellationTokenSource.Cancel()))
                {
                    return await _mediaSourceTaskCompletionSource.Task.ConfigureAwait(false);
                }
            }

            public async Task StopAsync(CancellationToken cancellationToken)
            {
                //Debug.WriteLine("PlayState.StopAsync() " + _id);

                await _mediaStreamFacade.StopAsync(cancellationToken).ConfigureAwait(false);

                await _playingTask.ConfigureAwait(false);
            }

            public async Task CloseAsync()
            {
                //Debug.WriteLine("PlayState.CloseAsync() " + _id);

                if (!_playingCancellationTokenSource.IsCancellationRequested)
                    _playingCancellationTokenSource.Cancel();

                await _playingTask.ConfigureAwait(false);
            }

            public void OnMediaClosed()
            {
                //Debug.WriteLine("PlayState.OnMediaClosed() " + _id);

                _playingTaskCompletionSource.TrySetResult(null);
            }

            public async Task OnMediaFailedAsync()
            {
                Debug.WriteLine("PlayState.OnMediaFailedAsync() " + _id);

                try
                {
                    await CloseAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("PlayState.OnMediaFailedAsync() CloseAsync() failed: " + ex.Message);
                }

                _playingTaskCompletionSource.TrySetResult(null);
            }

            public override string ToString()
            {
                return string.Format("ID {0} IsCompleted {1}", _id, _playingTask.IsCompleted);
            }
        }

        #endregion
    }
}

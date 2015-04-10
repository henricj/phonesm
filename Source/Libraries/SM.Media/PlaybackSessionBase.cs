// -----------------------------------------------------------------------
//  <copyright file="PlaybackSessionBase.cs" company="Henric Jungheim">
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
using SM.Media.Content;
using SM.Media.Utility;

namespace SM.Media
{
    public abstract class PlaybackSessionBase<TMediaSource> : IDisposable
        where TMediaSource : class
    {
#if DEBUG
        // ReSharper disable once StaticFieldInGenericType
        static int _idCount;
        readonly int _id = Interlocked.Increment(ref _idCount);
#endif
        readonly TaskCompletionSource<TMediaSource> _mediaSourceTaskCompletionSource = new TaskCompletionSource<TMediaSource>();
        readonly IMediaStreamFacadeBase<TMediaSource> _mediaStreamFacade;
        readonly CancellationTokenSource _playingCancellationTokenSource = new CancellationTokenSource();
        int _isDisposed;

        protected PlaybackSessionBase(IMediaStreamFacadeBase<TMediaSource> mediaStreamFacade)
        {
            if (null == mediaStreamFacade)
                throw new ArgumentNullException("mediaStreamFacade");

            _mediaStreamFacade = mediaStreamFacade;
        }

        public ContentType ContentType { get; set; }

        protected IMediaStreamFacadeBase<TMediaSource> MediaStreamFacade
        {
            get { return _mediaStreamFacade; }
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (0 != Interlocked.Exchange(ref _isDisposed, 1))
                return;

            Dispose(true);

            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            _mediaSourceTaskCompletionSource.TrySetCanceled();

            _playingCancellationTokenSource.CancelDisposeSafe();
        }

        #endregion

        public virtual Task PlayAsync(Uri source, CancellationToken cancellationToken)
        {
            Debug.WriteLine("PlaybackSessionBase.PlayAsync() " + this);

            var playingTask = PlayerAsync(source, cancellationToken);

            TaskCollector.Default.Add(playingTask, "StreamingMediaPlugin PlayerAsync");

            return MediaStreamFacade.PlayingTask;
        }

        async Task PlayerAsync(Uri source, CancellationToken cancellationToken)
        {
            //Debug.WriteLine("PlaybackSessionBase.PlayerAsync() " + this);

            try
            {
                using (var createMediaCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_playingCancellationTokenSource.Token, cancellationToken))
                {
                    MediaStreamFacade.ContentType = ContentType;

                    var mss = await MediaStreamFacade.CreateMediaStreamSourceAsync(source, createMediaCancellationTokenSource.Token).ConfigureAwait(false);

                    if (!_mediaSourceTaskCompletionSource.TrySetResult(mss))
                        throw new OperationCanceledException();
                }

                return;
            }
            catch (OperationCanceledException)
            { }
            catch (Exception ex)
            {
                Debug.WriteLine("PlaybackSessionBase.PlayerAsync() failed: " + ex.ExtendedMessage());
            }

            try
            {
                _mediaSourceTaskCompletionSource.TrySetCanceled();

                await MediaStreamFacade.CloseAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("PlaybackSessionBase.PlayerAsync() cleanup failed: " + ex.ExtendedMessage());
            }

            //Debug.WriteLine("PlaybackSessionBase.PlayerAsync() completed " + this);
        }

        public virtual async Task<TMediaSource> GetMediaSourceAsync(CancellationToken cancellationToken)
        {
            //Debug.WriteLine("PlaybackSessionBase.GetMediaSourceAsync() " + this);

            return await _mediaSourceTaskCompletionSource.Task.WithCancellation(cancellationToken).ConfigureAwait(false);
        }

        public virtual async Task StopAsync(CancellationToken cancellationToken)
        {
            //Debug.WriteLine("PlaybackSessionBase.StopAsync() " + this);

            await MediaStreamFacade.StopAsync(cancellationToken).ConfigureAwait(false);

            await MediaStreamFacade.PlayingTask.ConfigureAwait(false);
        }

        public virtual async Task CloseAsync()
        {
            //Debug.WriteLine("PlaybackSessionBase.CloseAsync() " + this);

            if (!_playingCancellationTokenSource.IsCancellationRequested)
                _playingCancellationTokenSource.Cancel();

            await MediaStreamFacade.PlayingTask.ConfigureAwait(false);
        }

        public virtual async Task OnMediaFailedAsync()
        {
            Debug.WriteLine("PlaybackSessionBase.OnMediaFailedAsync() " + this);

            try
            {
                await CloseAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("PlaybackSessionBase.OnMediaFailedAsync() CloseAsync() failed: " + ex.Message);
            }
        }

        public override string ToString()
        {
#if DEBUG
            return String.Format("Playback ID {0} IsCompleted {1}", _id, MediaStreamFacade.PlayingTask.IsCompleted);
#else
            return String.Format("Playback IsCompleted " + _mediaStreamFacade.PlayingTask.IsCompleted);
#endif
        }
    }
}

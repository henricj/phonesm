// -----------------------------------------------------------------------
//  <copyright file="MediaStreamFascadeBase.cs" company="Henric Jungheim">
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
using SM.Media.Buffering;
using SM.Media.Builder;
using SM.Media.Content;
using SM.Media.MediaParser;
using SM.Media.Playlists;
using SM.Media.Segments;
using SM.Media.Utility;

namespace SM.Media
{
    public interface IMediaStreamFascade : IDisposable
    {
        /// <summary>
        ///     Force the <see cref="Source" /> to be considered <see cref="SM.Media.Content.ContentType" />.
        ///     The type will be detected if null. Set this value before setting <see cref="Source" />.
        /// </summary>
        /// <seealso cref="SM.Media.Content.ContentTypes" />
        ContentType ContentType { get; set; }

        Uri Source { get; set; }
        TimeSpan? SeekTarget { get; set; }
        TsMediaManager.MediaState State { get; }
        IBuilder<IMediaManager> Builder { get; }

        event EventHandler<TsMediaManagerStateEventArgs> StateChange;
        void Play();
        void RequestStop();
        Task CloseAsync();
    }

    public class MediaStreamFascadeBase : IMediaStreamFascade
    {
        readonly AsyncFifoWorker _asyncFifoWorker = new AsyncFifoWorker();
        readonly CancellationTokenSource _disposeCancellationTokenSource = new CancellationTokenSource();
        readonly IBuilder<IMediaManager> _mediaManagerBuilder;
        readonly Func<IMediaStreamSource, Task> _setSourceAsync;
        int _isDisposed;
        IMediaManager _mediaManager;
        ISegmentManager _playlist;
        Uri _source;

        protected MediaStreamFascadeBase(IBuilder<IMediaManager> mediaManagerBuilder, Func<IMediaStreamSource, Task> setSourceAsync)
        {
            if (mediaManagerBuilder == null)
                throw new ArgumentNullException("mediaManagerBuilder");
            if (null == setSourceAsync)
                throw new ArgumentNullException("setSourceAsync");

            _setSourceAsync = setSourceAsync;
            _mediaManagerBuilder = mediaManagerBuilder;
        }

        #region IMediaStreamFascade Members

        public void Dispose()
        {
            if (0 != Interlocked.Exchange(ref _isDisposed, 1))
                return;

            Dispose(true);

            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        public ContentType ContentType { get; set; }

        public virtual Uri Source
        {
            get { return _source; }
            set
            {
                ThrowIfDisposed();

                if (value == null)
                    Post(CloseMediaAsync);
                else if (value.IsAbsoluteUri)
                    Post(() => SetMediaSourceAsync(value));
                else
                {
                    Debug.WriteLine("MediaStreamFascade Source setter: invalid URL: " + value);
                    Post(CloseMediaAsync);
                }
            }
        }

        public TimeSpan? SeekTarget
        {
            get { return null == _mediaManager ? null : _mediaManager.SeekTarget; }
            set
            {
                ThrowIfDisposed();

                if (null == _mediaManager)
                    return;

                _mediaManager.SeekTarget = value;
            }
        }

        public TsMediaManager.MediaState State
        {
            get
            {
                if (null == _mediaManager)
                    return TsMediaManager.MediaState.Closed;

                return _mediaManager.State;
            }
        }

        public IBuilder<IMediaManager> Builder
        {
            get { return _mediaManagerBuilder; }
        }

        public event EventHandler<TsMediaManagerStateEventArgs> StateChange;

        public void Play()
        {
            Debug.WriteLine("MediaStreamFascade.Play()");

            ThrowIfDisposed();

            Post(StartPlaybackAsync);
        }

        public void RequestStop()
        {
            Debug.WriteLine("MediaStreamFascade.Stop()");

            ThrowIfDisposed();

            Post(CloseMediaAsync);
        }

        public Task CloseAsync()
        {
            Debug.WriteLine("MediaStreamFascade.CloseAsync()");

            ThrowIfDisposed();

            _disposeCancellationTokenSource.Cancel();

            return _asyncFifoWorker.PostAsync(CloseMediaAsync, CancellationToken.None);
        }

        #endregion

        void ThrowIfDisposed()
        {
            if (0 != _isDisposed)
                throw new ObjectDisposedException(GetType().Name);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            if (!_disposeCancellationTokenSource.IsCancellationRequested)
                _disposeCancellationTokenSource.Cancel();

            StateChange = null;

            CleanupMediaManager();

            _mediaManagerBuilder.DisposeSafe();

            _asyncFifoWorker.Dispose();

            _disposeCancellationTokenSource.Dispose();
        }

        void CleanupMediaManager()
        {
            var mediaManager = _mediaManager;

            if (null != mediaManager)
            {
                mediaManager.OnStateChange -= MediaManagerOnStateChange;

                _mediaManager = null;

                _mediaManagerBuilder.Destroy(mediaManager);
            }
        }

        void Post(Func<Task> work)
        {
            _asyncFifoWorker.Post(work, _disposeCancellationTokenSource.Token);
        }

        async Task SetMediaSourceAsync(Uri source)
        {
            Debug.WriteLine("MediaStreamFascade.SetMediaSourceAsync({0})", source);

            try
            {
                await OpenMediaAsync(source).ConfigureAwait(true);

                await _setSourceAsync(_mediaManager.MediaStreamSource).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MediaStreamFascade.SetMediaSourceAsync({0}) failed: {1}", source, ex.Message);
            }
        }

        async Task StartPlaybackAsync()
        {
            Debug.WriteLine("MediaStreamFascade.StartPlaybackAsync()");

            try
            {
                if (null == _source)
                    return;

                if (null == _mediaManager || TsMediaManager.MediaState.Error == _mediaManager.State || TsMediaManager.MediaState.Closed == _mediaManager.State)
                {
                    await OpenMediaAsync(_source).ConfigureAwait(false);

                    Debug.Assert(null != _mediaManager);
                }
            }
            catch (Exception ex)
            {
                // Send a "Failed" message here?
                Debug.WriteLine("MediaStreamFascade.StartPlaybackAsync() failed: " + ex.Message);
            }
        }

        async Task OpenMediaAsync(Uri source)
        {
            Debug.WriteLine("MediaStreamFascade.OpenMediaAsync()");

            if (null != _mediaManager)
                await CloseMediaAsync().ConfigureAwait(false);

            Debug.Assert(null == _mediaManager);
            Debug.Assert(null == _playlist);

            if (null == source)
                return;

            if (!source.IsAbsoluteUri)
            {
                Debug.WriteLine("MediaStreamFascade.OpenMediaAsync() source is not absolute: " + source);
                return;
            }

            _source = source;

            _mediaManager = _mediaManagerBuilder.Create();

            _mediaManager.Source = new[] { source };
            _mediaManager.ContentType = ContentType;

            _mediaManager.OnStateChange += MediaManagerOnStateChange;
        }

        async Task CloseMediaAsync()
        {
            Debug.WriteLine("MediaStreamFascade.CloseMediaAsync()");

            if (null != _mediaManager)
            {
                try
                {
                    //Debug.WriteLine("MediaPlayerSource.CloseMediaAsync() calling mediaManager.CloseAsync()");

                    await _mediaManager.CloseAsync().ConfigureAwait(false);

                    //Debug.WriteLine("MediaPlayerSource.CloseMediaAsync() returned from mediaManager.CloseAsync()");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("MediaStreamFascade.CloseMediaAsync() Media manager close failed: " + ex.Message);
                }

                CleanupMediaManager();
            }

            var playlist = _playlist;

            if (null != playlist)
            {
                _playlist = null;

                playlist.CleanupBackground("MediaStreamFascade.CloseMediaAsync playlist");
            }

            _source = null;

            Debug.WriteLine("MediaStreamFascade.CloseMediaAsync() completed");
        }

        void MediaManagerOnStateChange(object sender, TsMediaManagerStateEventArgs e)
        {
            Debug.WriteLine("MediaStreamFascade.MediaManagerOnStateChange() to {0}: {1}", e.State, e.Message);

            var stateChange = StateChange;

            if (null == stateChange)
                return;

            if (e.State == TsMediaManager.MediaState.Closed)
                RequestStop();

            try
            {
                stateChange(this, e);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MediaStreamFascade.MediaManagerOnStateChange() Exception in StateChange event handler: " + ex.Message);
            }
        }
    }

    public static class MediaStreamFascadeExtensions
    {
        public static void SetParameter(this IMediaStreamFascade mediaStreamFascade, IPlaylistSegmentManagerParameters parameters)
        {
            mediaStreamFascade.Builder.RegisterSingleton(parameters);
        }

        public static void SetParameter(this IMediaStreamFascade mediaStreamFascade, IMediaManagerParameters parameters)
        {
            mediaStreamFascade.Builder.RegisterSingleton(parameters);
        }

        public static void SetParameter(this IMediaStreamFascade mediaStreamFascade, IBufferingPolicy policy)
        {
            mediaStreamFascade.Builder.RegisterSingleton(policy);
        }

        public static void SetParameter(this IMediaStreamFascade mediaStreamFascade, IMediaStreamSource mediaStreamSource)
        {
            mediaStreamFascade.Builder.RegisterSingleton(mediaStreamSource);
        }

        public static void SetParameter(this IMediaStreamFascade mediaStreamFascade, IMediaElementManager mediaElementManager)
        {
            mediaStreamFascade.Builder.RegisterSingleton(mediaElementManager);
        }
    }
}

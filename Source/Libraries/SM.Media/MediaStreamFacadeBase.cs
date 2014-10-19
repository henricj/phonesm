// -----------------------------------------------------------------------
//  <copyright file="MediaStreamFacadeBase.cs" company="Henric Jungheim">
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
using SM.Media.Segments;
using SM.Media.Utility;

namespace SM.Media
{
    public interface IMediaStreamFacadeBase : IDisposable
    {
        /// <summary>
        ///     Force the source to be considered <see cref="SM.Media.Content.ContentType" />.
        ///     The type will be detected if null. Set this value before calling CreateMediaStreamSourceAsync().
        /// </summary>
        /// <seealso cref="SM.Media.Content.ContentTypes" />
        ContentType ContentType { get; set; }

        TimeSpan? SeekTarget { get; set; }
        TsMediaManager.MediaState State { get; }
        IBuilder<IMediaManager> Builder { get; }

        event EventHandler<TsMediaManagerStateEventArgs> StateChange;

        void RequestStop();
        Task StopAsync(CancellationToken cancellationToken);
        Task CloseAsync();
    }

    public interface IMediaStreamFacadeBase<TMediaStreamSource> : IMediaStreamFacadeBase
        where TMediaStreamSource : class
    {
        Task<TMediaStreamSource> CreateMediaStreamSourceAsync(Uri source, CancellationToken cancellationToken);
    }

    public abstract class MediaStreamFacadeBase<TMediaStreamSource> : IMediaStreamFacadeBase<TMediaStreamSource>
        where TMediaStreamSource : class
    {
        readonly AsyncFifoWorker _asyncFifoWorker = new AsyncFifoWorker();
        readonly CancellationTokenSource _disposeCancellationTokenSource = new CancellationTokenSource();
        readonly object _lock = new object();
        readonly IBuilder<IMediaManager> _mediaManagerBuilder;
        CancellationTokenSource _closeCancellationTokenSource = new CancellationTokenSource();
        int _isDisposed;
        IMediaManager _mediaManager;
        ISegmentManager _playlist;

        protected MediaStreamFacadeBase(IBuilder<IMediaManager> mediaManagerBuilder)
        {
            if (mediaManagerBuilder == null)
                throw new ArgumentNullException("mediaManagerBuilder");

            _mediaManagerBuilder = mediaManagerBuilder;
        }

        IMediaManager MediaManager
        {
            get
            {
                lock (_lock)
                {
                    return _mediaManager;
                }
            }
        }

        #region IMediaStreamFacadeBase<TMediaStreamSource> Members

        public void Dispose()
        {
            if (0 != Interlocked.Exchange(ref _isDisposed, 1))
                return;

            Dispose(true);

            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        public ContentType ContentType { get; set; }

        public TimeSpan? SeekTarget
        {
            get
            {
                var mediaManager = MediaManager;

                return null == mediaManager ? null : mediaManager.SeekTarget;
            }
            set
            {
                ThrowIfDisposed();

                var mediaManager = MediaManager;

                if (null == mediaManager)
                    return;

                mediaManager.SeekTarget = value;
            }
        }

        public TsMediaManager.MediaState State
        {
            get
            {
                var mediaManager = MediaManager;

                if (null == mediaManager)
                    return TsMediaManager.MediaState.Closed;

                return mediaManager.State;
            }
        }

        public IBuilder<IMediaManager> Builder
        {
            get { return _mediaManagerBuilder; }
        }

        public event EventHandler<TsMediaManagerStateEventArgs> StateChange;

        public void RequestStop()
        {
            Debug.WriteLine("MediaStreamFacadeBase.RequestStop()");

            ThrowIfDisposed();

            if (_closeCancellationTokenSource.IsCancellationRequested)
                return; // CloseAsync has already been called.

            try
            {
                Post(CloseMediaAsync, "MediaStreamFacadeBase.RequestStop() CloseMediaAsync");
            }
            catch (Exception ex)
            {
                // We have probably just lost a race with CloseAsync()
                Debug.WriteLine("MediaStreamFacadeBase.RequestStop() Post failed: " + ex.Message);
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            Debug.WriteLine("MediaStreamFacadeBase.StopAsync()");

            ThrowIfDisposed();

            using (var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _closeCancellationTokenSource.Token, _disposeCancellationTokenSource.Token))
            {
                await RequestCloseMediaAsync(linkedToken.Token).ConfigureAwait(false);
            }
        }

        public Task CloseAsync()
        {
            Debug.WriteLine("MediaStreamFacadeBase.CloseAsync()");

            return RequestCloseMediaAsync(_disposeCancellationTokenSource.Token);
        }

        public virtual async Task<TMediaStreamSource> CreateMediaStreamSourceAsync(Uri source, CancellationToken cancellationToken)
        {
            if (null == source)
                return null;

            Exception exception;

            try
            {
                var mediaManager = await CreateMediaManagerAsync(source, cancellationToken).ConfigureAwait(false);

                var configurator = await mediaManager.OpenMediaStreamConfiguratorAsync(cancellationToken).ConfigureAwait(false);

                var mss = await configurator.CreateMediaStreamSourceAsync<TMediaStreamSource>(cancellationToken).ConfigureAwait(false);

                return mss;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MediaStreamFacadeBase.CreateAsync() failed: " + ex.Message);

                exception = new AggregateException(ex.Message, ex);
            }

            await CloseAsync().ConfigureAwait(false);

            throw exception;
        }

        #endregion

        void ResetCloseCancellationToken()
        {
            if (!_closeCancellationTokenSource.IsCancellationRequested)
                return;

            _closeCancellationTokenSource.DisposeSafe();
            _closeCancellationTokenSource = new CancellationTokenSource();
        }

        Task RequestCloseMediaAsync(CancellationToken cancellationToken)
        {
            Debug.WriteLine("MediaStreamFacadeBase.RequestCloseMediaAsync()");

            return _asyncFifoWorker.PostAsync(CloseMediaAsync, "MediaStreamFacadeBase.RequestCloseMediaAsync() CloseMediaAsync", cancellationToken);
        }

        void ThrowIfDisposed()
        {
            if (0 != _isDisposed)
                throw new ObjectDisposedException(GetType().Name);
        }

        protected virtual void Dispose(bool disposing)
        {
            Debug.WriteLine("MediaStreamFacadeBase.Dispose({0})", disposing);

            if (!disposing)
                return;

            if (!_disposeCancellationTokenSource.IsCancellationRequested)
                _disposeCancellationTokenSource.Cancel();

            StateChange = null;

            IMediaManager mediaManager;

            lock (_lock)
            {
                mediaManager = _mediaManager;
                _mediaManager = null;
            }

            if (null != mediaManager)
                CleanupMediaManager(mediaManager);

            _mediaManagerBuilder.DisposeSafe();

            _asyncFifoWorker.Dispose();

            _disposeCancellationTokenSource.Dispose();
        }

        void CleanupMediaManager(IMediaManager mediaManager)
        {
            Debug.WriteLine("MediaStreamFacadeBase.CleanupMediaManager()");

            if (null == mediaManager)
                return;

            mediaManager.OnStateChange -= MediaManagerOnStateChange;

            mediaManager.DisposeSafe();

            _mediaManagerBuilder.Destroy(mediaManager);

            Debug.WriteLine("MediaStreamFacadeBase.CleanupMediaManager() completed");
        }

        void Post(Func<Task> work, string description)
        {
            _asyncFifoWorker.Post(work, description, _disposeCancellationTokenSource.Token);
        }

        protected async Task<IMediaManager> CreateMediaManagerAsync(Uri source, CancellationToken cancellationToken)
        {
            IMediaManager mediaManager = null;

            using (var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCancellationTokenSource.Token))
            {
                _asyncFifoWorker.Post(CloseMediaAsync, "MediaStreamFacadeBase.CreateMediaMangerAsync() CloseMediaAsync", linkedToken.Token);

                await _asyncFifoWorker
                    .PostAsync(async () => { mediaManager = await OpenMediaAsync(source).ConfigureAwait(false); }, "MediaStreamFacadeBase.CreateMediaMangerAsync() OpenMediaAsync", linkedToken.Token)
                    .ConfigureAwait(false);
            }

            return mediaManager;
        }

        async Task<IMediaManager> OpenMediaAsync(Uri source)
        {
            Debug.WriteLine("MediaStreamFacadeBase.OpenMediaAsync({0})", source);

            var mediaManager = MediaManager;

            if (null != mediaManager)
                await CloseMediaAsync().ConfigureAwait(false);

            Debug.Assert(null == MediaManager);
            Debug.Assert(null == _playlist);

            if (null == source)
                return null;

            if (!source.IsAbsoluteUri)
            {
                Debug.WriteLine("MediaStreamFacadeBase.OpenMediaAsync() source is not absolute: " + source);
                return null;
            }

            ResetCloseCancellationToken();

            mediaManager = _mediaManagerBuilder.Create();

            mediaManager.ContentType = ContentType;

            mediaManager.OnStateChange += MediaManagerOnStateChange;

            lock (_lock)
            {
                Debug.Assert(null == _mediaManager);

                _mediaManager = mediaManager;
            }

            mediaManager.Source = new[] { source };

            return mediaManager;
        }

        async Task CloseMediaAsync()
        {
            Debug.WriteLine("MediaStreamFacadeBase.CloseMediaAsync()");

            if (!_closeCancellationTokenSource.IsCancellationRequested)
                _closeCancellationTokenSource.Cancel();

            IMediaManager mediaManager;

            lock (_lock)
            {
                mediaManager = _mediaManager;
                _mediaManager = null;
            }

            if (null != mediaManager)
            {
                try
                {
                    Debug.WriteLine("MediaStreamFacadeBase.CloseMediaAsync() calling mediaManager.CloseAsync()");

                    await mediaManager.CloseAsync().ConfigureAwait(false);

                    Debug.WriteLine("MediaStreamFacadeBase.CloseMediaAsync() returned from mediaManager.CloseAsync()");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("MediaStreamFacadeBase.CloseMediaAsync() Media manager close failed: " + ex.Message);
                }

                CleanupMediaManager(mediaManager);
            }

            var playlist = _playlist;

            if (null != playlist)
            {
                _playlist = null;

                playlist.CleanupBackground("MediaStreamFacadeBase.CloseMediaAsync playlist");
            }

            Debug.WriteLine("MediaStreamFacadeBase.CloseMediaAsync() completed");
        }

        void MediaManagerOnStateChange(object sender, TsMediaManagerStateEventArgs e)
        {
            Debug.WriteLine("MediaStreamFacadeBase.MediaManagerOnStateChange() to {0}: {1}", e.State, e.Message);

            if (e.State == TsMediaManager.MediaState.Closed)
            {
                var mediaManager = MediaManager;

                if (null != mediaManager)
                    RequestStop();
            }

            var stateChange = StateChange;

            if (null == stateChange)
                return;

            try
            {
                stateChange(this, e);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MediaStreamFacadeBase.MediaManagerOnStateChange() Exception in StateChange event handler: " + ex.Message);
            }
        }
    }

    public static class MediaStreamFacadeExtensions
    {
        public static void SetParameter(this IMediaStreamFacadeBase mediaStreamFacade, IMediaManagerParameters parameters)
        {
            mediaStreamFacade.Builder.RegisterSingleton(parameters);
        }

        public static void SetParameter(this IMediaStreamFacadeBase mediaStreamFacade, IBufferingPolicy policy)
        {
            mediaStreamFacade.Builder.RegisterSingleton(policy);
        }

        public static void SetParameter(this IMediaStreamFacadeBase mediaStreamFacade, IMediaStreamConfigurator mediaStreamConfigurator)
        {
            mediaStreamFacade.Builder.RegisterSingleton(mediaStreamConfigurator);
        }

        public static void SetParameter(this IMediaStreamFacadeBase mediaStreamFacade, IApplicationInformation applicationInformation)
        {
            mediaStreamFacade.Builder.RegisterSingleton(applicationInformation);
        }
    }
}

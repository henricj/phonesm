// -----------------------------------------------------------------------
//  <copyright file="MediaStreamFacadeBase.cs" company="Henric Jungheim">
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
using SM.Media.Buffering;
using SM.Media.Builder;
using SM.Media.Content;
using SM.Media.MediaManager;
using SM.Media.MediaParser;
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
        MediaManagerState State { get; }
        IBuilder<IMediaManager> Builder { get; }
        bool IsDisposed { get; }

        Task PlayingTask { get; }

        event EventHandler<MediaManagerStateEventArgs> StateChange;

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
        readonly AsyncLock _asyncLock = new AsyncLock();
        readonly CancellationTokenSource _disposeCancellationTokenSource = new CancellationTokenSource();
        readonly object _lock = new object();
        readonly IBuilder<IMediaManager> _mediaManagerBuilder;
        CancellationTokenSource _closeCancellationTokenSource;
        int _isDisposed;
        IMediaManager _mediaManager;

        protected MediaStreamFacadeBase(IBuilder<IMediaManager> mediaManagerBuilder)
        {
            if (mediaManagerBuilder == null)
                throw new ArgumentNullException("mediaManagerBuilder");

            _mediaManagerBuilder = mediaManagerBuilder;

            // ReSharper disable once PossiblyMistakenUseOfParamsMethod
            _closeCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_disposeCancellationTokenSource.Token);
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
            set { SetMediaManager(value); }
        }

        #region IMediaStreamFacadeBase<TMediaStreamSource> Members

        public bool IsDisposed
        {
            get { return 0 != _isDisposed; }
        }

        public Task PlayingTask
        {
            get
            {
                IMediaManager mediaManager;

                lock (_lock)
                {
                    mediaManager = _mediaManager;
                }

                if (null == mediaManager)
                    return TplTaskExtensions.CompletedTask;

                return mediaManager.PlayingTask;
            }
        }

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

        public MediaManagerState State
        {
            get
            {
                var mediaManager = MediaManager;

                if (null == mediaManager)
                    return MediaManagerState.Closed;

                return mediaManager.State;
            }
        }

        public IBuilder<IMediaManager> Builder
        {
            get { return _mediaManagerBuilder; }
        }

        public event EventHandler<MediaManagerStateEventArgs> StateChange;

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            Debug.WriteLine("MediaStreamFacadeBase.StopAsync()");

            ThrowIfDisposed();

            using (var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _closeCancellationTokenSource.Token))
            {
                await StopMediaAsync(MediaManager, linkedToken.Token).ConfigureAwait(false);
            }
        }

        public async Task CloseAsync()
        {
            Debug.WriteLine("MediaStreamFacadeBase.CloseAsync()");

            await CloseMediaManagerAsync(MediaManager).ConfigureAwait(false);
        }

        public virtual async Task<TMediaStreamSource> CreateMediaStreamSourceAsync(Uri source, CancellationToken cancellationToken)
        {
            Debug.WriteLine("MediaStreamFacadeBase.CreateMediaStreamSourceAsync() " + source);

            if (null != source && !source.IsAbsoluteUri)
            {
                throw new ArgumentException("source must be absolute: " + source);

                //Debug.WriteLine("MediaStreamFacadeBase.OpenMediaAsync() source is not absolute: " + source);
                //return null;
            }

            Exception exception;

            // ReSharper disable once PossiblyMistakenUseOfParamsMethod
            var closeCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCancellationTokenSource.Token);

            if (!_closeCancellationTokenSource.IsCancellationRequested)
                _closeCancellationTokenSource.Cancel();

            try
            {
                using (var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, closeCts.Token))
                {
                    linkedToken.CancelAfter(MediaStreamFacadeSettings.Parameters.CreateTimeout);

                    var mediaManager = MediaManager;

                    if (null != mediaManager)
                        await StopMediaAsync(mediaManager, linkedToken.Token).ConfigureAwait(false);

                    _closeCancellationTokenSource.Dispose();
                    _closeCancellationTokenSource = closeCts;

                    if (null == source)
                        return null;

                    mediaManager = MediaManager ?? await CreateMediaManagerAsync(linkedToken.Token).ConfigureAwait(false);

                    var configurator = await mediaManager.OpenMediaAsync(new[] { source }, linkedToken.Token).ConfigureAwait(false);

                    var mss = await configurator.CreateMediaStreamSourceAsync<TMediaStreamSource>(linkedToken.Token).ConfigureAwait(false);

                    return mss;
                }
            }
            catch (OperationCanceledException ex)
            {
                exception = ex;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MediaStreamFacadeBase.CreateAsync() failed: " + ex.ExtendedMessage());

                exception = new AggregateException(ex.Message, ex);
            }

            await CloseAsync().ConfigureAwait(false);

            throw exception;
        }

        #endregion

        void SetMediaManager(IMediaManager value)
        {
            if (ReferenceEquals(_mediaManager, value))
                return;

            IMediaManager mediaManager;

            lock (_lock)
            {
                mediaManager = _mediaManager;
                _mediaManager = value;
            }

            if (null != mediaManager)
            {
                Debug.WriteLine("**** MediaStreamFacadeBase.SetMediaManager() _mediaManager was not null");

                CleanupMediaManager(mediaManager);
            }
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

            if (!_closeCancellationTokenSource.IsCancellationRequested)
                _closeCancellationTokenSource.Cancel();

            if (!_disposeCancellationTokenSource.IsCancellationRequested)
                _disposeCancellationTokenSource.Cancel();

            _asyncLock.LockAsync(CancellationToken.None).Wait();

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

            _asyncLock.Dispose();

            _closeCancellationTokenSource.Dispose();

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

        protected async Task<IMediaManager> CreateMediaManagerAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using (await _asyncLock.LockAsync(cancellationToken).ConfigureAwait(false))
            {
                var mediaManager = MediaManager;

                if (null != mediaManager)
                    throw new InvalidOperationException("A MediaManager already exists");

                cancellationToken.ThrowIfCancellationRequested();

                mediaManager = CreateMediaManager();

                Debug.Assert(null == _mediaManager);

                MediaManager = mediaManager;

                return mediaManager;
            }
        }

        IMediaManager CreateMediaManager()
        {
            Debug.WriteLine("MediaStreamFacadeBase.CreateMediaManager()");

            Debug.Assert(null == MediaManager);

            var mediaManager = _mediaManagerBuilder.Create();

            mediaManager.ContentType = ContentType;

            mediaManager.OnStateChange += MediaManagerOnStateChange;

            return mediaManager;
        }

        async Task StopMediaAsync(IMediaManager mediaManager, CancellationToken cancellationToken)
        {
            Debug.WriteLine("MediaStreamFacadeBase.StopMediaAsync()");

            using (await _asyncLock.LockAsync(cancellationToken).ConfigureAwait(false))
            {
                var mm = MediaManager;

                if (null == mm || !ReferenceEquals(mm, mediaManager))
                    return;

                await mm.StopMediaAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        async Task CloseMediaManagerAsync(IMediaManager mediaManager)
        {
            Debug.WriteLine("MediaStreamFacadeBase.CloseMediaAsync()");

            using (await _asyncLock.LockAsync(CancellationToken.None).ConfigureAwait(false))
            {
                await UnlockedCloseMediaManagerAsync(mediaManager).ConfigureAwait(false);
            }
        }

        async Task UnlockedCloseMediaManagerAsync(IMediaManager mediaManager)
        {
            Debug.WriteLine("MediaStreamFacadeBase.UnlockedCloseMediaAsync()");

            IMediaManager mm;

            lock (_lock)
            {
                mm = _mediaManager;

                if (null == mm || !ReferenceEquals(mm, mediaManager))
                    return;

                _mediaManager = null;
            }

            if (!_closeCancellationTokenSource.IsCancellationRequested)
                _closeCancellationTokenSource.Cancel();

            try
            {
                Debug.WriteLine("MediaStreamFacadeBase.UnlockedCloseMediaManagerAsync() calling mediaManager.CloseAsync()");

                await mm.CloseMediaAsync().ConfigureAwait(false);

                Debug.WriteLine("MediaStreamFacadeBase.UnlockedCloseMediaManagerAsync() returned from mediaManager.CloseAsync()");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MediaStreamFacadeBase.UnlockedCloseMediaManagerAsync() Media manager close failed: " + ex.Message);
            }

            CleanupMediaManager(mm);

            Debug.WriteLine("MediaStreamFacadeBase.UnlockedCloseMediaManagerAsync() completed");
        }

        async void MediaManagerOnStateChange(object sender, MediaManagerStateEventArgs e)
        {
            Debug.WriteLine("MediaStreamFacadeBase.MediaManagerOnStateChange() to {0}: {1}", e.State, e.Message);

            if (e.State == MediaManagerState.Closed)
            {
                try
                {
                    await StopMediaAsync((IMediaManager)sender, _closeCancellationTokenSource.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                { }
                catch (Exception ex)
                {
                    Debug.WriteLine("MediaStreamFacadeBase.MediaManagerOnStateChange() stop failed: " + ex.Message);
                }
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
        public static void RequestStop(this IMediaStreamFacadeBase mediaStreamFacade)
        {
            var stopTask = RequestStopAsync(mediaStreamFacade, TimeSpan.FromSeconds(5), CancellationToken.None);

            TaskCollector.Default.Add(stopTask, "MediaStreamFacade RequestStop");
        }

        public static async Task<bool> RequestStopAsync(this IMediaStreamFacadeBase mediaStreamFacade,
            TimeSpan timeout, CancellationToken cancellationToken)
        {
            // ReSharper disable once PossiblyMistakenUseOfParamsMethod
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                cts.CancelAfter(timeout);

                {
                    try
                    {
                        await mediaStreamFacade.StopAsync(cts.Token).ConfigureAwait(false);

                        return true;
                    }
                    catch (OperationCanceledException)
                    {
                        Debug.WriteLine(!cancellationToken.IsCancellationRequested ? "RequestStop timeout" : "RequestStop canceled");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("RequestStop failed: " + ex.ExtendedMessage());
                    }
                }

                return false;
            }
        }

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

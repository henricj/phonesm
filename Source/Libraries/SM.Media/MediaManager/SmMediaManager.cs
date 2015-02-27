// -----------------------------------------------------------------------
//  <copyright file="SmMediaManager.cs" company="Henric Jungheim">
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SM.Media.Buffering;
using SM.Media.Content;
using SM.Media.MediaParser;
using SM.Media.Segments;
using SM.Media.Utility;
using SM.TsParser;

namespace SM.Media.MediaManager
{
    public sealed class SmMediaManager : IMediaManager
    {
        const int MaxBuffers = 8;
        readonly AsyncLock _asyncLock = new AsyncLock();
        readonly Func<IBufferingManager> _bufferingManagerFactory;
        readonly object _lock = new object();
        readonly IMediaParserFactory _mediaParserFactory;
        readonly IMediaStreamConfigurator _mediaStreamConfigurator;
        readonly Action<IProgramStreams> _programStreamsHandler;
        readonly SignalTask _reportStateTask;
        readonly ISegmentReaderManagerFactory _segmentReaderManagerFactory;
        CancellationTokenSource _closeCancellationTokenSource = new CancellationTokenSource();
        TaskCompletionSource<object> _closeTaskCompletionSource;
        int _isDisposed;
        MediaManagerState _mediaState;
        string _mediaStateMessage;
        int _openCount;
        Task _playTask;
        CancellationTokenSource _playbackCancellationTokenSource = new CancellationTokenSource();
        TaskCompletionSource<object> _playbackTaskCompletionSource = new TaskCompletionSource<object>();
        ISegmentReaderManager _readerManager;
        IMediaReader[] _readers;

        public SmMediaManager(ISegmentReaderManagerFactory segmentReaderManagerFactory,
            IMediaStreamConfigurator mediaStreamConfigurator, Func<IBufferingManager> bufferingManagerFactory,
            IMediaManagerParameters mediaManagerParameters, IMediaParserFactory mediaParserFactory)
        {
            if (null == segmentReaderManagerFactory)
                throw new ArgumentNullException("segmentReaderManagerFactory");
            if (null == mediaStreamConfigurator)
                throw new ArgumentNullException("mediaStreamConfigurator");
            if (null == bufferingManagerFactory)
                throw new ArgumentNullException("bufferingManagerFactory");

            _segmentReaderManagerFactory = segmentReaderManagerFactory;
            _mediaStreamConfigurator = mediaStreamConfigurator;
            _bufferingManagerFactory = bufferingManagerFactory;
            _mediaParserFactory = mediaParserFactory;
            _programStreamsHandler = mediaManagerParameters.ProgramStreamsHandler;

            // Start with a canceled token (i.e., we are not playing)
            _playbackCancellationTokenSource.Cancel();
            _playbackTaskCompletionSource.TrySetResult(null);

            _reportStateTask = new SignalTask(ReportState);
        }

        bool IsClosed
        {
            get
            {
                var state = State;

                return MediaManagerState.Idle == state || MediaManagerState.Closed == state;
            }
        }

        bool IsRunning
        {
            get
            {
                var state = State;

                return MediaManagerState.OpenMedia == state || MediaManagerState.Opening == state
                       || MediaManagerState.Playing == state || MediaManagerState.Seeking == state;
            }
        }

        #region IMediaManager Members

        public void Dispose()
        {
            Debug.WriteLine("SmMediaManager.Dispose()");

            if (0 != Interlocked.Exchange(ref _isDisposed, 1))
                return;

            if (null != OnStateChange)
            {
                Debug.WriteLine("SmMediaManager.Dispose(): OnStateChange is not null");

                if (Debugger.IsAttached)
                    Debugger.Break();

                OnStateChange = null;
            }

            _mediaStreamConfigurator.MediaManager = null;

            CloseAsync()
                .Wait();

            using (_reportStateTask)
            using (_asyncLock)
            { }

            using (_playbackCancellationTokenSource)
            { }

            using (_closeCancellationTokenSource)
            { }
        }

        public MediaManagerState State
        {
            get { lock (_lock) return _mediaState; }
            private set { SetMediaState(value, null); }
        }

        public TimeSpan? SeekTarget
        {
            get { return _mediaStreamConfigurator.SeekTarget; }
            set { _mediaStreamConfigurator.SeekTarget = value; }
        }

        /// <inheritdoc />
        public ContentType ContentType { get; set; }

        public Task PlayingTask
        {
            get { return _playbackTaskCompletionSource.Task; }
        }

        public async Task<IMediaStreamConfigurator> OpenMediaAsync(ICollection<Uri> source, CancellationToken cancellationToken)
        {
            Debug.WriteLine("SmMediaManager.OpenMediaAsync()");

            if (null == source)
                throw new ArgumentNullException("source");

            if (0 == source.Count || source.Any(s => null == s))
                throw new ArgumentException("No valid URLs", "source");

            source = source.ToArray();

            using (await _asyncLock.LockAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!IsClosed)
                    await CloseAsync().ConfigureAwait(false);

                _playbackTaskCompletionSource = new TaskCompletionSource<object>();

                State = MediaManagerState.OpenMedia;

                await OpenAsync(source).ConfigureAwait(false);

                return _mediaStreamConfigurator;
            }
        }

        public async Task StopMediaAsync(CancellationToken cancellationToken)
        {
            Debug.WriteLine("SmMediaManager.StopMediaAsync()");

            if (!IsRunning)
                return;

            using (await _asyncLock.LockAsync(cancellationToken).ConfigureAwait(false))
            {
                await CloseAsync().ConfigureAwait(false);
            }
        }

        public async Task CloseMediaAsync()
        {
            Debug.WriteLine("SmMediaManager.CloseMediaAsync()");

            if (IsClosed)
                return;

            // Is this an unavoidable race or yet another
            // symptom of the unsystematic way the pipeline
            // lifetime is managed?  We catch them for "CloseAsync()"
            // since closing something that is (hopefully) already
            // closed should probably return rather than causing the
            // app to exit.  At any rate, the state of the object
            // at return is what the caller asked for (closed).
            try
            {
                using (await _asyncLock.LockAsync(CancellationToken.None).ConfigureAwait(false))
                {
                    await CloseAsync().ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException ex)
            {
                Debug.WriteLine("SmMediaManager.CloseMediaAsync() operation canceled exception: " + ex.Message);
            }
            catch (ObjectDisposedException ex)
            {
                Debug.WriteLine("SmMediaManager.CloseMediaAsync() object disposed exception: " + ex.Message);
            }
        }

        public async Task<TimeSpan> SeekMediaAsync(TimeSpan position)
        {
            Debug.WriteLine("SmMediaManager.SeekMediaAsync({0})", position);

            using (await _asyncLock.LockAsync(_closeCancellationTokenSource.Token).ConfigureAwait(false))
            {
                return await SeekAsync(position).ConfigureAwait(false);
            }
        }

        public event EventHandler<MediaManagerStateEventArgs> OnStateChange;

        #endregion

        async Task CloseAsync()
        {
            Debug.WriteLine("SmMediaManager.CloseAsync()");

            var closeTaskCompletionSource = new TaskCompletionSource<object>();

            var currentCloseTaskCompletionSource = Interlocked.CompareExchange(ref _closeTaskCompletionSource, closeTaskCompletionSource, null);

            if (null != currentCloseTaskCompletionSource)
            {
                await currentCloseTaskCompletionSource.Task.ConfigureAwait(false);

                Debug.WriteLine("SmMediaManager.CloseAsync() completed by other caller");

                return;
            }

            State = MediaManagerState.Closing;

            var playbackTaskCompletionSource = _playbackTaskCompletionSource;

            _closeCancellationTokenSource.Cancel();

            await CloseCleanupAsync().ConfigureAwait(false);

            State = MediaManagerState.Closed;

            await _reportStateTask.WaitAsync().ConfigureAwait(false);

            Debug.WriteLine("SmMediaManager.CloseAsync() completed");

            Interlocked.CompareExchange(ref _closeTaskCompletionSource, null, closeTaskCompletionSource);

            var task = TaskEx.Run(() =>
            {
                closeTaskCompletionSource.TrySetResult(null);
                playbackTaskCompletionSource.TrySetResult(null);
            });

            TaskCollector.Default.Add(task, "SmMediaManager close");
        }

        async Task CloseCleanupAsync()
        {
            Debug.WriteLine("SmMediaManager.CloseCleanupAsync()");

            var tasks = new List<Task>();

            var readerManager = _readerManager;

            if (null != readerManager)
            {
                //Debug.WriteLine("SmMediaManager.CloseCleanupAsync() calling readerManager.StopAsync()");

                _readerManager = null;

                tasks.Add(readerManager.StopAsync());
            }

            var msc = _mediaStreamConfigurator;

            if (null != msc)
                tasks.Add(msc.CloseAsync());

            if (null != _readers && _readers.Length > 0)
                tasks.Add(CloseReadersAsync());

            if (null != _playTask)
                tasks.Add(_playTask);

            if (tasks.Count > 0)
            {
                while (tasks.Any(t => !t.IsCompleted))
                    try
                    {
                        var t = TaskEx.Delay(2500);

                        Debug.WriteLine("SmMediaManager.CloseCleanupAsync() waiting for tasks");
                        await TaskEx.WhenAny(t, TaskEx.WhenAll(tasks)).ConfigureAwait(false);
                        Debug.WriteLine("SmMediaManager.CloseCleanupAsync() finished tasks");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("SmMediaManager.CloseCleanupAsync() play task failed: " + ex.ExtendedMessage());
                    }
            }

            if (null != msc)
                msc.MediaManager = null;

            DisposeReaders();

            if (null != readerManager)
                readerManager.DisposeSafe();
        }

        Task ReportState()
        {
            Debug.WriteLine("SmMediaManager.ReportState() state {0} message {1}", _mediaState, _mediaStateMessage);

            MediaManagerState state;
            string message;

            lock (_lock)
            {
                state = _mediaState;
                message = _mediaStateMessage;
                _mediaStateMessage = null;
            }

            var handlers = OnStateChange;

            if (null != handlers)
                handlers(this, new MediaManagerStateEventArgs(state, message));

            if (null != message)
            {
                var mss = _mediaStreamConfigurator;

                if (null != mss)
                    mss.ReportError(message);
            }

            return TplTaskExtensions.CompletedTask;
        }

        void ResetCancellationToken()
        {
            Debug.WriteLine("SmMediaManager.ResetCancellationToken()");

            if (_closeCancellationTokenSource.IsCancellationRequested)
            {
                _closeCancellationTokenSource.CancelDisposeSafe();

                _closeCancellationTokenSource = new CancellationTokenSource();
            }

            if (_playbackCancellationTokenSource.IsCancellationRequested)
            {
                _playbackCancellationTokenSource.CancelDisposeSafe();

                // ReSharper disable once PossiblyMistakenUseOfParamsMethod
                _playbackCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_closeCancellationTokenSource.Token);
            }
        }

        void SetMediaState(MediaManagerState state, string message)
        {
            lock (_lock)
            {
                if (state == _mediaState)
                    return;

                Debug.WriteLine("SmMediaManager.SetMediaState() {0} -> {1}", _mediaState, state);

                _mediaState = state;

                if (null != message)
                    _mediaStateMessage = message;
            }

            _reportStateTask.Fire();
        }

        void StartReaders()
        {
            var token = _playbackCancellationTokenSource.Token;

            var tasks = _readers.Select(r => r.ReadAsync(token) as Task);

            var cleanupTask = TaskEx.WhenAll(tasks)
                .ContinueWith(
                    async t =>
                    {
                        var ex = t.Exception;

                        if (null == ex)
                            return; // This should never happen ("OnlyOnFaulted" below)

                        Debug.WriteLine("SmMediaManager.StartReaders() ReadAsync failed: " + ex.ExtendedMessage());
                        SetMediaState(MediaManagerState.Error, ex.ExtendedMessage());

                        lock (_lock)
                        {
                            if (null != _closeTaskCompletionSource)
                                return;
                        }

                        try
                        {
                            await CloseMediaAsync().ConfigureAwait(false);
                        }
                        catch (Exception ex2)
                        {
                            Debug.WriteLine("SmMediaManager.StartReaders() ReadAsync close media failed " + ex2);
                        }
                    }, token, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);

            TaskCollector.Default.Add(cleanupTask, "SmMediaManager.StartReaders() cleanup tasks");
        }

        async Task OpenAsync(ICollection<Uri> source)
        {
            Debug.WriteLine("SmMediaManager.OpenAsync() state " + State);

            State = MediaManagerState.Opening;

            ++_openCount;

            ResetCancellationToken();

            Exception exception;

            Task<IMediaReader>[] readerTasks = null;

            try
            {
                _readerManager = await _segmentReaderManagerFactory.CreateAsync(
                    new SegmentManagerParameters
                    {
                        Source = source
                    }, ContentType, _playbackCancellationTokenSource.Token).ConfigureAwait(false);

                if (null == _readerManager)
                {
                    Debug.WriteLine("SmMediaManager.OpenAsync() unable to create reader manager");

                    SetMediaState(MediaManagerState.Error, "Unable to create reader");

                    return;
                }

                readerTasks = _readerManager.SegmentManagerReaders
                    .Select(CreateReaderPipeline)
                    .ToArray();

                _readers = await TaskEx.WhenAll<IMediaReader>(readerTasks)
                    .ConfigureAwait(false);

                foreach (var reader in _readers)
                    reader.IsEnabled = true;

                await _readerManager.StartAsync(_playbackCancellationTokenSource.Token).ConfigureAwait(false);

                _mediaStreamConfigurator.MediaManager = this;

                StartReaders();

                return;
            }
            catch (OperationCanceledException ex)
            {
                SetMediaState(MediaManagerState.Error, "Media play canceled");

                exception = ex;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SmMediaManager.OpenAsync() failed: " + ex.Message);

                SetMediaState(MediaManagerState.Error, "Unable to play media");

                exception = new AggregateException(ex.Message, ex);
            }

            await CleanupFailedOpenAsync(readerTasks);

            throw exception;
        }

        async Task CleanupFailedOpenAsync(Task<IMediaReader>[] readerTasks)
        {
            Debug.WriteLine("SmMediaManager.CleanupFailedOpenAsync() state " + State);

            _playbackCancellationTokenSource.Cancel();

            if (null == _readers && null != readerTasks)
            {
                // Clean up any stragglers.
                _readers = readerTasks.Where(
                    r =>
                    {
                        if (null == r)
                            return false;

                        var readerException = r.Exception;

                        if (null != readerException)
                        {
                            Debug.WriteLine("SmMediaManager.CleanupFailedOpenAsync(): reader create failed: " + readerException.Message);
                            return false;
                        }

                        return r.IsCompleted;
                    })
                    .Select(r => r.Result)
                    .ToArray();

                await CloseReadersAsync().ConfigureAwait(false);

                DisposeReaders();
            }

            if (null != _readerManager)
            {
                _readerManager.DisposeSafe();
                _readerManager = null;
            }
        }

        async Task<IMediaReader> CreateReaderPipeline(ISegmentManagerReaders segmentManagerReaders)
        {
            var reader = new MediaReader(_bufferingManagerFactory(), _mediaParserFactory, segmentManagerReaders, new BlockingPool<WorkBuffer>(MaxBuffers));

            await reader.InitializeAsync(segmentManagerReaders, CheckConfigurationCompleted,
                _mediaStreamConfigurator.CheckForSamples,
                _playbackCancellationTokenSource.Token, _programStreamsHandler)
                .ConfigureAwait(false);

            return
                reader;
        }

        void CheckConfigurationCompleted()
        {
            var state = State;

            if (MediaManagerState.Opening != state && MediaManagerState.OpenMedia != state)
                return;

            if (null == _readers || _readers.Any(r => !r.IsConfigured))
                return;

            _playTask = _mediaStreamConfigurator.PlayAsync(_readers.SelectMany(r => r.MediaStreams), _readerManager.Duration, _closeCancellationTokenSource.Token);

            State = MediaManagerState.Playing;

            var openCount = _openCount;

            _playTask.ContinueWith(async t =>
            {
                var taskException = t.Exception;

                if (null != taskException)
                    Debug.WriteLine("SmMediaManager.CheckConfigurationComplete() play task failed: " + taskException.Message);

                try
                {
                    using (await _asyncLock.LockAsync(CancellationToken.None).ConfigureAwait(false))
                    {
                        if (openCount != _openCount)
                            return;

                        await CloseAsync().ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("SmMediaManager.CheckConfigurationComplete() play continuation failed: " + ex.Message);
                }
            });
        }

        async Task CloseReadersAsync()
        {
            Debug.WriteLine("SmMediaManager.CloseReadersAsync() closing readers");

            if (null == _readers || _readers.Length < 1)
                return;

            try
            {
                var tasks = _readers.Select(
                    async reader =>
                    {
                        if (null == reader)
                            return;

                        try
                        {
                            await reader.CloseAsync().ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("SmMediaManager.CloseReadersAsync(): reader.CloseAsync failed: " + ex.Message);
                        }
                    })
                    .Where(t => null != t);

                await TaskEx.WhenAll(tasks).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SmMediaManager.CloseReadersAsync: task failed: " + ex.Message);
            }

            Debug.WriteLine("SmMediaManager.CloseReadersAsync() readers closed");
        }

        void DisposeReaders()
        {
            Debug.WriteLine("SmMediaManager.DisposeReaders()");

            var readers = _readers;

            _readers = null;

            if (null == readers || readers.Length < 1)
                return;

            foreach (var reader in readers)
                reader.DisposeBackground("SmMediaManager dispose reader");

            Debug.WriteLine("SmMediaManager.DisposeReaders() completed");
        }

        bool IsSeekInRange(TimeSpan position)
        {
            return _readers.All(reader => reader.IsBuffered(position));
        }

        async Task<TimeSpan> SeekAsync(TimeSpan position)
        {
            Debug.WriteLine("SmMediaManager.SeekAsync()");

            if (_playbackCancellationTokenSource.IsCancellationRequested)
                return TimeSpan.MinValue;

            try
            {
                if (IsSeekInRange(position))
                    return position;

                var readers = _readers;

                if (null == readers || readers.Length < 1)
                    return TimeSpan.MinValue;

                await TaskEx.WhenAll(readers.Select(reader => reader.StopAsync())).ConfigureAwait(false);

                if (_playbackCancellationTokenSource.IsCancellationRequested)
                    return TimeSpan.MinValue;

                foreach (var reader in readers)
                    reader.IsEnabled = true;

                State = MediaManagerState.Seeking;

                var actualPosition = await _readerManager.SeekAsync(position, _playbackCancellationTokenSource.Token).ConfigureAwait(false);

                StartReaders();

                return actualPosition;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SmMediaManager.SeekAsync() failed: " + ex.Message);
            }

            return TimeSpan.MinValue;
        }
    }
}

// -----------------------------------------------------------------------
//  <copyright file="TsMediaManager.cs" company="Henric Jungheim">
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

namespace SM.Media
{
    public class TsMediaManagerStateEventArgs : EventArgs
    {
        public readonly string Message;
        public readonly TsMediaManager.MediaState State;

        public TsMediaManagerStateEventArgs(TsMediaManager.MediaState state, string message = null)
        {
            State = state;
            Message = message;
        }
    }

    public sealed class TsMediaManager : IMediaManager
    {
        #region MediaState enum

        public enum MediaState
        {
            Idle,
            Opening,
            OpenMedia,
            Seeking,
            Playing,
            Closed,
            Error,
            Closing
        }

        #endregion

        const int MaxBuffers = 8;
        readonly AsyncFifoWorker _asyncFifoWorker = new AsyncFifoWorker();
        readonly Func<IBufferingManager> _bufferingManagerFactory;
        readonly CancellationTokenSource _closeCancellationTokenSource = new CancellationTokenSource();
        readonly object _lock = new object();
        readonly IMediaParserFactory _mediaParserFactory;
        readonly IMediaStreamSource _mediaStreamSource;
        readonly Action<IProgramStreams> _programStreamsHandler;
        readonly SignalTask _reportStateTask;
        readonly ISegmentReaderManagerFactory _segmentReaderManagerFactory;
        TaskCompletionSource<object> _closeTaskCompletionSource;
        int _isDisposed;
        MediaState _mediaState;
        string _mediaStateMessage;
        CancellationTokenSource _playbackCancellationTokenSource;
        ISegmentReaderManager _readerManager;
        IMediaReader[] _readers;

        public TsMediaManager(ISegmentReaderManagerFactory segmentReaderManagerFactory,
            IMediaStreamSource mediaStreamSource, Func<IBufferingManager> bufferingManagerFactory,
            IMediaManagerParameters mediaManagerParameters, IMediaParserFactory mediaParserFactory)
        {
            if (null == segmentReaderManagerFactory)
                throw new ArgumentNullException("segmentReaderManagerFactory");
            if (null == mediaStreamSource)
                throw new ArgumentNullException("mediaStreamSource");
            if (null == bufferingManagerFactory)
                throw new ArgumentNullException("bufferingManagerFactory");

            _segmentReaderManagerFactory = segmentReaderManagerFactory;
            _mediaStreamSource = mediaStreamSource;
            _bufferingManagerFactory = bufferingManagerFactory;
            _mediaParserFactory = mediaParserFactory;
            _programStreamsHandler = mediaManagerParameters.ProgramStreamsHandler;

            _mediaStreamSource.MediaManager = this;

            ResetCancellationToken();

            // Start with a canceled token (i.e., we are not playing)
            _playbackCancellationTokenSource.Cancel();

            _reportStateTask = new SignalTask(ReportState);
        }

        #region IMediaManager Members

        public void Dispose()
        {
            Debug.WriteLine("TsMediaManager.Dispose()");

            if (0 != Interlocked.Exchange(ref _isDisposed, 1))
                return;

            if (null != OnStateChange)
            {
                Debug.WriteLine("TsMediaManager.Dispose(): OnStateChange is not null");

                if (Debugger.IsAttached)
                    Debugger.Break();

                OnStateChange = null;
            }

            _mediaStreamSource.MediaManager = null;

            CloseAsync()
                .Wait();

            using (_reportStateTask)
            using (_asyncFifoWorker)
            { }

            using (_playbackCancellationTokenSource)
            { }

            using (_closeCancellationTokenSource)
            { }
        }

        public MediaState State
        {
            get { return _mediaState; }
            private set { SetMediaState(value, null); }
        }

        public TimeSpan? SeekTarget
        {
            get { return _mediaStreamSource.SeekTarget; }
            set { _mediaStreamSource.SeekTarget = value; }
        }

        public IMediaStreamSource MediaStreamSource
        {
            get { return _mediaStreamSource; }
        }

        /// <inheritdoc />
        public ContentType ContentType { get; set; }

        public ICollection<Uri> Source { get; set; }

        public void OpenMedia()
        {
            Debug.WriteLine("TsMediaManager.OpenMedia()");

            _asyncFifoWorker.Post(() =>
                                  {
                                      Debug.WriteLine("TsMediaManager.OpenMedia() handler");

                                      State = MediaState.OpenMedia;

                                      return OpenMediaAsync();
                                  }, "TsMediaManager.OpenMedia() OpenMediaAsync", _closeCancellationTokenSource.Token);
        }

        public void CloseMedia()
        {
            Debug.WriteLine("TsMediaManager.CloseMedia()");

            // Is this an unavoidable race or yet another
            // symptom of the unsystematic way the pipeline
            // lifetime is managed?  We catch them for "CloseMedia()"
            // since closing something that is (hopefully) already
            // closed should probably return rather than causing the
            // app to exit.  At any rate, the state of the object
            // at return is what the caller asked for (closed).
            try
            {
                _asyncFifoWorker.Post(CloseAsync, "TsMediaManager.CloseMedia() CloseAsync", CancellationToken.None);
            }
            catch (OperationCanceledException ex)
            {
                Debug.WriteLine("TsMediaManager.CloseMedia() operation canceled exception: " + ex.Message);
            }
            catch (ObjectDisposedException ex)
            {
                Debug.WriteLine("TsMediaManager.CloseMedia() object disposed exception: " + ex.Message);
            }
        }

        public async Task<TimeSpan> SeekMediaAsync(TimeSpan position)
        {
            Debug.WriteLine("TsMediaManager.SeekMediaAsync({0})", position);

            var token = _playbackCancellationTokenSource.Token;

            TimeSpan actualPosition;

            await _asyncFifoWorker.PostAsync(async () => actualPosition = await SeekAsync(position).ConfigureAwait(false), "TsMediaManager.SeekMediaAsync() SeekAsync", token).ConfigureAwait(false);

            return actualPosition;
        }

        public event EventHandler<TsMediaManagerStateEventArgs> OnStateChange;

        public async Task CloseAsync()
        {
            Debug.WriteLine("TsMediaManager.CloseAsync()");

            TaskCompletionSource<object> closeTaskCompletionSource;
            var isClosing = true;

            lock (_lock)
            {
                closeTaskCompletionSource = _closeTaskCompletionSource;

                if (null == closeTaskCompletionSource)
                {
                    isClosing = false;

                    closeTaskCompletionSource = new TaskCompletionSource<object>();

                    _closeTaskCompletionSource = closeTaskCompletionSource;
                }
            }

            if (isClosing)
            {
                await closeTaskCompletionSource.Task.ConfigureAwait(false);

                Debug.WriteLine("TsMediaManager.CloseAsync() completed by other caller");

                return;
            }

            State = MediaState.Closing;

            _closeCancellationTokenSource.Cancel();

            Task stopPlaylistTask = null;

            var readerManager = _readerManager;

            if (null != readerManager)
            {
                Debug.WriteLine("TsMediaManager.CloseAsync() calling readerManager.StopAsync()");

                _readerManager = null;

                stopPlaylistTask = readerManager.StopAsync();
            }

            var mss = _mediaStreamSource;

            Task drainTask = null;

            if (null != mss)
            {
                Debug.WriteLine("TsMediaManager.CloseAsync() calling _mediaStreamSource.CloseAsync()");

                drainTask = mss.CloseAsync();
            }

            if (null != _readers && _readers.Length > 0)
                await CloseReadersAsync().ConfigureAwait(false);

            if (null != stopPlaylistTask)
            {
                try
                {
                    await stopPlaylistTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // This is normal...
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("TsMediaManager.CloseAsync() stop failed: " + ex.Message);
                }
            }

            if (null != drainTask)
            {
                try
                {
                    //Debug.WriteLine("TsMediaManager.CloseAsync() waiting for _mediaStreamSource.CloseAsync()");

                    await drainTask.ConfigureAwait(false);

                    //Debug.WriteLine("TsMediaManager.CloseAsync() finished _mediaStreamSource.CloseAsync()");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("TsMediaManager.CloseAsync() drain failed: " + ex.Message);
                }
            }

            if (null != mss)
                mss.MediaManager = null;

            DisposeReaders();

            if (null != readerManager)
                readerManager.DisposeSafe();

            State = MediaState.Closed;

            await _reportStateTask.WaitAsync().ConfigureAwait(false);

            var t = Task.Factory.StartNew(s => ((TaskCompletionSource<object>)s).TrySetResult(string.Empty),
                closeTaskCompletionSource, CancellationToken.None, TaskCreationOptions.PreferFairness, TaskScheduler.Default);

            TaskCollector.Default.Add(t, "TsMediaManager close");

            Debug.WriteLine("TsMediaManager.CloseAsync() completed");
        }

        #endregion

        Task ReportState()
        {
            //Debug.WriteLine("TsMediaManager.ReportState() state {0} message {1}", _mediaState, _mediaStateMessage);

            MediaState state;
            string message;

            lock (_lock)
            {
                state = _mediaState;
                message = _mediaStateMessage;
                _mediaStateMessage = null;
            }

            var handlers = OnStateChange;

            if (null != handlers)
                handlers(this, new TsMediaManagerStateEventArgs(state, message));

            if (null != message)
            {
                var mss = _mediaStreamSource;

                if (null != mss)
                    mss.ReportError(message);
            }

            return TplTaskExtensions.CompletedTask;
        }

        void ResetCancellationToken()
        {
            var oldPcts = _playbackCancellationTokenSource;

            CancellationTokenSource newPcts = null;

            for (; ; )
            {
                if (null != oldPcts && !oldPcts.IsCancellationRequested)
                {
                    if (null != newPcts)
                        newPcts.Dispose();

                    return;
                }

                if (null == newPcts)
                {
                    // ReSharper disable once PossiblyMistakenUseOfParamsMethod
                    newPcts = CancellationTokenSource.CreateLinkedTokenSource(_closeCancellationTokenSource.Token);
                }

                var pcts = Interlocked.CompareExchange(ref _playbackCancellationTokenSource, newPcts, oldPcts);

                if (pcts == oldPcts)
                {
                    if (null != oldPcts)
                    {
                        if (!oldPcts.IsCancellationRequested)
                            oldPcts.Cancel();

                        oldPcts.DisposeSafe();
                    }

                    return;
                }

                oldPcts = pcts;
            }
        }

        void SetMediaState(MediaState state, string message)
        {
            lock (_lock)
            {
                if (state == _mediaState)
                    return;

                _mediaState = state;

                if (null != message)
                    _mediaStateMessage = message;
            }

            _reportStateTask.Fire();
        }

        void StartReaders()
        {
            foreach (var reader in _readers)
            {
                var startReader = reader.StartAsync(_playbackCancellationTokenSource.Token);

                startReader.ContinueWith(t => CloseMedia(), TaskContinuationOptions.OnlyOnFaulted);
            }
        }

        async Task OpenMediaAsync()
        {
            Debug.WriteLine("TsMediaManager.OpenMediaAsync() state " + State);

            if (MediaState.Error == State)
                await StopAsync().ConfigureAwait(false);

            if (MediaState.OpenMedia != State)
            {
                Debug.WriteLine("TsMediaManager.OpenMediaAsync() leaving early " + State);
                return;
            }

            State = MediaState.Opening;

            ResetCancellationToken();

            Exception exception;

            Task<IMediaReader>[] readerTasks = null;

            try
            {
                _readerManager = await _segmentReaderManagerFactory.CreateAsync(
                    new SegmentManagerParameters
                    {
                        Source = Source.ToArray()
                    }, ContentType, _playbackCancellationTokenSource.Token).ConfigureAwait(false);

                if (null == _readerManager)
                {
                    Debug.WriteLine("TsMediaManager.OpenMediaAsync() unable to create reader manager");

                    SetMediaState(MediaState.Error, "Unable to create reader");

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

                StartReaders();

                return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("TsMediaManager.OpenMediaAsync failed: " + ex.Message);

                SetMediaState(MediaState.Error, "Unable to play media");

                exception = new AggregateException(ex.Message, ex);
            }

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
                            Debug.WriteLine("TsMediaManager.OpenMediaAsync(): reader create failed: " + readerException.Message);
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

            throw exception;
        }

        async Task<IMediaReader> CreateReaderPipeline(ISegmentManagerReaders segmentManagerReaders)
        {
            var reader = new MediaReader(_bufferingManagerFactory(), _mediaParserFactory, segmentManagerReaders, new BlockingPool<WorkBuffer>(MaxBuffers));

            await reader.InitializeAsync(segmentManagerReaders, CheckConfigurationCompleted,
                () => _mediaStreamSource.CheckForSamples(),
                _playbackCancellationTokenSource.Token, _programStreamsHandler)
                        .ConfigureAwait(false);

            return
                reader;
        }

        void CheckConfigurationCompleted()
        {
            var state = State;

            if (MediaState.Opening != state && MediaState.OpenMedia != state)
                return;

            if (null == _readers || _readers.Any(r => !r.IsConfigured))
                return;

            _mediaStreamSource.Configure(_readers.SelectMany(r => r.MediaStreams), _readerManager.Duration);

            State = MediaState.Playing;
        }

        async Task StopAsync()
        {
            _playbackCancellationTokenSource.Cancel();

            if (null == _readers || _readers.Length < 1)
                return;

            var tasks = _readers.Select(reader => reader.StopAsync());

            await TaskEx.WhenAll(tasks).ConfigureAwait(false);
        }

        async Task CloseReadersAsync()
        {
            //Debug.WriteLine("TsMediaManager.CloseAsync() closing readers");

            if (null == _readers || _readers.Length < 1)
                return;

            try
            {
                var tasks = _readers.Select(
                    reader =>
                    {
                        if (null == reader)
                            return null;

                        try
                        {
                            return reader.CloseAsync();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("TsMediaManager.CloseReadersAsync(): reader.CloseAsync failed: " + ex.Message);
                        }

                        return null;
                    })
                                    .Where(t => null != t);

                await TaskEx.WhenAll(tasks).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("TsMediaManager.CloseAsync: task failed: " + ex.Message);
            }

            //Debug.WriteLine("TsMediaManager.CloseAsync() readers closed");
        }

        void DisposeReaders()
        {
            //Debug.WriteLine("TsMediaManager.DisposeReaders()");

            var readers = _readers;

            _readers = null;

            if (null == readers || readers.Length < 1)
                return;

            foreach (var reader in readers)
                reader.DisposeBackground("TsMediaManager dispose reader");

            //Debug.WriteLine("TsMediaManager.DisposeReaders() completed");
        }

        bool IsSeekInRange(TimeSpan position)
        {
            return _readers.All(reader => reader.IsBuffered(position));
        }

        async Task<TimeSpan> SeekAsync(TimeSpan position)
        {
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

                State = MediaState.Seeking;

                var actualPosition = await _readerManager.SeekAsync(position, _playbackCancellationTokenSource.Token).ConfigureAwait(false);

                StartReaders();

                return actualPosition;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("TsMediaManager.SeekAsync() failed: " + ex.Message);
            }

            return TimeSpan.MinValue;
        }
    }
}

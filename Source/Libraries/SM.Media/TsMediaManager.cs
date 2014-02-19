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
using SM.Media.Configuration;
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

    public sealed class TsMediaManager : IMediaManager, IDisposable
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
        readonly IMediaElementManager _mediaElementManager;
        readonly IMediaParserFactory _mediaParserFactory;
        readonly IMediaStreamSource _mediaStreamSource;
        readonly Action<IProgramStreams> _programStreamsHandler;
        readonly SignalTask _reportStateTask;
        readonly ISegmentReaderManagerFactory _segmentReaderManagerFactory;
        int _isDisposed;
        MediaState _mediaState;
        string _mediaStateMessage;
        CancellationTokenSource _playbackCancellationTokenSource;
        ISegmentReaderManager _readerManager;
        ReaderPipeline[] _readers;

        public TsMediaManager(ISegmentReaderManagerFactory segmentReaderManagerFactory, IMediaElementManager mediaElementManager,
            IMediaStreamSource mediaStreamSource, Func<IBufferingManager> bufferingManagerFactory,
            IMediaManagerParameters mediaManagerParameters, IMediaParserFactory mediaParserFactory)
        {
            if (null == segmentReaderManagerFactory)
                throw new ArgumentNullException("segmentReaderManagerFactory");
            if (null == mediaElementManager)
                throw new ArgumentNullException("mediaElementManager");
            if (null == mediaStreamSource)
                throw new ArgumentNullException("mediaStreamSource");
            if (null == bufferingManagerFactory)
                throw new ArgumentNullException("bufferingManagerFactory");

            _segmentReaderManagerFactory = segmentReaderManagerFactory;
            _mediaElementManager = mediaElementManager;
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

        bool IsClosed
        {
            get { return _closeCancellationTokenSource.IsCancellationRequested; }
        }

        #region IDisposable Members

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

            using (_playbackCancellationTokenSource)
            { }

            using (_closeCancellationTokenSource)
            { }

            using (_reportStateTask)
            using (_asyncFifoWorker)
            { }
        }

        #endregion

        #region IMediaManager Members

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
                                  });
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
                _asyncFifoWorker.Post(CloseAsync);
            }
            catch (OperationCanceledException ex)
            {
                Debug.WriteLine("TsMediaManager.CloseMedia() operation cancelled exception: " + ex.Message);
            }
            catch (ObjectDisposedException ex)
            {
                Debug.WriteLine("TsMediaManager.CloseMedia() object disposed exception: " + ex.Message);
            }
        }

        public Task<TimeSpan> SeekMediaAsync(TimeSpan position)
        {
            Debug.WriteLine("TsMediaManager.SeekMediaAsync({0})", position);

            var token = _playbackCancellationTokenSource.Token;

            return StartWorkAsync(() => SeekAsync(position), token);
        }

        public event EventHandler<TsMediaManagerStateEventArgs> OnStateChange;

        public async Task CloseAsync()
        {
            Debug.WriteLine("TsMediaManager.CloseAsync()");

            if (IsClosed)
                return;

            State = MediaState.Closing;

            _closeCancellationTokenSource.Cancel();

            Task stopPlaylistTask = null;

            var readerManager = _readerManager;

            if (null != readerManager)
            {
                //Debug.WriteLine("TsMediaManager.CloseAsync() calling readerManager.StopAsync()");

                _readerManager = null;

                stopPlaylistTask = readerManager.StopAsync();
            }

            var mss = _mediaStreamSource;

            Task drainTask = null;

            if (null != mss)
            {
                //Debug.WriteLine("TsMediaManager.CloseAsync() calling _mediaStreamSource.CloseAsync()");

                drainTask = mss.CloseAsync();
            }

            string error = null;

            if (null != _readers && _readers.Length > 0)
            {
                try
                {
                    await CloseReadersAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("TsMediaManager.CloseAsync: " + ex.Message);
                    error = "Close failed: " + ex.Message;
                }
            }

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

            try
            {
                //Debug.WriteLine("TsMediaManager.CloseAsync() calling _mediaElementManager.CloseAsync()");

                await _mediaElementManager.CloseAsync().ConfigureAwait(false);

                //Debug.WriteLine("TsMediaManager.CloseAsync() returned from _mediaElementManager.CloseAsync()");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("TsMediaManager.CloseAsync() media element manager CloseAsync failed: " + ex.Message);
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

            if (null != _readers && _readers.Length > 0)
                DisposeReaders();

            if (null != readerManager)
                readerManager.DisposeSafe();

            if (null == error)
                State = MediaState.Closed;
            else
                SetMediaState(MediaState.Error, error);

            Debug.WriteLine("TsMediaManager.CloseAsync() completed");
        }

        #endregion

        Task ReportState()
        {
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
            if (null != _playbackCancellationTokenSource && !_playbackCancellationTokenSource.IsCancellationRequested)
                return;

            using (_playbackCancellationTokenSource)
            { }

            // ReSharper disable once PossiblyMistakenUseOfParamsMethod
            _playbackCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_closeCancellationTokenSource.Token);
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

            Task<ReaderPipeline>[] readerTasks = null;

            try
            {
                _readerManager = await _segmentReaderManagerFactory.CreateAsync(Source.ToArray(), ContentType, _playbackCancellationTokenSource.Token).ConfigureAwait(false);

                readerTasks = _readerManager.SegmentManagerReaders
                                            .Select(CreateReaderPipeline)
                                            .ToArray();

                _readers = await TaskEx.WhenAll<ReaderPipeline>(readerTasks)
                                       .ConfigureAwait(false);

                foreach (var reader in _readers)
                    reader.QueueWorker.IsEnabled = true;

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

            if (null == _readers && null != readerTasks)
            {
                // Clean up any stragglers.
                var cleanupReaderTasks = readerTasks.Where(r => null != r);

                foreach (var readerTask in cleanupReaderTasks)
                {
                    var readerException = readerTask.Exception;

                    if (null != readerException)
                    {
                        Debug.WriteLine("TsMediaManager.OpenMediaAsync(): reader create failed: " + readerException.Message);
                        continue;
                    }

                    var reader = readerTask.Result;

                    try
                    {
                        await reader.StopAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("TsMediaManager.OpenMediaAsync(): reader cleanup failed: " + ex.Message);
                    }
                }

                if (null != _readerManager)
                {
                    _readerManager.DisposeSafe();
                    _readerManager = null;
                }
            }

            throw exception;
        }

        async Task<ReaderPipeline> CreateReaderPipeline(ISegmentManagerReaders segmentManagerReaders)
        {
            var reader = new ReaderPipeline
                         {
                             SegmentReaders = segmentManagerReaders,
                             BlockingPool = new BlockingPool<WorkBuffer>(MaxBuffers),
                         };

            var startReaderTask = reader.SegmentReaders.Manager.StartAsync();

            var localReader = reader;

            reader.QueueWorker = new QueueWorker<WorkBuffer>(
                wi =>
                {
                    var mediaParser = localReader.MediaParser;

                    if (null == wi)
                        mediaParser.ProcessEndOfData();
                    else
                        mediaParser.ProcessData(wi.Buffer, 0, wi.Length);
                }, reader.BlockingPool.Free);

            reader.CallbackReader = new CallbackReader(segmentManagerReaders.Readers, reader.QueueWorker.Enqueue, reader.BlockingPool);

            reader.BufferingManager = _bufferingManagerFactory();

            reader.BufferingManager.Initialize(reader.QueueWorker, () => _mediaStreamSource.CheckForSamples());

            await startReaderTask.ConfigureAwait(false);

            var contentType = reader.SegmentReaders.Manager.ContentType;

            if (null == contentType)
                throw new NotSupportedException("Unknown content type");

            await InitializeMediaParser(reader, contentType).ConfigureAwait(false);

            return reader;
        }

        async Task InitializeMediaParser(ReaderPipeline reader, ContentType contentType)
        {
            Debug.WriteLine("TsMediaManager.InitializeMediaParser() for " + contentType);

            var mediaParserParameters = new MediaParserParameters(reader.BufferingManager, _mediaStreamSource.CheckForSamples);

            var mediaParser = await _mediaParserFactory.CreateAsync(mediaParserParameters, contentType, CancellationToken.None).ConfigureAwait(false);

            if (null == mediaParser)
                throw new NotSupportedException("Unsupported content type: " + contentType);

            reader.MediaParser = mediaParser;

            mediaParser.ConfigurationComplete += (s, e) => ConfigurationComplete(reader);

            reader.MediaParser.Initialize(
                (streamType, freePacket) => new StreamBuffer(streamType, freePacket, reader.BufferingManager, _mediaStreamSource.CheckForSamples),
                _programStreamsHandler);
        }

        void ConfigurationComplete(ReaderPipeline reader)
        {
            reader.IsConfigured = true;

            CheckConfigurationCompleted();
        }

        void CheckConfigurationCompleted()
        {
            var state = State;

            if (MediaState.Opening != state && MediaState.OpenMedia != state)
                return;

            if (null == _readers || _readers.Any(r => !r.IsConfigured))
                return;

            var configuration = new MediaConfiguration
                                {
                                    Duration = _readerManager.Duration
                                };

            foreach (var mediaStream in _readers.SelectMany(r => r.MediaParser.MediaStreams))
            {
                var configurationSource = mediaStream.ConfigurationSource;

                var video = configurationSource as IVideoConfigurationSource;

                if (null != video)
                {
                    if (null != configuration.Video)
                    {
                        Debug.WriteLine("TsMediaManager.CheckConfigurationCompleted() multiple video streams");
                        continue;
                    }

                    configuration.Video = mediaStream;

                    continue;
                }

                var audio = configurationSource as IAudioConfigurationSource;

                if (null != audio)
                {
                    if (null != configuration.Audio)
                    {
                        Debug.WriteLine("TsMediaManager.CheckConfigurationCompleted() multiple audio streams");
                        continue;
                    }

                    configuration.Audio = mediaStream;

                    continue;
                }

                Debug.WriteLine("TsMediaManager.CheckConfigurationCompleted() unexpected media stream");
            }

            _mediaStreamSource.Configure(configuration);

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

            try
            {
                var tasks = _readers.Select(reader =>
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

            foreach (var reader in readers)
                reader.DisposeBackground("TsMediaManager dispose reader");

            //Debug.WriteLine("TsMediaManager.DisposeReaders() completed");
        }

        bool IsSeekInRange(TimeSpan position)
        {
            return _readers.All(reader => reader.BufferingManager.IsSeekAlreadyBuffered(position));
        }

        async Task<TimeSpan> SeekAsync(TimeSpan position)
        {
            if (IsSeekInRange(position))
                return position;

            foreach (var reader in _readers)
            {
                reader.QueueWorker.IsEnabled = false;
                reader.MediaParser.EnableProcessing = false;
            }

            var stopReaderTasks = _readers.Select(
                async r =>
                {
                    await r.CallbackReader.StopAsync().ConfigureAwait(false);
                    await r.QueueWorker.ClearAsync().ConfigureAwait(false);
                });

            await TaskEx.WhenAll(stopReaderTasks).ConfigureAwait(false);

            foreach (var reader in _readers)
            {
                reader.MediaParser.FlushBuffers();
                reader.BufferingManager.Flush();

                reader.MediaParser.EnableProcessing = true;
                reader.QueueWorker.IsEnabled = true;
            }

            State = MediaState.Seeking;

            var actualPosition = await _readerManager.SeekAsync(position, _playbackCancellationTokenSource.Token).ConfigureAwait(false);

            StartReaders();

            return actualPosition;
        }

        Task<TReturn> StartWorkAsync<TReturn>(Func<Task<TReturn>> func, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<TReturn>();

            _asyncFifoWorker.PostAsync(() =>
                                       {
                                           var task = func();

                                           task.ContinueWith(t =>
                                                             {
                                                                 if (t.IsCanceled)
                                                                     tcs.TrySetCanceled();
                                                                 else if (t.IsFaulted)
                                                                     tcs.TrySetException(t.Exception);
                                                                 else
                                                                     tcs.TrySetResult(t.Result);
                                                             }, cancellationToken);

                                           return task;
                                       });

            return tcs.Task;
        }

        #region Nested type: ReaderPipeline

        sealed class ReaderPipeline : IDisposable
        {
            public BlockingPool<WorkBuffer> BlockingPool;
            public IBufferingManager BufferingManager;
            public CallbackReader CallbackReader;
            public IMediaParser MediaParser;
            public QueueWorker<WorkBuffer> QueueWorker;
            public ISegmentManagerReaders SegmentReaders;
            int _isDisposed;

            public bool IsConfigured { get; set; }

            #region IDisposable Members

            public void Dispose()
            {
                if (0 != Interlocked.Exchange(ref _isDisposed, 1))
                    return;

                using (CallbackReader)
                { }

                using (QueueWorker)
                { }

                using (BlockingPool)
                { }

                using (MediaParser)
                { }

                CallbackReader = null;
                QueueWorker = null;
                BlockingPool = null;
                MediaParser = null;
                BufferingManager = null;
                SegmentReaders = null;
            }

            #endregion

            public Task StartAsync(CancellationToken cancellationToken)
            {
                MediaParser.StartPosition = SegmentReaders.Manager.StartPosition;

                BufferingManager.Flush();

                return CallbackReader.StartAsync(cancellationToken);
            }

            public async Task CloseAsync()
            {
                await StopReadingAsync().ConfigureAwait(false);

                var queue = QueueWorker;

                if (null != queue)
                {
                    try
                    {
                        await queue.CloseAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("TsMediaManager.ReaderPipeline.CloseAsync(): queue clear failed: " + ex.Message);
                    }
                }
            }

            public async Task StopAsync()
            {
                await StopReadingAsync().ConfigureAwait(false);

                var queue = QueueWorker;

                if (null != queue)
                {
                    try
                    {
                        await queue.ClearAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("TsMediaManager.ReaderPipeline.StopAsync(): queue clear failed: " + ex.Message);
                    }
                }
            }

            async Task StopReadingAsync()
            {
                if (null != CallbackReader)
                {
                    try
                    {
                        await CallbackReader.StopAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("TsMediaManager.ReaderPipeline.StopAsync(): callback reader stop failed: " + ex.Message);
                    }
                }

                if (null != MediaParser)
                {
                    MediaParser.EnableProcessing = false;
                    MediaParser.FlushBuffers();
                }

                if (null != BufferingManager)
                    BufferingManager.Flush();
            }
        }

        #endregion
    }
}

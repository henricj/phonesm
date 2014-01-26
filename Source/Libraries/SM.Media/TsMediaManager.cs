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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SM.Media.AAC;
using SM.Media.Buffering;
using SM.Media.Configuration;
using SM.Media.MP3;
using SM.Media.Segments;
using SM.Media.Utility;
using SM.Media.Web;
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

    public sealed class TsMediaManager : ITsMediaManager, IMediaManager, IDisposable
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
        readonly MediaManagerParameters.BufferingManagerFactoryDelegate _bufferingManagerFactory;
        readonly IBufferingPolicy _bufferingPolicy;
        readonly CancellationTokenSource _closeCancellationTokenSource = new CancellationTokenSource();
        readonly Queue<ConfigurationEventArgs> _configurationEvents = new Queue<ConfigurationEventArgs>();
        readonly FifoTaskScheduler _fifoTaskScheduler = new FifoTaskScheduler(CancellationToken.None);
        readonly IMediaElementManager _mediaElementManager;
        readonly IMediaStreamSource _mediaStreamSource;
        readonly Action<IProgramStreams> _programStreamsHandler;
        readonly ISegmentReaderManager _segmentReaderManager;
        MediaState _mediaState;
        CancellationTokenSource _playbackCancellationTokenSource;
        ISegmentReaderManager _readerManager;
        ReaderPipeline[] _readers;

        public TsMediaManager(MediaManagerParameters mediaManagerParameters)
        {
            _segmentReaderManager = mediaManagerParameters.SegmentReaderManager;
            _mediaElementManager = mediaManagerParameters.MediaElementManager;
            _mediaStreamSource = mediaManagerParameters.MediaStreamSource;
            _bufferingManagerFactory = mediaManagerParameters.BufferingManagerFactory;
            _bufferingPolicy = mediaManagerParameters.BufferingPolicy;
            _programStreamsHandler = mediaManagerParameters.ProgramStreamsHandler;

            if (null == _segmentReaderManager)
                throw new ArgumentNullException("segmentReaderManager");

            if (null == _mediaElementManager)
                throw new ArgumentNullException("mediaElementManager");

            if (null == _mediaStreamSource)
                throw new ArgumentNullException("mediaStreamSource");

            if (null == _bufferingManagerFactory)
                throw new ArgumentException("bufferingManagerFactory");

            _mediaStreamSource.MediaManager = this;

            ResetCancellationToken();

            // Start with a cancelled token (i.e., we are not playing)
            _playbackCancellationTokenSource.Cancel();
        }

        bool IsClosed
        {
            get { return _closeCancellationTokenSource.IsCancellationRequested; }
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (null != OnStateChange)
                Debug.WriteLine("TsMediaManager.Dispose(): OnStateChange is not null");

            CloseAsync()
                .Wait();

            using (_readerManager)
            { }

            _readerManager = null;

            using (_playbackCancellationTokenSource)
            { }

            using (_closeCancellationTokenSource)
            { }
        }

        #endregion

        #region IMediaManager Members

        public void OpenMedia()
        {
            StartWork(() =>
                      {
                          State = MediaState.OpenMedia;

                          CheckConfigurationCompleted();
                      },
                _closeCancellationTokenSource.Token, "TsMediaManager.OpenMedia");
        }

        public void CloseMedia()
        {
            Close();
        }

        public Task<TimeSpan> SeekMediaAsync(TimeSpan position)
        {
            return StartWorkAsync(() => SeekAsync(position), _playbackCancellationTokenSource.Token);
        }

        #endregion

        #region ITsMediaManager Members

        public MediaState State
        {
            get { return _mediaState; }
            set { SetMediaState(value, null); }
        }

        public void Play()
        {
            StartWork(() => PlayAsync(_segmentReaderManager), _closeCancellationTokenSource.Token, "TsMediaManager.Play");
        }

        public void Close()
        {
            StartWork((Func<Task>)CloseAsync, CancellationToken.None, "TsMediaManager.Close");
        }

        public void Pause()
        { }

        public void Resume()
        { }

        public TimeSpan? SeekTarget
        {
            get { return _mediaStreamSource.SeekTarget; }
            set { _mediaStreamSource.SeekTarget = value; }
        }

        public event EventHandler<TsMediaManagerStateEventArgs> OnStateChange;

        #endregion

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
            if (state == _mediaState)
                return;

            _mediaState = state;

            var handlers = OnStateChange;

            if (null == handlers)
                return;

            handlers(this, new TsMediaManagerStateEventArgs(state, message));

            if (MediaState.Error == state)
            {
                if (null != _mediaStreamSource)
                    _mediaStreamSource.ReportError(message);
            }
        }

        void StartReaders()
        {
            foreach (var reader in _readers)
            {
                var startReader = reader.StartAsync(_playbackCancellationTokenSource.Token);

                startReader.ContinueWith(t => Close(), TaskContinuationOptions.OnlyOnFaulted);
            }
        }

        async Task PlayAsync(ISegmentReaderManager segmentManager)
        {
            if (MediaState.Playing == State || MediaState.Seeking == State)
                await StopAsync().ConfigureAwait(false);

            _readerManager = segmentManager;

            ResetCancellationToken();

            Exception exception;

            Task<ReaderPipeline>[] readerTasks = null;

            try
            {
                readerTasks = _readerManager.SegmentManagerReaders
                                            .Select(CreateReaderPipeline)
                                            .ToArray();

                _readers = await TaskEx.WhenAll<ReaderPipeline>(readerTasks)
                                       .ConfigureAwait(false);

                foreach (var reader in _readers)
                    reader.QueueWorker.IsEnabled = true;

                State = MediaState.Opening;

                await _readerManager.StartAsync(_playbackCancellationTokenSource.Token).ConfigureAwait(false);

                StartReaders();

                await _mediaElementManager.SetSourceAsync(_mediaStreamSource).ConfigureAwait(false);

                return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("TsMediaManager.PlayAsync failed: " + ex.Message);

                SetMediaState(MediaState.Error, "Unable to play media");

                exception = new AggregateException(ex);
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
                        Debug.WriteLine("TsMediaManager.PlayAsync(): reader create failed: " + readerException.Message);
                        continue;
                    }

                    var reader = readerTask.Result;

                    try
                    {
                        await reader.StopAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("TsMediaManager.PlayAsync(): reader cleanup failed: " + ex.Message);
                    }
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

            reader.BufferingManager = _bufferingManagerFactory(segmentManagerReaders, reader.QueueWorker, _mediaStreamSource.CheckForSamples, _bufferingPolicy);

            Action<IProgramStreams> programStreamsHandler = null;

            await startReaderTask.ConfigureAwait(false);

            var firstSegment = await reader.SegmentReaders.Manager.Playlist.FirstOrDefaultAsync().ConfigureAwait(false);

            if (null == firstSegment)
                throw new FileNotFoundException();

            var ext = firstSegment.Url.GetExtension();

            if (null != ext)
            {
                if (string.Equals(ext, ".mp3", StringComparison.OrdinalIgnoreCase))
                {
                    var mediaParser = new Mp3MediaParser(reader.BufferingManager, new BufferPool(64 * 1024, 2), _mediaStreamSource.CheckForSamples);

                    mediaParser.MediaStream.ConfigurationComplete += (sender, args) => SendConfigurationComplete(args, reader);

                    reader.MediaParser = mediaParser;

                    reader.ExpectedStreamCount = 1;
                }
                else if (string.Equals(ext, ".aac", StringComparison.OrdinalIgnoreCase))
                {
                    var mediaParser = new AacMediaParser(reader.BufferingManager, new BufferPool(64 * 1024, 2), _mediaStreamSource.CheckForSamples);

                    mediaParser.MediaStream.ConfigurationComplete += (sender, args) => SendConfigurationComplete(args, reader);

                    reader.MediaParser = mediaParser;

                    reader.ExpectedStreamCount = 1;
                }
            }

            if (null == reader.MediaParser)
            {
                var tsTimestamp = new TsTimestamp();

                reader.MediaParser = new TsMediaParser(
                    (streamType, tsDecoder) => new StreamBuffer(streamType, tsDecoder.PesPacketPool.FreePesPacket, reader.BufferingManager, _mediaStreamSource.CheckForSamples),
                    tsTimestamp,
                    mediaStream =>
                    {
                        mediaStream.ConfigurationComplete +=
                            (sender, args) => SendConfigurationComplete(args, localReader);
                    });

                reader.ExpectedStreamCount = 2;

                if (null == _programStreamsHandler)
                {
                    programStreamsHandler = pss =>
                                            {
                                                var count = DefaultProgramStreamsHandler(pss);

                                                localReader.ExpectedStreamCount = count;
                                            };
                }
                else
                {
                    var localHandler = _programStreamsHandler;

                    programStreamsHandler = pss =>
                                            {
                                                localHandler(pss);

                                                localReader.ExpectedStreamCount = pss.Streams.Count(s => !s.BlockStream);
                                            };
                }
            }

            reader.MediaParser.Initialize(programStreamsHandler);

            return reader;
        }

        static int DefaultProgramStreamsHandler(IProgramStreams pss)
        {
            var hasAudio = false;
            var hasVideo = false;
            var count = 0;

            foreach (var stream in pss.Streams)
            {
                switch (stream.StreamType.Contents)
                {
                    case TsStreamType.StreamContents.Audio:
                        if (hasAudio)
                            stream.BlockStream = true;
                        else
                        {
                            hasAudio = true;
                            ++count;
                        }
                        break;
                    case TsStreamType.StreamContents.Video:
                        if (hasVideo)
                            stream.BlockStream = true;
                        else
                        {
                            hasVideo = true;
                            ++count;
                        }
                        break;
                    default:
                        stream.BlockStream = true;
                        break;
                }
            }

            return count;
        }

        void SendConfigurationComplete(ConfigurationEventArgs args, ReaderPipeline reader)
        {
            StartWork(() => ConfigurationComplete(args, reader), _playbackCancellationTokenSource.Token, "TsMediaManager.SendConfigurationComplete");
        }

        void ConfigurationComplete(ConfigurationEventArgs eventArgs, ReaderPipeline reader)
        {
            ++reader.CompletedStreamCount;

            _configurationEvents.Enqueue(eventArgs);

            CheckConfigurationCompleted();
        }

        void CheckConfigurationCompleted()
        {
            var state = State;

            if (MediaState.Opening != state && MediaState.OpenMedia != state)
                return;

            if (null == _readers || _readers.Any(r => r.ExpectedStreamCount != r.CompletedStreamCount))
                return;

            var configuration = new MediaConfiguration
                                {
                                    Duration = _segmentReaderManager.Duration
                                };

            while (_configurationEvents.Count > 0)
            {
                var args = _configurationEvents.Dequeue();

                var video = args.ConfigurationSource as IVideoConfigurationSource;

                if (null != video)
                {
                    configuration.VideoConfiguration = video;
                    configuration.VideoStream = args.StreamSource;

                    continue;
                }

                var audio = args.ConfigurationSource as IAudioConfigurationSource;

                if (null == audio)
                    continue;

                configuration.AudioConfiguration = audio;
                configuration.AudioStream = args.StreamSource;
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
                Debug.WriteLine("TsMediaManager.CloseAsync() calling readerManager.StopAsync()");

                stopPlaylistTask = readerManager.StopAsync();
            }

            var mss = _mediaStreamSource;

            Task drainTask = null;

            if (null != mss)
            {
                Debug.WriteLine("TsMediaManager.CloseAsync() calling _mediaStreamSource.CloseAsync()");

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

            if (null != _mediaElementManager)
            {
                try
                {
                    Debug.WriteLine("TsMediaManager.CloseAsync() calling _mediaElementManager.CloseAsync()");

                    await _mediaElementManager.CloseAsync().ConfigureAwait(false);

                    Debug.WriteLine("TsMediaManager.CloseAsync() returned from _mediaElementManager.CloseAsync()");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("TsMediaManager.CloseAsync() media element manager CloseAsync failed: " + ex.Message);
                }
            }

            if (null != drainTask)
            {
                try
                {
                    Debug.WriteLine("TsMediaManager.CloseAsync() waiting for _mediaStreamSource.CloseAsync()");

                    await drainTask.ConfigureAwait(false);

                    Debug.WriteLine("TsMediaManager.CloseAsync() finished _mediaStreamSource.CloseAsync()");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("TsMediaManager.CloseAsync() drain failed: " + ex.Message);
                }
            }

            if (null != _readers && _readers.Length > 0)
                DisposeReaders();

            if (null == error)
                State = MediaState.Closed;
            else
                SetMediaState(MediaState.Error, error);

            Debug.WriteLine("TsMediaManager.CloseAsync() completed");
        }

        async Task CloseReadersAsync()
        {
            Debug.WriteLine("TsMediaManager.CloseAsync() closing readers");

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

            Debug.WriteLine("TsMediaManager.CloseAsync() readers closed");
        }

        void DisposeReaders()
        {
            Debug.WriteLine("TsMediaManager.CleanupReaders()");

            var readers = _readers;

            _readers = null;

            var task = TaskEx.Run(() =>
                                  {
                                      foreach (var reader in readers)
                                      {
                                          try
                                          {
                                              using (reader)
                                              { }
                                          }
                                          catch (Exception ex)
                                          {
                                              Debug.WriteLine("TsMediaManager.CleanupReaders(): dispose of reader failed: " + ex.Message);
                                          }
                                      }
                                  });

            TaskCollector.Default.Add(task, "TsMediaManager dispose readers");

            Debug.WriteLine("TsMediaManager.CleanupReaders() completed");
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

        void StartWork(Action action, CancellationToken cancellationToken, string description)
        {
            var task = StartWorkAsync(action, cancellationToken);

            TaskCollector.Default.Add(task, description);
        }

        void StartWork(Func<Task> func, CancellationToken cancellationToken, string description)
        {
            var task = StartWorkAsync(func, cancellationToken);

            TaskCollector.Default.Add(task, description);
        }

        Task StartWorkAsync(Action action, CancellationToken cancellationToken)
        {
            var task = Task.Factory.StartNew(action, cancellationToken, TaskCreationOptions.None, _fifoTaskScheduler);

            return task;
        }

        Task StartWorkAsync(Func<Task> func, CancellationToken cancellationToken)
        {
            var task = Task.Factory.StartNew(func, cancellationToken, TaskCreationOptions.None, _fifoTaskScheduler);

            return task.Unwrap();
        }

        Task<TReturn> StartWorkAsync<TReturn>(Func<Task<TReturn>> func, CancellationToken cancellationToken)
        {
            var task = Task.Factory.StartNew(func, cancellationToken, TaskCreationOptions.None, _fifoTaskScheduler);

            return task.Unwrap();
        }

        Task<TReturn> StartWorkAsync<TReturn>(Func<TReturn> func, CancellationToken cancellationToken)
        {
            var task = Task.Factory.StartNew(func, cancellationToken, TaskCreationOptions.None, _fifoTaskScheduler);

            return task;
        }

        #region Nested type: ReaderPipeline

        sealed class ReaderPipeline : IDisposable
        {
            public BlockingPool<WorkBuffer> BlockingPool;
            public IBufferingManager BufferingManager;
            public CallbackReader CallbackReader;
            public int CompletedStreamCount;
            public int ExpectedStreamCount;
            public IMediaParser MediaParser;
            public QueueWorker<WorkBuffer> QueueWorker;
            public ISegmentManagerReaders SegmentReaders;
            int _isDisposed;

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

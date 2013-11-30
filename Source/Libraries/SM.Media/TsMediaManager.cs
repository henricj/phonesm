// -----------------------------------------------------------------------
//  <copyright file="TsMediaManager.cs" company="Henric Jungheim">
//  Copyright (c) 2012, 2013.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012, 2013 Henric Jungheim <software@henric.org>
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
using SM.Media.Configuration;
using SM.Media.MP3;
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
            Error
        }

        #endregion

        const int MaxBuffers = 8;
        readonly TaskCommandWorker _commandWorker = new TaskCommandWorker();
        readonly Queue<ConfigurationEventArgs> _configurationEvents = new Queue<ConfigurationEventArgs>();
        readonly IMediaElementManager _mediaElementManager;
        readonly IMediaStreamSource _mediaStreamSource;
        readonly Action<IProgramStreams> _programStreamsHandler;
        readonly ISegmentReaderManager _segmentReaderManager;
        CancellationTokenSource _cancellationTokenSource;
        MediaState _mediaState;
        ISegmentReaderManager _readerManager;
        ReaderPipeline[] _readers;

        public TsMediaManager(ISegmentReaderManager segmentReaderManager, IMediaElementManager mediaElementManager, IMediaStreamSource mediaStreamSource, Action<IProgramStreams> programStreamsHandler = null)
        {
            if (null == segmentReaderManager)
                throw new ArgumentNullException("segmentReaderManager");

            if (null == mediaElementManager)
                throw new ArgumentNullException("mediaElementManager");

            if (mediaStreamSource == null)
                throw new ArgumentNullException("mediaStreamSource");

            _segmentReaderManager = segmentReaderManager;
            _mediaElementManager = mediaElementManager;
            _mediaStreamSource = mediaStreamSource;
            _programStreamsHandler = programStreamsHandler;

            _mediaStreamSource.MediaManager = this;

            // Start with a cancelled token (i.e., we are not playing)
            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationTokenSource.Cancel();
        }

        #region IDisposable Members

        public void Dispose()
        {
            CloseAsync()
                .Wait();

            using (_readerManager)
            { }

            _readerManager = null;
        }

        #endregion

        #region IMediaManager Members

        public void OpenMedia()
        {
            _commandWorker.SendCommand(new WorkCommand(
                () =>
                {
                    State = MediaState.OpenMedia;

                    CheckConfigurationCompleted();

                    return TplTaskExtensions.CompletedTask;
                }));
        }

        public void CloseMedia()
        {
            Close();
        }

        public Task<TimeSpan> SeekMediaAsync(TimeSpan position)
        {
            var seekCompletion = new TaskCompletionSource<TimeSpan>();

            var localSeekCompletion = seekCompletion;

            _commandWorker.SendCommand(new WorkCommand(
                () => SeekAsync(position)
                    .ContinueWith(t =>
                                  {
                                      if (t.IsFaulted)
                                          seekCompletion.SetException(t.Exception);
                                      else
                                          seekCompletion.SetResult(t.Result);
                                  }),
                b => { if (!b) localSeekCompletion.SetCanceled(); }));

            return seekCompletion.Task;
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
            _commandWorker.SendCommand(new WorkCommand(() => PlayAsync(_segmentReaderManager)));
        }

        public void Close()
        {
            _commandWorker.SendCommand(new WorkCommand(CloseAsync));
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
                reader.MediaParser.StartPosition = reader.SegmentReaders.Manager.StartPosition;

                reader.BufferingManager.Flush();

                var startReader = reader.CallbackReader.StartAsync();

                startReader.ContinueWith(t => Close(), TaskContinuationOptions.OnlyOnFaulted);
            }
        }

        async Task PlayAsync(ISegmentReaderManager segmentManager)
        {
            await StopAsync().ConfigureAwait(false);

            _readerManager = segmentManager;

            _cancellationTokenSource = new CancellationTokenSource();

            Exception exception;

            Task<ReaderPipeline>[] readerTasks = null;

            try
            {
                readerTasks = _readerManager.SegmentManagerReaders
                                            .Select(CreateReaderPipeline)
                                            .ToArray();

                _readers = await TaskEx.WhenAll(readerTasks)
                                       .ConfigureAwait(false);

                foreach (var reader in _readers)
                    reader.QueueWorker.IsEnabled = true;

                State = MediaState.Opening;

                await _readerManager.StartAsync(_cancellationTokenSource.Token).ConfigureAwait(false);

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

            await StopAsync().ConfigureAwait(false);

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
                        await StopReaderAsync(reader).ConfigureAwait(false);
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

            reader.BufferingManager = new BufferingManager(reader.QueueWorker, _mediaStreamSource.CheckForSamples);

            Action<IProgramStreams> programStreamsHandler = null;

            await startReaderTask.ConfigureAwait(false);

            var firstSegment = await reader.SegmentReaders.Manager.Playlist.FirstOrDefaultAsync().ConfigureAwait(false);

            if (null == firstSegment)
                throw new FileNotFoundException();

            var filename = firstSegment.Url.LocalPath;

            var lastPeriod = filename.LastIndexOf('.');

            if (lastPeriod > 0)
            {
                var ext = filename.Substring(lastPeriod);

                if (string.Equals(ext, ".mp3", StringComparison.CurrentCultureIgnoreCase))
                {
                    var mediaParser = new Mp3MediaParser(reader.BufferingManager, new BufferPool(64 * 1024, 2), _mediaStreamSource.CheckForSamples);

                    mediaParser.MediaStream.ConfigurationComplete += (sender, args) => SendConfigurationComplete(args, reader);

                    reader.MediaParser = mediaParser;

                    reader.ExpectedStreamCount = 1;
                }
                else if (string.Equals(ext, ".aac", StringComparison.CurrentCultureIgnoreCase))
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
            _commandWorker.SendCommand(
                new WorkCommand(() =>
                                {
                                    ConfigurationComplete(args, reader);

                                    return TplTaskExtensions.CompletedTask;
                                }));
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
            _cancellationTokenSource.Cancel();

            if (null == _readers)
                return;

            foreach (var reader in _readers)
                await StopReaderAsync(reader).ConfigureAwait(false);
        }

        static async Task StopReaderAsync(ReaderPipeline reader)
        {
            try
            {
                await reader.CallbackReader.StopAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("TsMediaManage.StopAsync(): callback reader stop failed: " + ex.Message);
            }

            var queue = reader.QueueWorker;

            if (null != queue)
            {
                try
                {
                    await queue.ClearAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("TsMediaManage.StopAsync(): queue clear failed: " + ex.Message);
                }
            }

            reader.MediaParser.FlushBuffers();

            reader.BufferingManager.Flush();
        }

        public async Task CloseAsync()
        {
            var tasks = new List<Task>();

            Task stopPlaylist = null;

            var readerManager = _readerManager;

            if (null != readerManager)
                stopPlaylist = readerManager.StopAsync();

            var mss = _mediaStreamSource;

            Task drainTask = null;
            if (null != mss)
                drainTask = mss.CloseAsync();

            string error = null;

            try
            {
                if (null != _readers)
                {
                    foreach (var reader in _readers)
                    {
                        if (null != reader.QueueWorker)
                            tasks.Add(reader.QueueWorker.CloseAsync());

                        if (null != reader.CallbackReader)
                        {
                            try
                            {
                                var stopAsync = reader.CallbackReader.StopAsync();

                                tasks.Add(stopAsync);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("TsMediaManager.CloseAsync: StopAsync failed: " + ex.Message);
                                error = "Stop failed: " + ex.Message;
                            }
                        }
                    }
                    if (tasks.Count > 0)
                    {
                        try
                        {
                            await TaskEx.WhenAll(tasks).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("TsMediaManager.CloseAsync: task failed: " + ex.Message);
                            error = ex.Message;
                        }
                    }
                }

                if (null != drainTask || null != stopPlaylist)
                {
                    await TaskEx.WhenAny(drainTask ?? TplTaskExtensions.CompletedTask,
                        stopPlaylist ?? TplTaskExtensions.CompletedTask,
                        TaskEx.Delay(1500)).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("TsMediaManager.CloseAsync: " + ex.Message);
                error = "Close failed: " + ex.Message;
            }

            if (null != _mediaElementManager)
                await _mediaElementManager.CloseAsync().ConfigureAwait(false);

            if (null != _readers)
            {
                foreach (var reader in _readers)
                {
                    if (null != reader.MediaParser)
                    {
                        reader.MediaParser.EnableProcessing = false;
                        reader.MediaParser.FlushBuffers();
                    }

                    if (null != reader.BufferingManager)
                        reader.BufferingManager.Flush();

                    using (reader)
                    { }
                }
            }

            if (null == error)
                State = MediaState.Closed;
            else
                SetMediaState(MediaState.Error, error);
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

            var stopPlaylist = _readerManager.StopAsync();

            var stopReaderTasks = _readers.Select(
                async r =>
                {
                    await r.CallbackReader.StopAsync().ConfigureAwait(false);
                    await r.QueueWorker.ClearAsync().ConfigureAwait(false);
                });

            await TaskEx.WhenAll(stopReaderTasks).ConfigureAwait(false);

            await stopPlaylist.ConfigureAwait(false);

            foreach (var reader in _readers)
            {
                reader.MediaParser.FlushBuffers();
                reader.BufferingManager.Flush();

                reader.MediaParser.EnableProcessing = true;
                reader.QueueWorker.IsEnabled = true;
            }

            await _readerManager.StartAsync().ConfigureAwait(false);

            State = MediaState.Seeking;

            var actualPosition = await _readerManager.SeekAsync(position, _cancellationTokenSource.Token).ConfigureAwait(false);

            StartReaders();

            return actualPosition;
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

            #region IDisposable Members

            public void Dispose()
            {
                using (CallbackReader)
                { }

                using (QueueWorker)
                { }

                using (BlockingPool)
                { }
            }

            #endregion
        }

        #endregion
    }
}

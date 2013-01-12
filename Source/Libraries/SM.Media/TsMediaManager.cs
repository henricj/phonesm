// -----------------------------------------------------------------------
//  <copyright file="TsMediaManager.cs" company="Henric Jungheim">
//  Copyright (c) 2012.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012 Henric Jungheim <software@henric.org>
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
using SM.Media.Configuration;
using SM.Media.Segments;
using SM.Media.Utility;

namespace SM.Media
{
    public sealed class TsMediaManager : ITsMediaManager, IMediaManager, IDisposable
    {
        const int MaxBuffers = 8;
        readonly CommandWorker _commandWorker = new CommandWorker();
        readonly Queue<ConfigurationEventArgs> _configurationEvents = new Queue<ConfigurationEventArgs>();
        readonly IMediaElementManager _mediaElementManager;
        readonly Func<IMediaManager, IMediaStreamSource> _mediaStreamSourceFactory;
        readonly ISegmentReaderManager _segmentReaderManager;
        MediaState _mediaState;
        IMediaStreamSource _mediaStreamSource;
        ISegmentReaderManager _readerManager;
        ReaderPipeline[] _readers;

        public TsMediaManager(ISegmentReaderManager segmentReaderManager, IMediaElementManager mediaElementManager, Func<IMediaManager, IMediaStreamSource> mediaStreamSourceFactory)
        {
            if (null == segmentReaderManager)
                throw new ArgumentNullException("segmentReaderManager");

            if (null == mediaElementManager)
                throw new ArgumentNullException("mediaElementManager");

            if (null == mediaStreamSourceFactory)
                throw new ArgumentNullException("mediaStreamSourceFactory");

            _segmentReaderManager = segmentReaderManager;
            _mediaElementManager = mediaElementManager;
            _mediaStreamSourceFactory = mediaStreamSourceFactory;
        }

        #region IDisposable Members

        public void Dispose()
        {
            CloseAsync().Wait();

            using (_readerManager)
            { }

            _readerManager = null;
        }

        #endregion

        #region IMediaManager Members

        public void OpenMedia()
        {
            _commandWorker.SendCommand(new CommandWorker.Command(
                                           () =>
                                           {
                                               _mediaState = MediaState.OpenMedia;

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

            _commandWorker.SendCommand(new CommandWorker.Command(
                                           () => SeekAsync(position)
                                                     .ContinueWith(t => seekCompletion.SetResult(position)),
                                           b => { if (!b) localSeekCompletion.SetCanceled(); }));

            return seekCompletion.Task;
        }

        public void ValidateEvent(MediaStreamFsm.MediaEvent mediaEvent)
        {
            _mediaElementManager.ValidateEvent(mediaEvent);
        }

        #endregion

        #region ITsMediaManager Members

        public void Play(ISegmentReaderManager segmentManager)
        {
            _commandWorker.SendCommand(new CommandWorker.Command(() => PlayAsync(segmentManager)));
        }

        public void Close()
        {
            _commandWorker.SendCommand(new CommandWorker.Command(CloseAsync));
        }

        public void Pause()
        { }

        public void Resume()
        { }

        public void ReportPosition(TimeSpan position)
        {
            foreach (var reader in _readers)
            {
                reader.MediaParser.ReportPosition(position);
            }
        }

        public TimeSpan? SeekTarget
        {
            get { return _mediaStreamSource.SeekTarget; }
            set { _mediaStreamSource.SeekTarget = value; }
        }

        #endregion

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

        public void Seek(TimeSpan timestamp)
        {
            _commandWorker.SendCommand(new CommandWorker.Command(() => SeekAsync(timestamp)));
        }

        async Task PlayAsync(ISegmentReaderManager segmentManager)
        {
            await StopAsync();

            _readerManager = segmentManager;

            _mediaStreamSource = _mediaStreamSourceFactory(this);

            _readers = _readerManager.SegmentManagerReaders
                                     .Select(CreateReaderPipeline)
                                     .ToArray();

            foreach (var reader in _readers)
            {
                reader.QueueWorker.IsEnabled = true;
            }

            _mediaState = MediaState.Opening;

            await _readerManager.StartAsync(CancellationToken.None);

            StartReaders();

            await _mediaElementManager.SetSource(_mediaStreamSource);
        }

        ReaderPipeline CreateReaderPipeline(ISegmentManagerReaders segmentManagerReaders)
        {
            var reader = new ReaderPipeline
                         {
                             SegmentReaders = segmentManagerReaders,
                             BlockingPool = new BlockingPool<WorkBuffer>(MaxBuffers),
                         };

            var localReader = reader;

            reader.QueueWorker = new QueueWorker<WorkBuffer>(
                wi =>
                {
                    var mediaParser = localReader.MediaParser;

                    if (null == wi)
                        mediaParser.ProcessEndOfData();
                    else
                        mediaParser.ProcessData(wi.Buffer, wi.Length);
                }, reader.BlockingPool.Free);

            reader.CallbackReader = new CallbackReader(segmentManagerReaders.Readers, reader.QueueWorker.Enqueue, reader.BlockingPool);

            reader.BufferingManager = new BufferingManager(reader.QueueWorker, value => SendBufferingProgress(value, reader));

            reader.MediaParser = new MediaParser(reader.BufferingManager,
                                                 mediaStream =>
                                                 {
                                                     mediaStream.ConfigurationComplete +=
                                                         (sender, args) => SendConfigurationComplete(args, reader);
                                                 });

            reader.MediaParser.Initialize();

            return reader;
        }

        void SendBufferingProgress(double value, ReaderPipeline reader)
        {
            _commandWorker.SendCommand(new CommandWorker.Command(() =>
                                                                 {
                                                                     reader.BufferingProgress = value;

                                                                     if (MediaState.Playing != _mediaState)
                                                                         return TplTaskExtensions.CompletedTask;

                                                                     var progress = _readers.Min(r => r.BufferingProgress);

                                                                     _mediaStreamSource.ReportProgress(progress);

                                                                     return TplTaskExtensions.CompletedTask;
                                                                 }));
        }

        void SendConfigurationComplete(ConfigurationEventArgs args, ReaderPipeline reader)
        {
            _commandWorker.SendCommand(
                new CommandWorker.Command(() =>
                                          {
                                              ConfigurationComplete(args, reader);

                                              return TplTaskExtensions.CompletedTask;
                                          }));
        }

        void ConfigurationComplete(ConfigurationEventArgs eventArgs, ReaderPipeline reader)
        {
            Debug.Assert(null == reader.ConfigurationEventArgs);

            _configurationEvents.Enqueue(eventArgs);

            CheckConfigurationCompleted();
        }

        void CheckConfigurationCompleted()
        {
            if (_mediaState != MediaState.OpenMedia)
                return;

            if (_configurationEvents.Count < 2)
                return;

            var configuration = new MediaConfiguration { Duration = _segmentReaderManager.Duration };

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
        }

        async Task StopAsync()
        {
            if (null == _readers)
                return;

            foreach (var reader in _readers)
            {
                await reader.CallbackReader.StopAsync();

                var queue = reader.QueueWorker;

                if (null != queue)
                    await queue.ClearAsync();

                reader.MediaParser.FlushBuffers();

                reader.BufferingManager.Flush();
            }
        }

        async Task CloseAsync()
        {
            var tasks = new List<Task>();

            var mss = _mediaStreamSource;

            Task drainTask = null;
            if (null != mss)
                drainTask = mss.CloseAsync();

            try
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
                        }
                    }
                }

                if (tasks.Count > 0)
                {
                    try
                    {
                        await TaskEx.WhenAll(tasks);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("TsMediaManager.CloseAsync: task failed: " + ex.Message);
                    }
                }

                if (null != drainTask)
                    await TaskEx.WhenAny(drainTask, TaskEx.Delay(1500));
            }
            catch (Exception ex)
            {
                Debug.WriteLine("TsMediaManager.CloseAsync: " + ex.Message);
            }

            await _mediaElementManager.Close();

            foreach (var reader in _readers)
            {
                using (reader)
                { }
            }

            _mediaState = MediaState.Closed;
        }

        bool IsSeekInRange(TimeSpan position)
        {
            return _readers.All(reader => reader.BufferingManager.IsSeekAlreadyBuffered(position));
        }

        async Task SeekAsync(TimeSpan position)
        {
            if (IsSeekInRange(position))
                return;

            foreach (var reader in _readers)
            {
                reader.QueueWorker.IsEnabled = false;
                reader.MediaParser.EnableProcessing = false;
            }

            var stopReaderTasks = _readers.Select<ReaderPipeline, Task>(
                async r =>
                {
                    await r.CallbackReader.StopAsync();
                    await r.QueueWorker.ClearAsync();
                });

            await TaskEx.WhenAll(stopReaderTasks);

            foreach (var reader in _readers)
            {
                reader.MediaParser.FlushBuffers();
                reader.BufferingManager.Flush();

                reader.MediaParser.EnableProcessing = true;
                reader.QueueWorker.IsEnabled = true;
            }

            _mediaState = MediaState.Seeking;

            await _readerManager.SeekAsync(position, CancellationToken.None);

            StartReaders();
        }

        #region Nested type: MediaState

        enum MediaState
        {
            Idle,
            Opening,
            OpenMedia,
            Seeking,
            Playing,
            Closed
        }

        #endregion

        #region Nested type: ReaderPipeline

        sealed class ReaderPipeline : IDisposable
        {
            public BlockingPool<WorkBuffer> BlockingPool;
            public IBufferingManager BufferingManager;
            public double BufferingProgress;
            public CallbackReader CallbackReader;
            public ConfigurationEventArgs ConfigurationEventArgs;
            public MediaParser MediaParser;
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

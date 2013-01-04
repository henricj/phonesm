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
using System.Threading.Tasks;
using SM.Media.Segments;
using SM.Media.Utility;

namespace SM.Media
{
    public sealed class TsMediaManager : ITsMediaManager, IMediaManager, IDisposable
    {
        static readonly TimeSpan SeekEndTolerance = TimeSpan.FromSeconds(8);
        static readonly TimeSpan SeekBeginTolerance = TimeSpan.FromMilliseconds(250);
        readonly CommandWorker _commandWorker = new CommandWorker();
        readonly IMediaElementManager _mediaElementManager;
        readonly Func<IMediaManager, IMediaStreamSource> _mediaStreamSourceFactory;
        MediaParser _mediaParser;
        IMediaStreamSource _mediaStreamSource;
        QueueWorker<CallbackReader.WorkBuffer> _queueWorker;
        CallbackReader _reader;
        ISegmentReaderManager _readerManager;

        public TsMediaManager(IMediaElementManager mediaElementManager, Func<IMediaManager, IMediaStreamSource> mediaStreamSourceFactory)
        {
            if (null == mediaElementManager)
                throw new ArgumentNullException("mediaElementManager");

            if (null == mediaStreamSourceFactory)
                throw new ArgumentNullException("mediaStreamSourceFactory");

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
                                               var startReader = _reader.StartAsync(TimeSpan.Zero);

                                               _reader.ReaderTask.ContinueWith(t => Close(), TaskContinuationOptions.NotOnRanToCompletion);

                                               return startReader;
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
            _mediaParser.ReportPosition(position);
        }

        #endregion

        public void Seek(TimeSpan timestamp)
        {
            _commandWorker.SendCommand(new CommandWorker.Command(() => SeekAsync(timestamp)));
        }

        async Task PlayAsync(ISegmentReaderManager segmentManager)
        {
            await StopAsync();

            _readerManager = segmentManager;

            _reader = new CallbackReader(_readerManager, buffer => _queueWorker.Enqueue(buffer));

            _mediaStreamSource = _mediaStreamSourceFactory(this);

            _queueWorker = new QueueWorker<CallbackReader.WorkBuffer>(
                wi =>
                {
                    if (null == wi)
                        _mediaParser.ProcessData(null, 0);
                    else
                        _mediaParser.ProcessData(wi.Buffer, wi.Length);
                }, _reader.FreeBuffer);

            _mediaParser = new MediaParser(_queueWorker, _mediaStreamSource.ReportProgress, mediaStream => { mediaStream.ConfigurationComplete += _mediaStreamSource.MediaStreamOnConfigurationComplete; });

            _mediaParser.Initialize();

            _queueWorker.IsEnabled = true;

            await _mediaElementManager.SetSource(_mediaStreamSource);
        }

        async Task StopAsync()
        {
            var reader = _reader;

            if (null != reader)
                await reader.StopAsync();

            var queue = _queueWorker;

            if (null != queue)
                await queue.FlushAsync();
        }

        async Task CloseAsync()
        {
            var tasks = new List<Task>();

            var mss = _mediaStreamSource;

            Task drainTask = null;
            if (null != mss)
                drainTask = mss.CloseAsync();

            var queueWorker = _queueWorker;
            var reader = _reader;

            try
            {
                if (null != queueWorker)
                    tasks.Add(queueWorker.CloseAsync());

                // Setting the MediaElement's source to null will cause MediaElement to call TsMediaStreamSource.Dispose().
                // Make sure we are done pushing packets before we start disposing things.

                if (null != reader)
                {
                    try
                    {
                        var stopAsync = reader.StopAsync();

                        tasks.Add(stopAsync);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("TsMediaManager.CloseAsync: StopAsync failed: " + ex.Message);
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

            _queueWorker = null;
            _reader = null;

            using (queueWorker)
            { }

            using (reader)
            { }
        }

        async Task<TimeSpan> SeekAsync(TimeSpan position)
        {
            var bufferPosition = _mediaParser.BufferPosition;

            if (position >= bufferPosition - SeekBeginTolerance && position < bufferPosition + SeekEndTolerance)
                return TimeSpan.Zero;

            _queueWorker.IsEnabled = false;

            try
            {
                await _reader.StopAsync();
            }
            catch (OperationCanceledException)
            {
                // This is normal...
            }

            _queueWorker.IsEnabled = true;

            return await _reader.StartAsync(position);
        }
    }
}

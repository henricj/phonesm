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
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using SM.Media.Segments;
using SM.Media.Utility;

namespace SM.Media
{
    public interface IMediaManager
    {
        void OpenMedia();
        void CloseMedia();
        Task<TimeSpan> SeekMediaAsync(TimeSpan position);
        void ValidateEvent(MediaStreamFsm.MediaEvent mediaEvent);
    }

    public class TsMediaManager : ITsMediaManager, IMediaManager
    {
        static readonly TimeSpan SeekEndTolerance = TimeSpan.FromSeconds(8);
        static readonly TimeSpan SeekBeginTolerance = TimeSpan.FromMilliseconds(250);
        readonly CommandWorker _commandWorker = new CommandWorker();
        readonly MediaElement _mediaElement;
        MediaParser _mediaParser;
        //#if MEDIA_STREAM_STATE_VALIDATION
        MediaStreamFsm _mediaStreamFsm = new MediaStreamFsm();
        //#endif
        TsMediaStreamSource _mediaStreamSource;
        QueueWorker<CallbackReader.WorkBuffer> _queueWorker;
        CallbackReader _reader;
        int _sourceIsSet;

        public TsMediaManager(MediaElement mediaElement)
        {
            if (null == mediaElement)
                throw new ArgumentNullException("mediaElement");

            _mediaElement = mediaElement;

            _mediaStreamFsm.Reset();
        }

        #region IMediaManager Members

        public void OpenMedia()
        {
            var startReadTask = _reader.StartAsync(TimeSpan.Zero);
        }

        public void CloseMedia()
        {
            _commandWorker.SendCommand(new CommandWorker.Command(CloseAsync));
        }

        public Task<TimeSpan> SeekMediaAsync(TimeSpan position)
        {
            var seekCompletion = new TaskCompletionSource<TimeSpan>();

            _commandWorker.SendCommand(new CommandWorker.Command(
                                           () => SeekAsync(position)
                                                     .ContinueWith(t => seekCompletion.SetResult(position)),
                                           b => { if (!b) seekCompletion.SetCanceled(); }));

            return seekCompletion.Task;
        }

        public void ValidateEvent(MediaStreamFsm.MediaEvent mediaEvent)
        {
            _mediaStreamFsm.ValidateEvent(mediaEvent);
        }

        #endregion

        #region ITsMediaManager Members

        public void Play(ISegmentManager segmentManager)
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

        async Task PlayAsync(ISegmentManager segmentManager)
        {
            await StopAsync();

            _reader = new CallbackReader(segmentManager, buffer => _queueWorker.Enqueue(buffer));

            _mediaStreamSource = new TsMediaStreamSource(this);

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

            await Dispatch(() =>
                           {
                               ValidateEvent(MediaStreamFsm.MediaEvent.MediaStreamSourceAssigned);
                               var wasSet = Interlocked.Exchange(ref _sourceIsSet, 1);

                               Debug.Assert(0 == wasSet);

                               _mediaElement.Source = null;

                               _mediaElement.SetSource(_mediaStreamSource);
                           });
        }

        Task Dispatch(Action action)
        {
            if (_mediaElement.Dispatcher.CheckAccess())
            {
                action();
                return TplTaskExtensions.CompletedTask;
            }

            return _mediaElement.Dispatcher.InvokeAsync(action);
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

            if (null != queueWorker)
                tasks.Add(queueWorker.CloseAsync());

            // Setting the MediaElement's source to null will cause MediaElement to call TsMediaStreamSource.Dispose().
            // Make sure we are done pushing packet before we start disposing things.

            var reader = _reader;
            if (null != reader)
                tasks.Add(reader.StopAsync());

            if (tasks.Count > 0)
            {
#if WINDOWS_PHONE8
                await Task.WhenAll(tasks);
#else
                await TaskEx.WhenAll(tasks);
#endif
            }

            if (null != drainTask)
            {
#if WINDOWS_PHONE8
                await Task.WhenAny(drainTask, Task.Delay(1500));
#else
                await TaskEx.WhenAny(drainTask, TaskEx.Delay(1500));
#endif
            }

            var wasSet = Interlocked.CompareExchange(ref _sourceIsSet, 2, 1);

            if (0 != wasSet)
                await _mediaElement.Dispatcher.InvokeAsync(UiThreadCleanup);

            _queueWorker = null;
            _reader = null;

            using (queueWorker)
            { }

            using (reader)
            { }
        }

        void UiThreadCleanup()
        {
            var was2 = Interlocked.CompareExchange(ref _sourceIsSet, 3, 2);

            if (2 != was2 && 3 != was2)
                return;

            var state = _mediaElement.CurrentState;

            if (MediaElementState.Closed != state && MediaElementState.Stopped != state)
                _mediaElement.Stop();

            state = _mediaElement.CurrentState;

            //if (MediaElementState.Closed == state || MediaElementState.Stopped == state)
            _mediaElement.Source = null;

            state = _mediaElement.CurrentState;

            if (MediaElementState.Closed == state || MediaElementState.Stopped == state)
            {
                var was3 = Interlocked.Exchange(ref _sourceIsSet, 0);

                Debug.Assert(3 == was3);
            }
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
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
using System.Threading.Tasks;
using System.Windows.Controls;
using SM.Media.Segments;

namespace SM.Media
{
    public interface IMediaManager
    {
        void OpenMedia();
        void CloseMedia();
        Task SeekMediaAsync(TimeSpan timestamp);
    }

    public class TsMediaManager : ITsMediaManager, IMediaManager
    {
        static readonly TimeSpan SeekEndTolerance = TimeSpan.FromSeconds(8);
        static readonly TimeSpan SeekBeginTolerance = TimeSpan.FromMilliseconds(250);
        readonly CommandWorker _commandWorker = new CommandWorker();
        readonly MediaElement _mediaElement;
        MediaParser _mediaParser;
        TsMediaStreamSource _mediaStreamSource;
        QueueWorker<CallbackReader.WorkBuffer> _queueWorker;
        CallbackReader _reader;

        public TsMediaManager(MediaElement mediaElement)
        {
            if (null == mediaElement)
                throw new ArgumentNullException("mediaElement");

            _mediaElement = mediaElement;
        }

        #region IMediaManager Members

        public void OpenMedia()
        {
            var startReadTask = _reader.StartAsync(TimeSpan.Zero);
        }

        public void CloseMedia()
        {
            _commandWorker.SendCommand(new CommandWorker.Command(StopAsync));
        }

        public Task SeekMediaAsync(TimeSpan timestamp)
        {
            var seekCompletion = new TaskCompletionSource<TimeSpan>();

            _commandWorker.SendCommand(new CommandWorker.Command(() =>
                                                                 {
                                                                     var seekTask = SeekAsync(timestamp);

                                                                     seekTask.ContinueWith(t => seekCompletion.TrySetResult(timestamp));

                                                                     return seekTask;
                                                                 }, b => seekCompletion.TrySetCanceled()));

            return seekCompletion.Task;
        }

        #endregion

        #region ITsMediaManager Members

        public void Play(ISegmentManager segmentManager)
        {
            _commandWorker.SendCommand(new CommandWorker.Command(() => PlayAsync(segmentManager)));
        }

        public void Stop()
        {
            _commandWorker.SendCommand(new CommandWorker.Command(() => _mediaElement.Dispatcher.InvokeAsync(() => _mediaElement.Source = null)));
            //_commandWorker.SendCommand(new CommandWorker.Command(StopAsync));
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

            await Dispatch(() => _mediaElement.SetSource(_mediaStreamSource));
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
            var queueWorker = _queueWorker;

            if (null != queueWorker)
                queueWorker.IsEnabled = false;

            // Setting the MediaElement's source to null will cause MediaElement to call TsMediaStreamSource.Dispose().
            // Make sure we are done pushing packet before we start disposing things.

            var reader = _reader;
            if (null != reader)
                await reader.StopAsync();

            if (null != queueWorker)
                await queueWorker.ClearAsync();

            _queueWorker = null;
            _reader = null;

            using (queueWorker)
            { }

            using (reader)
            { }
        }

        async Task SeekAsync(TimeSpan position)
        {
            var bufferPosition = _mediaParser.BufferPosition;

            if (position >= bufferPosition - SeekBeginTolerance && position < bufferPosition + SeekEndTolerance)
                return;

            _queueWorker.IsEnabled = false;

            await _reader.StopAsync();

            _queueWorker.IsEnabled = true;

            await _reader.StartAsync(position);
        }
    }
}
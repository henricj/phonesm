// -----------------------------------------------------------------------
//  <copyright file="SingleStreamMediaManager.cs" company="Henric Jungheim">
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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SM.Media.Buffering;
using SM.Media.Content;
using SM.Media.MediaParser;
using SM.Media.Utility;
using SM.Media.Web;

namespace SM.Media.MediaManager
{
    public class SingleStreamMediaManager : IMediaManager
    {
        readonly Func<IBufferingManager> _bufferingManagerFactory;
        readonly object _lock = new object();
        readonly IMediaParserFactory _mediaParserFactory;
        readonly IMediaStreamConfigurator _mediaStreamConfigurator;
        readonly SignalTask _reportStateTask;
        readonly IWebReaderManager _webReaderManager;
        MediaManagerState _mediaState;
        string _mediaStateMessage;
        CancellationTokenSource _playCancellationTokenSource;
        Task _playTask;

        public SingleStreamMediaManager(Func<IBufferingManager> bufferingManagerFactory, IMediaParserFactory mediaParserFactory, IMediaStreamConfigurator mediaStreamConfigurator, IWebReaderManager webReaderManager)
        {
            if (null == bufferingManagerFactory)
                throw new ArgumentNullException("bufferingManagerFactory");
            if (null == mediaParserFactory)
                throw new ArgumentNullException("mediaParserFactory");
            if (null == mediaStreamConfigurator)
                throw new ArgumentNullException("mediaStreamConfigurator");
            if (null == webReaderManager)
                throw new ArgumentNullException("webReaderManager");

            _bufferingManagerFactory = bufferingManagerFactory;
            _mediaParserFactory = mediaParserFactory;
            _mediaStreamConfigurator = mediaStreamConfigurator;
            _webReaderManager = webReaderManager;

            _reportStateTask = new SignalTask(ReportState);
        }

        #region IMediaManager Members

        public void Dispose()
        {
            Debug.WriteLine("SingleStreamMediaManager.Dispose()");

            if (null != OnStateChange)
            {
                Debug.WriteLine("SingleStreamMediaManager.Dispose(): OnStateChange is not null");

                if (Debugger.IsAttached)
                    Debugger.Break();

                OnStateChange = null;
            }

            _mediaStreamConfigurator.MediaManager = null;

            _reportStateTask.Dispose();

            CancellationTokenSource pcts;

            lock (_lock)
            {
                pcts = _playCancellationTokenSource;
                _playCancellationTokenSource = null;
            }

            if (null != pcts)
                pcts.Dispose();
        }

        public TimeSpan? SeekTarget { get; set; }
        public ContentType ContentType { get; set; }

        public MediaManagerState State
        {
            get { lock (_lock) return _mediaState; }
            private set { SetMediaState(value, null); }
        }

        public async Task<IMediaStreamConfigurator> OpenMediaAsync(ICollection<Uri> source, CancellationToken cancellationToken)
        {
            State = MediaManagerState.OpenMedia;

            var response = new WebResponse();

            using (var rootWebReader = _webReaderManager.CreateRootReader())
            {
                foreach (var url in source)
                {
                    IWebReader webReader = null;
                    IWebStreamResponse webStream = null;
                    CancellationTokenSource playCancellationTokenSource = null;
                    Task playTask = null;

                    try
                    {
                        webReader = rootWebReader.CreateChild(url, ContentKind.Unknown, ContentType);

                        webStream = await webReader.GetWebStreamAsync(null, false, cancellationToken, response: response).ConfigureAwait(false);

                        if (!webStream.IsSuccessStatusCode)
                            continue;

                        var contentType = response.ContentType;

                        if (null == contentType)
                            continue;

                        if (null == contentType || ContentKind.Playlist == contentType.Kind)
                            throw new FileNotFoundException("Content not supported with this media manager");

                        var configurationTaskCompletionSource = new TaskCompletionSource<bool>();

                        playCancellationTokenSource = new CancellationTokenSource();

                        var localPlayCancellationTokenSource = playCancellationTokenSource;

                        var cancelPlayTask = configurationTaskCompletionSource.Task
                            .ContinueWith(t =>
                            {
                                if (t.IsFaulted || t.IsCanceled)
                                    localPlayCancellationTokenSource.Cancel();
                            });

                        TaskCollector.Default.Add(cancelPlayTask, "SingleStreamMediaManager play cancellation");

                        playTask = SimplePlayAsync(contentType, webReader, webStream, configurationTaskCompletionSource, playCancellationTokenSource.Token);

                        var isConfigured = await configurationTaskCompletionSource.Task.ConfigureAwait(false);

                        if (isConfigured)
                        {
                            lock (_lock)
                            {
                                _playCancellationTokenSource = playCancellationTokenSource;
                                playCancellationTokenSource = null;

                                _playTask = playTask;
                                playTask = null;
                            }

                            webReader = null;
                            webStream = null;

                            return _mediaStreamConfigurator;
                        }
                    }
                    finally
                    {
                        if (null != webStream)
                            webStream.Dispose();

                        if (null != webReader)
                            webReader.Dispose();

                        if (null != playCancellationTokenSource)
                            playCancellationTokenSource.Cancel();

                        if (null != playTask)
                            TaskCollector.Default.Add(playTask, "SingleStreamMediaManager play task");
                    }
                }
            }

            throw new FileNotFoundException();
        }

        public Task StopMediaAsync(CancellationToken cancellationToken)
        {
            Task playTask = null;
            CancellationTokenSource playCancellationTokenSource = null;

            lock (_lock)
            {
                playTask = _playTask;
                playCancellationTokenSource = _playCancellationTokenSource;
            }

            if (null != playCancellationTokenSource)
                playCancellationTokenSource.Cancel();

            return playTask ?? TplTaskExtensions.CompletedTask;
        }

        public Task CloseMediaAsync()
        {
            return StopMediaAsync(CancellationToken.None);
        }

        public Task<TimeSpan> SeekMediaAsync(TimeSpan position)
        {
            throw new NotSupportedException();
        }

        public event EventHandler<MediaManagerStateEventArgs> OnStateChange;

        #endregion

        Task ReportState()
        {
            Debug.WriteLine("SingleStreamMediaManager.ReportState() state {0} message {1}", _mediaState, _mediaStateMessage);

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

        void SetMediaState(MediaManagerState state, string message)
        {
            lock (_lock)
            {
                if (state == _mediaState)
                    return;

                Debug.WriteLine("SingleStreamMediaState.SetMediaState() {0} -> {1}", _mediaState, state);

                _mediaState = state;

                if (null != message)
                    _mediaStateMessage = message;
            }

            _reportStateTask.Fire();
        }

        void CancelPlayback()
        {
            CancellationTokenSource playCancellationTokenSource;

            lock (_lock)
            {
                playCancellationTokenSource = _playCancellationTokenSource;
            }

            if (null == playCancellationTokenSource)
                return;

            if (!playCancellationTokenSource.IsCancellationRequested)
                playCancellationTokenSource.Cancel();
        }

        async Task SimplePlayAsync(ContentType contentType, IWebReader webReader, IWebStreamResponse webStreamResponse, TaskCompletionSource<bool> configurationTaskCompletionSource, CancellationToken cancellationToken)
        {
            try
            {
                _mediaStreamConfigurator.MediaManager = this;

                var mediaParser = await _mediaParserFactory.CreateAsync(new MediaParserParameters(), contentType, cancellationToken).ConfigureAwait(false);

                if (null == mediaParser)
                    throw new NotSupportedException("Unsupported content type: " + contentType);

                State = MediaManagerState.Opening;

                EventHandler configurationComplete = null;

                configurationComplete = (sender, args) =>
                {
                    // ReSharper disable once AccessToModifiedClosure
                    mediaParser.ConfigurationComplete -= configurationComplete;

                    configurationTaskCompletionSource.TrySetResult(true);
                };

                mediaParser.ConfigurationComplete += configurationComplete;

                using (var bufferingManager = _bufferingManagerFactory())
                {
                    var throttle = new QueueThrottle();

                    bufferingManager.Initialize(throttle, _mediaStreamConfigurator.CheckForSamples);

                    mediaParser.Initialize(bufferingManager);

                    Task reader = null;

                    try
                    {
                        using (webReader)
                        {
                            try
                            {
                                if (null == webStreamResponse)
                                    webStreamResponse = await webReader.GetWebStreamAsync(null, false, cancellationToken).ConfigureAwait(false);

                                reader = ReadResponseAsync(mediaParser, webStreamResponse, throttle, cancellationToken);

                                await TaskEx.WhenAny(configurationTaskCompletionSource.Task, cancellationToken.AsTask()).ConfigureAwait(false);

                                cancellationToken.ThrowIfCancellationRequested();

                                await _mediaStreamConfigurator.PlayAsync(mediaParser.MediaStreams, null, cancellationToken).ConfigureAwait(false);

                                State = MediaManagerState.Playing;

                                await reader.ConfigureAwait(false);

                                reader = null;
                            }
                            finally
                            {
                                if (null != webStreamResponse)
                                    webStreamResponse.Dispose();
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    { }
                    catch (Exception ex)
                    {
                        var message = ex.ExtendedMessage();

                        Debug.WriteLine("SingleStreamMediaManager.SimplePlayAsync() failed: " + message);

                        SetMediaState(MediaManagerState.Error, message);
                    }

                    State = MediaManagerState.Closing;

                    if (null != reader)
                    {
                        try
                        {
                            await reader.ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        { }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("SingleStreamMediaManager.SimplePlayAsync() reader failed: " + ex.ExtendedMessage());
                        }
                    }

                    bufferingManager.Shutdown(throttle);

                    await _mediaStreamConfigurator.CloseAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SingleStreamMediaManager.SimplePlayAsync() cleanup failed: " + ex.ExtendedMessage());
            }

            _mediaStreamConfigurator.MediaManager = null;

            if (!configurationTaskCompletionSource.Task.IsCompleted)
                configurationTaskCompletionSource.TrySetCanceled();

            State = MediaManagerState.Closed;

            await _reportStateTask.WaitAsync().ConfigureAwait(false);
        }

        async Task ReadResponseAsync(IMediaParser mediaParser, IWebStreamResponse webStreamResponse, QueueThrottle throttle, CancellationToken cancellationToken)
        {
            var buffer = new byte[16 * 1024];

            var cancellationTask = cancellationToken.AsTask();

            try
            {
                using (var stream = await webStreamResponse.GetStreamAsync(cancellationToken).ConfigureAwait(false))
                {
                    for (; ; )
                    {
                        var waitTask = throttle.WaitAsync();

                        if (!waitTask.IsCompleted)
                        {
                            await TaskEx.WhenAny(waitTask, cancellationTask).ConfigureAwait(false);
                        }

                        var length = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);

                        if (length <= 0)
                            return;

                        mediaParser.ProcessData(buffer, 0, length);
                    }
                }
            }
            finally
            {
                mediaParser.ProcessEndOfData();
            }
        }

        #region Nested type: QueueThrottle

        sealed class QueueThrottle : IQueueThrottling
        {
            readonly AsyncManualResetEvent _event = new AsyncManualResetEvent();

            #region IQueueThrottling Members

            public void Pause()
            {
                _event.Reset();
            }

            public void Resume()
            {
                _event.Set();
            }

            #endregion

            public Task WaitAsync()
            {
                return _event.WaitAsync();
            }
        }

        #endregion
    }
}

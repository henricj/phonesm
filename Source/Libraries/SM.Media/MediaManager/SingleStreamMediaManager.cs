// -----------------------------------------------------------------------
//  <copyright file="SingleStreamMediaManager.cs" company="Henric Jungheim">
//  Copyright (c) 2012-2017.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012-2017 Henric Jungheim <software@henric.org>
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
using SM.Media.Metadata;
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
        readonly IWebMetadataFactory _webMetadataFactory;
        readonly IWebReaderManager _webReaderManager;
        int _isDisposed;
        MediaManagerState _mediaState;
        string _mediaStateMessage;
        CancellationTokenSource _playCancellationTokenSource;
        Task _playTask;

        public SingleStreamMediaManager(Func<IBufferingManager> bufferingManagerFactory, IMediaParserFactory mediaParserFactory,
            IMediaStreamConfigurator mediaStreamConfigurator, IWebMetadataFactory webMetadataFactory, IWebReaderManager webReaderManager)
        {
            if (null == bufferingManagerFactory)
                throw new ArgumentNullException(nameof(bufferingManagerFactory));
            if (null == mediaParserFactory)
                throw new ArgumentNullException(nameof(mediaParserFactory));
            if (null == mediaStreamConfigurator)
                throw new ArgumentNullException(nameof(mediaStreamConfigurator));
            if (null == webMetadataFactory)
                throw new ArgumentNullException(nameof(webMetadataFactory));
            if (null == webReaderManager)
                throw new ArgumentNullException(nameof(webReaderManager));

            _bufferingManagerFactory = bufferingManagerFactory;
            _mediaParserFactory = mediaParserFactory;
            _mediaStreamConfigurator = mediaStreamConfigurator;
            _webMetadataFactory = webMetadataFactory;
            _webReaderManager = webReaderManager;

            _reportStateTask = new SignalTask(ReportState);
        }

        protected virtual void Dispose(bool disposing)
        {
            Debug.WriteLine("SingleStreamMediaManager.Dispose(bool)");

            if (!disposing)
                return;

            if (null != OnStateChange)
            {
                Debug.WriteLine("SingleStreamMediaManager.Dispose(bool): OnStateChange is not null");

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

            pcts?.Dispose();
        }

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

            OnStateChange?.Invoke(this, new MediaManagerStateEventArgs(state, message));

            if (null != message)
            {
                var mss = _mediaStreamConfigurator;

                mss?.ReportError(message);
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

        async Task SimplePlayAsync(ContentType contentType, IWebReader webReader, IWebStreamResponse webStreamResponse, WebResponse webResponse, TaskCompletionSource<bool> configurationTaskCompletionSource, CancellationToken cancellationToken)
        {
            try
            {
                _mediaStreamConfigurator.Initialize();

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

                    var streamMetadata = _webMetadataFactory.CreateStreamMetadata(webResponse);

                    mediaParser.InitializeStream(streamMetadata);

                    Task reader = null;

                    try
                    {
                        using (webReader)
                        {
                            try
                            {
                                if (null == webStreamResponse)
                                    webStreamResponse = await webReader.GetWebStreamAsync(null, false, cancellationToken, response: webResponse).ConfigureAwait(false);

                                reader = ReadResponseAsync(mediaParser, webStreamResponse, webResponse, throttle, cancellationToken);

                                await Task.WhenAny(configurationTaskCompletionSource.Task, cancellationToken.AsTask()).ConfigureAwait(false);

                                cancellationToken.ThrowIfCancellationRequested();

                                await _mediaStreamConfigurator.PlayAsync(mediaParser.MediaStreams, null, cancellationToken).ConfigureAwait(false);

                                State = MediaManagerState.Playing;

                                await reader.ConfigureAwait(false);

                                reader = null;
                            }
                            finally
                            {
                                webStreamResponse?.Dispose();
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

                    mediaParser.ConfigurationComplete -= configurationComplete;

                    mediaParser.EnableProcessing = false;
                    mediaParser.FlushBuffers();

                    bufferingManager.Flush();

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

        async Task ReadResponseAsync(IMediaParser mediaParser, IWebStreamResponse webStreamResponse, WebResponse webResponse, QueueThrottle throttle, CancellationToken cancellationToken)
        {
            //Debug.WriteLine("SingleStreamMediaManager.ReadResponseAsync()");

            var buffer = new byte[16 * 1024];

            var cancellationTask = cancellationToken.AsTask();

            try
            {
                var segmentMetadata = _webMetadataFactory.CreateSegmentMetadata(webResponse);

                mediaParser.StartSegment(segmentMetadata);

                using (var stream = await webStreamResponse.GetStreamAsync(cancellationToken).ConfigureAwait(false))
                {
                    for (;;)
                    {
                        var waitTask = throttle.WaitAsync();

                        if (!waitTask.IsCompleted)
                        {
                            await Task.WhenAny(waitTask, cancellationTask).ConfigureAwait(false);

                            cancellationToken.ThrowIfCancellationRequested();
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

                //Debug.WriteLine("SingleStreamMediaManager.ReadResponseAsync() done");
            }
        }

        #region Nested type: QueueThrottle

        sealed class QueueThrottle : IQueueThrottling
        {
            readonly AsyncManualResetEvent _event = new AsyncManualResetEvent();

            public Task WaitAsync()
            {
                return _event.WaitAsync();
            }

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
        }

        #endregion

        #region IMediaManager Members

        public void Dispose()
        {
            if (0 != Interlocked.Exchange(ref _isDisposed, 1))
                return;

            Dispose(true);

            GC.SuppressFinalize(this);
        }

        public TimeSpan? SeekTarget { get; set; }
        public ContentType ContentType { get; set; }
        public ContentType StreamContentType { get; set; }

        public MediaManagerState State
        {
            get
            {
                lock (_lock)
                {
                    return _mediaState;
                }
            }
            private set { SetMediaState(value, null); }
        }

        public Task PlayingTask
        {
            get
            {
                Task playingTask;

                lock (_lock)
                {
                    playingTask = _playTask;
                }

                return playingTask ?? TplTaskExtensions.CompletedTask;
            }
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
                        webReader = rootWebReader.CreateChild(url, ContentKind.Unknown, ContentType ?? StreamContentType);

                        webStream = await webReader.GetWebStreamAsync(null, false, cancellationToken, response: response).ConfigureAwait(false);

                        if (!webStream.IsSuccessStatusCode)
                            continue;

                        var contentType = response.ContentType;

                        if (null == contentType)
                            continue;

                        if (ContentKind.Playlist == contentType.Kind)
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

                        var localWebReader = webReader;
                        var localWebStream = webStream;
                        var playCancellationToken = playCancellationTokenSource.Token;

                        playTask = Task.Run(() => SimplePlayAsync(contentType, localWebReader, localWebStream, response, configurationTaskCompletionSource, playCancellationToken), playCancellationToken);

                        lock (_lock)
                        {
                            _playCancellationTokenSource = playCancellationTokenSource;
                            playCancellationTokenSource = null;

                            _playTask = playTask;
                            playTask = null;
                        }

                        var isConfigured = await configurationTaskCompletionSource.Task.ConfigureAwait(false);

                        if (isConfigured)
                        {
                            webReader = null;
                            webStream = null;

                            return _mediaStreamConfigurator;
                        }
                    }
                    finally
                    {
                        webStream?.Dispose();

                        webReader?.Dispose();

                        playCancellationTokenSource?.Cancel();

                        if (null != playTask)
                            TaskCollector.Default.Add(playTask, "SingleStreamMediaManager play task");
                    }
                }
            }

            throw new FileNotFoundException();
        }

        public Task StopMediaAsync(CancellationToken cancellationToken)
        {
            Task playTask;
            CancellationTokenSource playCancellationTokenSource;

            lock (_lock)
            {
                playTask = _playTask;
                playCancellationTokenSource = _playCancellationTokenSource;
            }

            playCancellationTokenSource?.Cancel();

            return playTask ?? TplTaskExtensions.CompletedTask;
        }

        public Task CloseMediaAsync()
        {
            return StopMediaAsync(CancellationToken.None);
        }

        public Task<TimeSpan> SeekMediaAsync(TimeSpan position)
        {
            return Task.FromResult(position);
        }

        public event EventHandler<MediaManagerStateEventArgs> OnStateChange;

        #endregion
    }
}

// -----------------------------------------------------------------------
//  <copyright file="MediaReader.cs" company="Henric Jungheim">
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
    interface IMediaReader : IDisposable
    {
        bool IsConfigured { get; }
        bool IsEnabled { get; set; }
        ICollection<IMediaParserMediaStream> MediaStreams { get; }
        Task StartAsync(CancellationToken cancellationToken);
        Task CloseAsync();
        Task StopAsync();

        bool IsBuffered(TimeSpan position);
    }

    sealed class MediaReader : IMediaReader
    {
        readonly IMediaParserFactory _mediaParserFactory;
        BlockingPool<WorkBuffer> _blockingPool;
        IBufferingManager _bufferingManager;
        CallbackReader _callbackReader;
        Action _checkConfiguration;
        int _isDisposed;
        bool _isEnabled;
        IMediaParser _mediaParser;
        QueueWorker<WorkBuffer> _queueWorker;
        ISegmentManagerReaders _segmentReaders;

        public MediaReader(IBufferingManager bufferingManager, IMediaParserFactory mediaParserFactory, ISegmentManagerReaders segmentReaders, BlockingPool<WorkBuffer> blockingPool)
        {
            if (null == bufferingManager)
                throw new ArgumentNullException("bufferingManager");
            if (null == mediaParserFactory)
                throw new ArgumentNullException("mediaParserFactory");

            _bufferingManager = bufferingManager;
            _mediaParserFactory = mediaParserFactory;
            _blockingPool = blockingPool;
            _segmentReaders = segmentReaders;
        }

        #region IMediaReader Members

        public bool IsConfigured { get; private set; }

        public bool IsEnabled
        {
            get { return _isEnabled; }
            set
            {
                _isEnabled = value;
                _mediaParser.EnableProcessing = value;
                _queueWorker.IsEnabled = value;
            }
        }

        public void Dispose()
        {
            if (0 != Interlocked.Exchange(ref _isDisposed, 1))
                return;

            using (_callbackReader)
            { }

            using (_queueWorker)
            { }

            using (_blockingPool)
            { }

            using (_mediaParser)
            { }

            _callbackReader = null;
            _queueWorker = null;
            _blockingPool = null;
            _mediaParser = null;
            _bufferingManager = null;
            _segmentReaders = null;
        }

        public ICollection<IMediaParserMediaStream> MediaStreams
        {
            get
            {
                if (!IsConfigured)
                    throw new InvalidOperationException("MediaStreams are not available until after IsConfigured is true.");

                return _mediaParser.MediaStreams;
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            //Debug.WriteLine("MediaReader.StartAsync()");

            _mediaParser.StartPosition = _segmentReaders.Manager.StartPosition;

            _bufferingManager.Flush();

            return _callbackReader.StartAsync(cancellationToken);
        }

        public async Task CloseAsync()
        {
            //Debug.WriteLine("MediaReader.CloseAsync()");

            _queueWorker.IsEnabled = false;

            await StopReadingAsync().ConfigureAwait(false);

            var queue = _queueWorker;

            if (null != queue)
            {
                try
                {
                    await queue.CloseAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("MediaReader.CloseAsync(): queue close failed: " + ex.Message);
                }
            }

            FlushBuffers();
        }

        public async Task StopAsync()
        {
            //Debug.WriteLine("MediaReader.StopAsync()");

            _queueWorker.IsEnabled = false;

            await StopReadingAsync().ConfigureAwait(false);

            var queue = _queueWorker;

            if (null != queue)
            {
                try
                {
                    await queue.ClearAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("MediaReader.CloseAsync(): queue clear failed: " + ex.Message);
                }
            }

            FlushBuffers();
        }

        public bool IsBuffered(TimeSpan position)
        {
            return _bufferingManager.IsSeekAlreadyBuffered(position);
        }

        #endregion

        public async Task InitializeAsync(ISegmentManagerReaders segmentManagerReaders, Action checkConfiguration,
            Action checkForSamples, CancellationToken cancellationToken, Action<IProgramStreams> programStreamsHandler)
        {
            _checkConfiguration = checkConfiguration;

            var startReaderTask = _segmentReaders.Manager.StartAsync();

            var localReader = this;

            _queueWorker = new QueueWorker<WorkBuffer>(
                wi =>
                {
                    //Debug.WriteLine("MediaReader dequeued " + wi);

                    var mediaParser = localReader._mediaParser;

                    if (null == wi)
                        mediaParser.ProcessEndOfData();
                    else
                        mediaParser.ProcessData(wi.Buffer, 0, wi.Length);
                }, _blockingPool.Free);

            _callbackReader = new CallbackReader(segmentManagerReaders.Readers, _queueWorker.Enqueue, _blockingPool);

            _bufferingManager.Initialize(_queueWorker, checkForSamples);

            await startReaderTask.ConfigureAwait(false);

            var contentType = _segmentReaders.Manager.ContentType;

            if (null == contentType)
            {
                Debug.WriteLine("TsMediaManager.CreateReaderPipeline() unable to determine content type, defaulting to transport stream");

                contentType = ContentTypes.TransportStream;
            }
            else if (ContentTypes.Binary == contentType)
            {
                Debug.WriteLine("TsMediaManager.CreateReaderPipeline() detected binary content, defaulting to transport stream");

                contentType = ContentTypes.TransportStream;
            }

            var mediaParserParameters = new MediaParserParameters();

            _mediaParser = await _mediaParserFactory.CreateAsync(mediaParserParameters, contentType, cancellationToken).ConfigureAwait(false);

            if (null == _mediaParser)
                throw new NotSupportedException("Unsupported content type: " + contentType);

            _mediaParser.ConfigurationComplete += ConfigurationComplete;

            _mediaParser.Initialize(_bufferingManager, programStreamsHandler);
        }

        void ConfigurationComplete(object sender, EventArgs eventArgs)
        {
            //Debug.WriteLine("MediaReader.ConfigurationComplete()");

            _mediaParser.ConfigurationComplete -= ConfigurationComplete;

            IsConfigured = true;

            _checkConfiguration();
        }

        async Task StopReadingAsync()
        {
            if (null != _callbackReader)
            {
                try
                {
                    await _callbackReader.StopAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("TsMediaManager.ReaderPipeline.StopAsync(): callback reader stop failed: " + ex.Message);
                }
            }
        }

        void FlushBuffers()
        {
            if (null != _mediaParser)
            {
                _mediaParser.EnableProcessing = false;
                _mediaParser.FlushBuffers();
            }

            if (null != _bufferingManager)
                _bufferingManager.Flush();
        }
    }
}

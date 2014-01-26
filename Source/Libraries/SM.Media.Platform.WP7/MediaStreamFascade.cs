// -----------------------------------------------------------------------
//  <copyright file="MediaStreamFascade.cs" company="Henric Jungheim">
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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using SM.Media.Segments;
using SM.Media.Utility;
using SM.Media.Web;

namespace SM.Media
{
    public sealed class MediaStreamFascade : IDisposable
    {
        readonly IHttpClients _httpClients;
        readonly object _lock = new object();
        readonly ISegmentManagerFactory _segmentManagerFactory;
        readonly Func<MediaStreamSource, Task> _setSourceAsync;
        readonly SignalTask _workerTask;
        string _defaultSourceType = ".m3u8";
        ISegmentManager _playlist;
        Uri _requestedSource;
        Uri _source;
        bool _stopRequested;
        TsMediaManager _tsMediaManager;
        TsMediaStreamSource _tsMediaStreamSource;

        public MediaStreamFascade(IHttpClients httpClients, ISegmentManagerFactory segmentManagerFactory, Func<MediaStreamSource, Task> setSourceAsync)
        {
            if (null == httpClients)
                throw new ArgumentNullException("httpClients");
            if (null == segmentManagerFactory)
                throw new ArgumentNullException("segmentManagerFactory");
            if (null == setSourceAsync)
                throw new ArgumentNullException("setSourceAsync");

            _httpClients = httpClients;
            _segmentManagerFactory = segmentManagerFactory;
            _setSourceAsync = setSourceAsync;

            _workerTask = new SignalTask(Worker, CancellationToken.None);
        }

        /// <summary>
        ///     Select the source type to be used if a type cannot be guessed from the source URL.  The default is ".m3u8".
        /// </summary>
        public string DefaultSourceType
        {
            get { return _defaultSourceType; }
            set { _defaultSourceType = value; }
        }

        public Uri Source
        {
            get { return _source; }
            set
            {
                if (value == null)
                {
                    lock (_lock)
                    {
                        _stopRequested = true;
                        _requestedSource = null;
                    }
                }
                else
                {
                    lock (_lock)
                    {
                        _stopRequested = false;
                        _requestedSource = value;
                    }
                }

                _workerTask.Fire();
            }
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (null != _tsMediaManager)
            {
                _tsMediaManager.OnStateChange -= TsMediaManagerOnStateChange;

                _tsMediaManager.DisposeSafe();
                _tsMediaManager = null;
            }

            if (null != _tsMediaStreamSource)
            {
                _tsMediaStreamSource.DisposeSafe();
                _tsMediaStreamSource = null;
            }
        }

        #endregion

        async Task Worker()
        {
            try
            {
                bool closeRequested;
                Uri requestedSource;

                lock (_lock)
                {
                    closeRequested = _stopRequested;

                    if (closeRequested)
                        _stopRequested = false;

                    requestedSource = _requestedSource;

                    if (null != requestedSource)
                        _requestedSource = null;
                }

                if (closeRequested)
                {
                    //Debug.WriteLine("MediaPlayerSource.Worker() calling CloseMediaAsync()");

                    await CloseMediaAsync().ConfigureAwait(false);

                    //Debug.WriteLine("MediaPlayerSource.Worker() returned from CloseMediaAsync()");

                    return;
                }

                if (null != requestedSource)
                {
                    //Debug.WriteLine("MediaPlayerSource.Worker() calling SetMediaSourceAsync()");
                    //var stopwatch = Stopwatch.StartNew();

                    await SetMediaSourceAsync(requestedSource).ConfigureAwait(false);

                    //stopwatch.Stop();
                    //Debug.WriteLine("MediaPlayerSource.Worker() returned from SetMediaSourceAsync()");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MediaPlayerSource.Worker() failed: " + ex.Message);
            }
        }

        async Task SetMediaSourceAsync(Uri source)
        {
            await OpenMediaAsync(source).ConfigureAwait(true);

            await _setSourceAsync(_tsMediaStreamSource).ConfigureAwait(false);
        }

        public async Task StartPlaybackAsync()
        {
            Debug.WriteLine("MediaPlayerSource.StartPlaybackAsync()");

            try
            {
                if (null == _source)
                    return;

                if (null == _tsMediaManager || null == _tsMediaStreamSource || TsMediaManager.MediaState.Error == _tsMediaManager.State || TsMediaManager.MediaState.Closed == _tsMediaManager.State)
                {
                    if (null == _source)
                        return;

                    await OpenMediaAsync(_source).ConfigureAwait(false);

                    Debug.Assert(null != _tsMediaManager);
                }

                _tsMediaManager.Play();
            }
            catch (Exception ex)
            {
                // Send a "Failed" message here?
                Debug.WriteLine("MediaPlayerSource.StartPlaybackAsync() failed: " + ex.Message);
            }
        }

        async Task OpenMediaAsync(Uri source)
        {
            Debug.WriteLine("MediaPlayerSource.OpenMediaAsync()");

            if (null != _tsMediaStreamSource)
                await CloseMediaAsync().ConfigureAwait(false);

            Debug.Assert(null == _tsMediaStreamSource);
            Debug.Assert(null == _tsMediaManager);
            Debug.Assert(null == _playlist);

            if (null == source)
                return;

            if (!source.IsAbsoluteUri)
            {
                Debug.WriteLine("MediaPlayerSource.OpenMediaAsync() source is not absolute: " + source);
                return;
            }

            _source = source;

            _playlist = await _segmentManagerFactory.CreateDefaultAsync(source, DefaultSourceType).ConfigureAwait(false);

            var segmentReaderManager = new SegmentReaderManager(new[] { _playlist }, _httpClients.CreateSegmentClient);

            _tsMediaStreamSource = new TsMediaStreamSource();

            var mediaManagerParameters = new MediaManagerParameters
                                         {
                                             SegmentReaderManager = segmentReaderManager,
                                             MediaStreamSource = _tsMediaStreamSource
                                         };

            _tsMediaManager = new TsMediaManager(mediaManagerParameters);

            _tsMediaManager.OnStateChange += TsMediaManagerOnStateChange;
        }

        async Task CloseMediaAsync()
        {
            Debug.WriteLine("MediaPlayerSource.CloseMediaAsync()");

            var mediaManager = _tsMediaManager;

            if (null != mediaManager)
            {
                try
                {
                    //Debug.WriteLine("MediaPlayerSource.CloseMediaAsync() calling mediaManager.CloseAsync()");

                    await mediaManager.CloseAsync().ConfigureAwait(false);

                    //Debug.WriteLine("MediaPlayerSource.CloseMediaAsync() returned from mediaManager.CloseAsync()");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Media manager close failed: " + ex.Message);
                }

                mediaManager.OnStateChange -= TsMediaManagerOnStateChange;

                _tsMediaManager = null;
            }

            var playlist = _playlist;

            if (null != playlist)
            {
                _playlist = null;

                playlist.CleanupBackground("MediaPlayerSource.CloseMediaAsync playlist");
            }

            var tsMediaStreamSource = _tsMediaStreamSource;

            if (null != tsMediaStreamSource)
            {
                _tsMediaStreamSource = null;

                tsMediaStreamSource.DisposeBackground("MediaPlayerSource.CloseMediaAsync tsMediaStreamSource");
            }

            if (null != mediaManager)
                mediaManager.DisposeBackground("MediaPlayerSource.CloseMediaAsync mediaManager");

            _source = null;

            Debug.WriteLine("MediaPlayerSource.CloseMediaAsync() completed");
        }

        void TsMediaManagerOnStateChange(object sender, TsMediaManagerStateEventArgs e)
        {
            Debug.WriteLine("MediaPlayerSource.TsMediaManagerOnOnStateChange() to {0}: {1}", e.State, e.Message);
        }

        public void RequestStop()
        {
            Debug.WriteLine("MediaPlayerSource.Stop()");

            lock (_lock)
            {
                _stopRequested = true;
                _requestedSource = null;
            }

            _workerTask.Fire();
        }

        public Task CloseAsync()
        {
            Debug.WriteLine("MediaPlayerSource.CloseAsync()");

            if (null != _tsMediaManager)
                _tsMediaManager.OnStateChange -= TsMediaManagerOnStateChange;

            var playlist = _playlist;

            if (null != playlist)
            {
                _playlist = null;

                playlist.CleanupBackground("MediaPlayerSource.Cleanup() playlist.CloseAsync()");
            }

            return _workerTask.WaitAsync();
        }
    }
}

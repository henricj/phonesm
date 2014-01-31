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
using SM.Media.Segments;
using SM.Media.Utility;
using SM.Media.Web;

namespace SM.Media
{
    public interface IMediaStreamFascadeParameters
    {
        IHttpClients HttpClients { get; }
        ISegmentManagerFactory SegmentManagerFactory { get; }
        Func<IMediaStreamSource> MediaStreamSourceFactory { get; }
        IMediaManagerParameters MediaManagerParameters { get; }
    }

    public sealed class MediaStreamFascade : IDisposable
    {
        readonly AsyncFifoWorker _asyncFifoWorker = new AsyncFifoWorker(CancellationToken.None);
        readonly IHttpClients _httpClients;
        readonly IMediaManagerParameters _mediaManagerParameters;
        readonly Func<IMediaStreamSource> _mediaStreamSourceFactory;
        readonly ISegmentManagerFactory _segmentManagerFactory;
        readonly Func<IMediaStreamSource, Task> _setSourceAsync;
        IMediaStreamSource _mediaStreamSource;
        ISegmentManager _playlist;
        Uri _source;
        TsMediaManager _tsMediaManager;

        public MediaStreamFascade(IMediaStreamFascadeParameters mediaStreamFascadeParameters, Func<IMediaStreamSource, Task> setSourceAsync)
        {
            if (null == mediaStreamFascadeParameters)
                throw new ArgumentNullException("mediaStreamFascadeParameters");
            if (null == setSourceAsync)
                throw new ArgumentNullException("setSourceAsync");

            if (null == mediaStreamFascadeParameters.SegmentManagerFactory)
                throw new ArgumentException("SegmentManagerFactory cannot be null", "mediaStreamFascadeParameters");
            if (null == mediaStreamFascadeParameters.MediaStreamSourceFactory)
                throw new ArgumentException("MediaStreamSourceFactory cannot be null", "mediaStreamFascadeParameters");
            if (null == mediaStreamFascadeParameters.MediaManagerParameters)
                throw new ArgumentException("MediaManagerParameters cannot be null", "mediaStreamFascadeParameters");

            _httpClients = mediaStreamFascadeParameters.HttpClients;
            _segmentManagerFactory = mediaStreamFascadeParameters.SegmentManagerFactory;
            _mediaStreamSourceFactory = mediaStreamFascadeParameters.MediaStreamSourceFactory;
            _mediaManagerParameters = mediaStreamFascadeParameters.MediaManagerParameters;
            _setSourceAsync = setSourceAsync;
        }

        public Uri Source
        {
            get { return _source; }
            set
            {
                if (value == null)
                    Post(CloseMediaAsync);
                else if (value.IsAbsoluteUri)
                    Post(() => SetMediaSourceAsync(value));
                else
                {
                    Debug.WriteLine("MediaStreamFascade Source setter: invalid URL: " + value);
                    Post(CloseMediaAsync);
                }
            }
        }

        public TimeSpan? SeekTarget
        {
            get { return null == _mediaStreamSource ? null : _mediaStreamSource.SeekTarget; }
            set
            {
                if (null == _mediaStreamSource)
                    return;

                _mediaStreamSource.SeekTarget = value;
            }
        }

        public TsMediaManager.MediaState State
        {
            get
            {
                if (null == _tsMediaManager)
                    return TsMediaManager.MediaState.Closed;

                return _tsMediaManager.State;
            }
        }

        #region IDisposable Members

        public void Dispose()
        {
            StateChange = null;

            if (null != _tsMediaManager)
            {
                _tsMediaManager.OnStateChange -= TsMediaManagerOnStateChange;

                _tsMediaManager.DisposeSafe();
                _tsMediaManager = null;
            }

            if (null != _mediaStreamSource)
            {
                _mediaStreamSource.DisposeSafe();
                _mediaStreamSource = null;
            }

            _asyncFifoWorker.Dispose();
        }

        #endregion

        public event EventHandler<TsMediaManagerStateEventArgs> StateChange;

        void Post(Func<Task> work)
        {
            _asyncFifoWorker.Post(work);
        }

        async Task SetMediaSourceAsync(Uri source)
        {
            Debug.WriteLine("MediaStreamFascade.SetMediaSourceAsync({0})", source);

            await OpenMediaAsync(source).ConfigureAwait(true);

            await _setSourceAsync(_mediaStreamSource).ConfigureAwait(false);
        }

        public void Play()
        {
            Debug.WriteLine("MediaStreamFascade.Play()");

            Post(StartPlaybackAsync);
        }

        async Task StartPlaybackAsync()
        {
            Debug.WriteLine("MediaPlayerSource.StartPlaybackAsync()");

            try
            {
                if (null == _source)
                    return;

                if (null == _tsMediaManager || null == _mediaStreamSource || TsMediaManager.MediaState.Error == _tsMediaManager.State || TsMediaManager.MediaState.Closed == _tsMediaManager.State)
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

            if (null != _mediaStreamSource)
                await CloseMediaAsync().ConfigureAwait(false);

            Debug.Assert(null == _mediaStreamSource);
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

            _playlist = await _segmentManagerFactory.CreateAsync(source, CancellationToken.None).ConfigureAwait(false);

            var segmentReaderManager = new SegmentReaderManager(new[] { _playlist }, _httpClients.CreateSegmentClient);

            _mediaStreamSource = _mediaStreamSourceFactory();

            var mediaManagerParameters = new MediaManagerParameters
                                         {
                                             SegmentReaderManager = segmentReaderManager,
                                             MediaStreamSource = _mediaStreamSource,
                                             MediaElementManager = _mediaManagerParameters.MediaElementManager,
                                             BufferingManagerFactory = _mediaManagerParameters.BufferingManagerFactory,
                                             BufferingPolicy = _mediaManagerParameters.BufferingPolicy,
                                             ProgramStreamsHandler = _mediaManagerParameters.ProgramStreamsHandler
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

            var tsMediaStreamSource = _mediaStreamSource;

            if (null != tsMediaStreamSource)
            {
                _mediaStreamSource = null;

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

            var stateChange = StateChange;

            if (null == stateChange)
                return;

            try
            {
                stateChange(this, e);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MediaStreamFascade.TsMediaManagerOnStateChange() Exception in StateChange event handler: " + ex.Message);
            }
        }

        public void RequestStop()
        {
            Debug.WriteLine("MediaPlayerSource.Stop()");

            Post(CloseMediaAsync);
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

            var tcs = new TaskCompletionSource<bool>();

            Post(() =>
                 {
                     tcs.TrySetResult(true);

                     return TplTaskExtensions.CompletedTask;
                 });

            return tcs.Task;
        }
    }
}

// -----------------------------------------------------------------------
//  <copyright file="MediaPlayerManager.cs" company="Henric Jungheim">
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
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Playback;
using Windows.Web.Http;
using SM.Media.Buffering;
using SM.Media.MediaManager;
using SM.Media.Metadata;
using SM.Media.Playlists;
using SM.Media.Pls;
using SM.Media.TransportStream.TsParser;
using SM.Media.Utility;
using SM.Media.Web;

namespace SM.Media.BackgroundAudio
{
    sealed class MediaPlayerManager : IDisposable
    {
        static readonly IWebReaderManagerParameters WebReaderManagerParameters
            = new WebReaderManagerParameters
            {
                DefaultHeaders = new[] { new KeyValuePair<string, string>("icy-metadata", "1") }
            };

        readonly AsyncLock _asyncLock = new AsyncLock();
        readonly DefaultBufferingPolicy _bufferingPolicy;
        readonly CancellationToken _cancellationToken;
        readonly MediaManagerParameters _mediaManagerParameters;
        readonly MediaPlayer _mediaPlayer;
        readonly MetadataHandler _metadataHandler;
        readonly IList<MediaTrack> _tracks = TrackManager.Tracks;
        TaskCompletionSource<object> _closePlaybackCompleted;
        IMediaStreamFacade _mediaStreamFacade;
        TimeSpan? _position;
        MediaTrack _track;
        int _trackIndex;

        public MediaPlayerManager(MediaPlayer mediaPlayer, MetadataHandler metadataHandler, CancellationToken cancellationToken)
        {
            if (null == mediaPlayer)
                throw new ArgumentNullException("mediaPlayer");
            if (null == metadataHandler)
                throw new ArgumentNullException("metadataHandler");

            Debug.WriteLine("MediaPlayerManager.ctor()");

            _mediaPlayer = mediaPlayer;
            _metadataHandler = metadataHandler;
            _cancellationToken = cancellationToken;

            var parameters = MediaStreamFacadeSettings.Parameters;

            parameters.UseHttpConnection = true;
            //parameters.UseSingleStreamMediaManager = true;

            _mediaManagerParameters = new MediaManagerParameters
            {
                ProgramStreamsHandler =
                    streams =>
                    {
                        var firstAudio = streams.Streams.FirstOrDefault(x => x.StreamType.Contents == TsStreamType.StreamContents.Audio);

                        var others = null == firstAudio ? streams.Streams : streams.Streams.Where(x => x.Pid != firstAudio.Pid);
                        foreach (
                            var programStream in others)
                            programStream.BlockStream = true;
                    }
            };

            _bufferingPolicy = new DefaultBufferingPolicy
            {
                BytesMinimumStarting = 24 * 1024,
                BytesMinimum = 64 * 1024
            };

            _mediaPlayer.MediaOpened += MediaPlayerOnMediaOpened;
            _mediaPlayer.MediaEnded += MediaPlayerOnMediaEnded;
            _mediaPlayer.CurrentStateChanged += MediaPlayerOnCurrentStateChanged;
            _mediaPlayer.MediaFailed += MediaPlayerOnMediaFailed;

            _position = BackgroundSettings.Position;

            var currentTrackUrl = BackgroundSettings.Track;

            if (null == currentTrackUrl)
            {
                if (_position.HasValue)
                {
                    _position = null;
                    BackgroundSettings.Track = null;
                }

                return;
            }

            for (var i = 0; i < _tracks.Count; ++i)
            {
                var track = _tracks[i];

                if (null == track)
                    continue;

                if (track.Url == currentTrackUrl)
                {
                    _trackIndex = i;
                    return;
                }
            }

            if (_position.HasValue)
            {
                _position = null;
                BackgroundSettings.Track = null;
            }
        }

        public MediaPlayer MediaPlayer
        {
            get
            {
                // Sometimes the MediaPlayer likes to throw COM exceptions.
                // The NaturalDuration property does not throw, but returns
                // TimeSpan.MinValue instead.
                var duration = _mediaPlayer.NaturalDuration;

                if (duration == TimeSpan.MinValue)
                    return null;

                return _mediaPlayer;
            }
        }

        public string TrackName
        {
            get
            {
                if (null == _track)
                    return null;

                return _track.Title;
            }
        }

        #region IDisposable Members

        public void Dispose()
        {
            Debug.WriteLine("MediaPlayerManager.Dispose()");

            _mediaPlayer.MediaOpened -= MediaPlayerOnMediaOpened;
            _mediaPlayer.MediaEnded -= MediaPlayerOnMediaEnded;
            _mediaPlayer.CurrentStateChanged -= MediaPlayerOnCurrentStateChanged;
            _mediaPlayer.MediaFailed -= MediaPlayerOnMediaFailed;

            if (null != _mediaStreamFacade)
            {
                _mediaStreamFacade.StateChange -= MediaStreamFacadeOnStateChange;

                _mediaStreamFacade.Dispose();
                _mediaStreamFacade = null;
            }

            _asyncLock.Dispose();
        }

        #endregion

        public event EventHandler<string> TrackChanged;
        public event EventHandler<string> Failed;
        public event EventHandler<object> Ended;

        async Task InitializeMediaStreamAsync()
        {
            Debug.WriteLine("MediaPlayerManager.InitializeMediaStreamAsync()");

            if (null != _mediaStreamFacade)
            {
                await CloseMediaSourceAsync().WithCancellation(_cancellationToken).ConfigureAwait(false);

                try
                {
                    await _mediaStreamFacade.StopAsync(_cancellationToken).ConfigureAwait(false);

                    ForceGc();

                    return;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("MediaPlayerManager.InitializeMediaStreamAsync() stop failed: " + ex.ExtendedMessage());
                }

                try
                {
                    await CleanupMediaStreamFacadeAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("MediaPlayerManager.InitializeMediaStreamAsync() cleanup failed: " + ex.ExtendedMessage());
                }
            }

            ForceGc();

            _mediaStreamFacade = MediaStreamFacadeSettings.Parameters.Create();

            _mediaStreamFacade.SetParameter(_bufferingPolicy);

            _mediaStreamFacade.SetParameter(_mediaManagerParameters);

            _mediaStreamFacade.SetParameter(WebReaderManagerParameters);

            _mediaStreamFacade.SetParameter(_metadataHandler.MetadataSink);

            _mediaStreamFacade.StateChange += MediaStreamFacadeOnStateChange;
        }

        void MediaStreamFacadeOnStateChange(object sender, MediaManagerStateEventArgs args)
        {
            Debug.WriteLine("MediaPlayerManager.MediaStreamFacadeOnStateChange(): " + args.State + " message " + args.Message);
        }

        void MediaPlayerOnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            var message = args.Error + "/" + args.ErrorMessage;

            Debug.WriteLine("MediaPlayerManager.MediaPlayerOnMediaFailed(): " + message);

            if (CheckClosePlayback())
                return;

            var ex = args.ExtendedErrorCode;

            if (null != ex)
                Debug.WriteLine("MediaPlayerManager.MediaPlayerOnMediaFailed() extended error: " + ex.Message);

            var task = Task.Run(async () =>
            {
                await CloseMediaSourceAsync().ConfigureAwait(false);

                var isOk = false;

                try
                {
                    if (null != _mediaStreamFacade)
                    {
                        using (await _asyncLock.LockAsync(CancellationToken.None).ConfigureAwait(false))
                        {
                            await CleanupMediaStreamFacadeAsync().ConfigureAwait(false);
                        }
                    }

                    isOk = true;
                }
                catch (Exception ex2)
                {
                    Debug.WriteLine("MediaPlayerManager.MediaPlayerOnMediaFailed() cleanup failed: " + ex2.ExtendedMessage());
                }

                if (!isOk)
                    BackgroundMediaPlayer.Shutdown();

                try
                {
                    var failed = Failed;

                    if (null != failed)
                        failed(this, message);
                }
                catch (Exception ex2)
                {
                    Debug.WriteLine("MediaPlayerManager.MediaPlayerOnMediaFailed() invoke failed: " + ex2.ExtendedMessage());
                }
            });

            TaskCollector.Default.Add(task, "MediaPlayerManager OnMediaFailed");
        }

        async Task CleanupMediaStreamFacadeAsync()
        {
            Debug.WriteLine("MediaPlayerManager.CleanupMediaStreamFacadeAsync()");

            var msf = _mediaStreamFacade;

            if (null == msf)
                return;

            _mediaStreamFacade = null;

            msf.StateChange -= MediaStreamFacadeOnStateChange;

            await msf.CloseAsync().ConfigureAwait(false);

            msf.DisposeBackground("MediaPlayerManager CleanupMediaStreamFacadeAsync");
        }

        void MediaPlayerOnCurrentStateChanged(MediaPlayer sender, object args)
        {
            Debug.WriteLine("MediaPlayerManager.MediaPlayerOnCurrentStateChanged(): " + sender.CurrentState);

            if (!ReferenceEquals(sender, _mediaPlayer))
                Debug.WriteLine("MediaPlayerManager.MediaPlayerOnCurrentStateChanged() unexpected media player instance");
        }

        void MediaPlayerOnMediaEnded(MediaPlayer sender, object args)
        {
            Debug.WriteLine("MediaPlayerManager.MediaPlayerOnMediaEnded()");

            if (CheckClosePlayback())
                return;

            try
            {
                var handler = Ended;

                if (null != handler)
                    handler(this, args);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MediaPlayerManager.MediaPlayerOnMediaEnded() invoke failed: " + ex.ExtendedMessage());
            }
        }

        void MediaPlayerOnMediaOpened(MediaPlayer sender, object args)
        {
            Debug.WriteLine("MediaPlayerManager.MediaPlayerOnMediaOpened()");

            if (_position.HasValue)
            {
                if (sender.CanSeek)
                    sender.Position = _position.Value;

                _position = null;
            }

            sender.Play();

            if (null == _closePlaybackCompleted)
                FireTrackChanged();
        }

        void FireTrackChanged()
        {
            var track = _track;

            var trackChanged = TrackChanged;

            if (null != trackChanged)
            {
                try
                {
                    trackChanged(this, null == track ? null : track.Title);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("MediaPlayerManager.FireTrackChanged() failed " + ex.ExtendedMessage());
                }
            }
        }

        public void Next()
        {
            Debug.WriteLine("MediaPlayerManager.Next()");

            if (++_trackIndex >= _tracks.Count)
                _trackIndex = 0;

            if (_position.HasValue)
            {
                _position = null;
                BackgroundSettings.Position = null;
            }

            Play();
        }

        public void Previous()
        {
            Debug.WriteLine("MediaPlayerManager.Previous()");

            if (--_trackIndex < 0)
                _trackIndex = _tracks.Count - 1;

            if (_position.HasValue)
            {
                _position = null;
                BackgroundSettings.Position = null;
            }

            Play();
        }

        public void Play()
        {
            Debug.WriteLine("MediaPlayerManager.Play()");

            var track = _trackIndex;

            var mediaTracks = _tracks;

            if (track < 0 || track >= mediaTracks.Count)
            {
                Stop();
                return;
            }

            var t = StartPlaybackAsync(mediaTracks[track]);

            TaskCollector.Default.Add(t, "MediaPlayerManager Play");
        }

        async Task StartPlaybackAsync(MediaTrack track)
        {
            Debug.WriteLine("MediaPlayerManager.StartPlaybackAsync() " + (null == track ? "<null>" : track.ToString()));

            try
            {
                using (await _asyncLock.LockAsync(_cancellationToken).ConfigureAwait(false))
                {
                    _track = null;

                    if (null == track || null == track.Url)
                    {
                        await CloseMediaSourceAsync().WithCancellation(_cancellationToken).ConfigureAwait(false);

                        FireTrackChanged();

                        return;
                    }

                    var url = track.Url;

                    BackgroundSettings.Track = url;

                    if (url.HasExtension(".pls"))
                    {
                        url = await GetUrlFromPlsPlaylistAsync(url).ConfigureAwait(false);
                    }

                    _track = track;

                    FireTrackChanged();

                    try
                    {
                        _mediaPlayer.AutoPlay = false;

                        if (track.UseNativePlayer)
                        {
                            _mediaPlayer.SetUriSource(url);
                        }
                        else
                        {
                            await InitializeMediaStreamAsync().ConfigureAwait(false);

                            var mss = await _mediaStreamFacade.CreateMediaStreamSourceAsync(url, _cancellationToken).ConfigureAwait(false);

                            if (null == mss)
                            {
                                Debug.WriteLine("AudioTrackStreamer.StartPlaybackAsync() unable to create media stream source");
                                return;
                            }

                            _mediaPlayer.SetMediaSource(mss);
                        }

                        return;
                    }
                    catch (OperationCanceledException)
                    { }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("MediaPlayerManager.StartPlaybackAsync() source setup failed: " + ex.Message);
                    }

                    await CloseMediaSourceAsync().WithCancellation(_cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MediaPlayerManager.StartPlaybackAsync() failed: " + ex.Message);
            }
        }

        async Task<Uri> GetUrlFromPlsPlaylistAsync(Uri url)
        {
            using (var client = new HttpClient())
            using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseContentRead).AsTask(_cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();

                var buffer = await response.Content.ReadAsBufferAsync().AsTask(_cancellationToken).ConfigureAwait(false);

                var pls = new PlsParser(response.RequestMessage.RequestUri);

                using (var stream = new MemoryStream(buffer.ToArray(), false))
                using (var reader = new StreamReader(stream))
                {
                    if (!await pls.ParseAsync(reader).ConfigureAwait(false))
                        throw new FileNotFoundException("Unable to parse PLS playlist");
                }

                var track = pls.Tracks.FirstOrDefault();

                if (null == track)
                    throw new FileNotFoundException("Empty PLS playlist");

                Uri trackUrl;
                if (!Uri.TryCreate(pls.BaseUrl, track.File, out trackUrl))
                    throw new FileNotFoundException("Invalid URL in PLS playlist");

                url = trackUrl;
            }
            return url;
        }

        public void Pause()
        {
            Debug.WriteLine("MediaPlayerManager.Pause()");

            try
            {
                if (_mediaPlayer.CanPause)
                    _mediaPlayer.Pause();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MediaPlayerManager.Pause() failed: " + ex.Message);
            }
        }

        public void Stop()
        {
            Debug.WriteLine("MediaPlayerManager.Stop()");

            try
            {
                BackgroundSettings.Track = null;
                BackgroundSettings.Position = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MediaPlayerManager.Stop() update settings failed: " + ex.Message);
            }

            try
            {
                var task = StopAsync();

                TaskCollector.Default.Add(task, "MediaPlayerManager Stop");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MediaPlayerManager.Stop() failed: " + ex.Message);
            }
        }

        Task CloseMediaSourceAsync()
        {
            Debug.WriteLine("MediaPlayerManager.CloseMediaSource()");

            try
            {
                // TODO: How do we stop????

                if (_mediaPlayer.CurrentState == MediaPlayerState.Closed)
                    return TplTaskExtensions.CompletedTask;

                // If we don't call SetUriSource(null), the next SetMediaSource() call
                // can cause the mediaPlayer to get into a state where both foreground and
                // background property reads hang (reads from mediaPlayer.Position or
                // mediaPlayer.CanSeek block indefinitely).
                _mediaPlayer.SetUriSource(null);

                if (_mediaPlayer.CurrentState == MediaPlayerState.Closed)
                    return TplTaskExtensions.CompletedTask;

                // At this point, the mediaPlayer may be in the "Playing" state.  We play
                // a zero-length stream to get it into the "Closed" state.

                var tcs = new TaskCompletionSource<object>();

                var oldTcs = Interlocked.Exchange(ref _closePlaybackCompleted, tcs);

                if (null != oldTcs)
                    tcs.Task.ContinueWith(t => oldTcs.TrySetResult(null));

                _mediaPlayer.AutoPlay = true;
                _mediaPlayer.SetMediaSource(NullMediaSource.MediaSource);

                return tcs.Task;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MediaPlayerManager.CloseMediaSource() failed: " + ex.ExtendedMessage());
            }

            return TplTaskExtensions.CompletedTask;
        }

        bool CheckClosePlayback()
        {
            var tcs = Interlocked.Exchange(ref _closePlaybackCompleted, null);

            if (null == tcs)
                return false;

            Debug.WriteLine("MediaPlayerManager.CheckClosePlayback() completed");

            tcs.TrySetResult(null);

            return true;
        }

        public async Task StopAsync()
        {
            Debug.WriteLine("MediaPlayerManager.StopAsync()");

            var fireTrackChanged = false;

            using (await _asyncLock.LockAsync(_cancellationToken).ConfigureAwait(false))
            {
                if (null != _mediaStreamFacade)
                {
                    var stopped = false;

                    try
                    {
                        stopped = await _mediaStreamFacade.RequestStopAsync(TimeSpan.FromSeconds(5), _cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("MediaPlayerManager.StopAsync() RequestStopAsync() failed: " + ex.ExtendedMessage());
                    }

                    if (!stopped)
                    {
                        try
                        {
                            await CleanupMediaStreamFacadeAsync().ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("MediaPlayerManager.StopAsync() CleanupMediaStreamFacadeAsync() failed: " + ex.ExtendedMessage());
                        }
                    }
                }

                if (null != _track)
                {
                    _track = null;
                    fireTrackChanged = true;
                }

                await CloseMediaSourceAsync().WithCancellation(_cancellationToken).ConfigureAwait(false);
            }

            if (fireTrackChanged)
                FireTrackChanged();
        }

        public async Task CloseAsync()
        {
            Debug.WriteLine("MediaPlayerManager.CloseAsync()");

            using (await _asyncLock.LockAsync(CancellationToken.None).ConfigureAwait(false))
            {
                if (null == _mediaStreamFacade)
                    return;

                try
                {
                    await _mediaStreamFacade.CloseAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("MediaPlayerManager.CloseAsync() failed: " + ex.ExtendedMessage());
                }
            }
        }

        /// <summary>
        ///     We eventually run out of unmanaged (!) memory.  Forcing a GC seems to help.  Yes this is a hack.
        /// </summary>
        static void ForceGc()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}

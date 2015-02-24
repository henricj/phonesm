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
using Windows.Foundation;
using Windows.Media.Playback;
using Windows.Storage.Streams;
using Windows.Web.Http;
using SM.Media.Buffering;
using SM.Media.MediaManager;
using SM.Media.Pls;
using SM.Media.Utility;
using SM.Media.Web;
using SM.TsParser;

namespace SM.Media.BackgroundAudio
{
    sealed class MediaPlayerManager : IDisposable
    {
        static readonly IRandomAccessStream EmptyStream = new InMemoryRandomAccessStream();
        readonly AsyncLock _asyncLock = new AsyncLock();
        readonly DefaultBufferingPolicy _bufferingPolicy;
        readonly CancellationToken _cancellationToken;
        readonly MediaManagerParameters _mediaManagerParameters;
        readonly MediaPlayer _mediaPlayer;
        readonly IList<MediaTrack> _tracks = TrackManager.Tracks;
        IMediaStreamFacade _mediaStreamFacade;
        MediaTrack _track;
        int _trackIndex;

        public MediaPlayerManager(MediaPlayer mediaPlayer, CancellationToken cancellationToken)
        {
            Debug.WriteLine("MediaPlayerManager.ctor()");

            if (null == mediaPlayer)
                throw new ArgumentNullException("mediaPlayer");

            _mediaPlayer = mediaPlayer;
            _cancellationToken = cancellationToken;

            var parameters = MediaStreamFacadeSettings.Parameters;

            parameters.UseHttpConnection = true;
            //parameters.UseSingleStreamMediaManager = true;

            _mediaManagerParameters = new MediaManagerParameters
            {
                ProgramStreamsHandler =
                    streams =>
                    {
                        var firstAudio = streams.Streams.First(x => x.StreamType.Contents == TsStreamType.StreamContents.Audio);

                        var others = streams.Streams.Where(x => x.Pid != firstAudio.Pid);
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
        }

        public MediaPlayer MediaPlayer
        {
            get { return _mediaPlayer; }
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
                _mediaStreamFacade.Dispose();
                _mediaStreamFacade = null;
            }

            _asyncLock.Dispose();
        }

        #endregion

        public event TypedEventHandler<MediaPlayerManager, string> TrackChanged;
        public event TypedEventHandler<MediaPlayerManager, Exception> Failed;

        void InitializeMediaStream()
        {
            if (null != _mediaStreamFacade)
                return;

            _mediaStreamFacade = MediaStreamFacadeSettings.Parameters.Create();

            _mediaStreamFacade.SetParameter(_bufferingPolicy);

            _mediaStreamFacade.SetParameter(_mediaManagerParameters);

            _mediaStreamFacade.StateChange += MediaStreamFacadeOnStateChange;
        }

        void MediaStreamFacadeOnStateChange(object sender, MediaManagerStateEventArgs args)
        {
            Debug.WriteLine("MediaPlayerManager.MediaStreamFacadeOnStateChange(): " + args.State + " message " + args.Message);
        }

        async void MediaPlayerOnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            Debug.WriteLine("MediaPlayerManager.MediaPlayerOnMediaFailed(): " + args.Error + "/" + args.ErrorMessage);

            var ex = args.ExtendedErrorCode;

            if (null != ex)
                Debug.WriteLine("MediaPlayerManager.MediaPlayerOnMediaFailed() extended error: " + ex.Message);

            Failed.Invoke(this, ex);

            var msf = _mediaStreamFacade;

            if (null != msf)
            {
                try
                {
                    _mediaStreamFacade = null;

                    msf.StateChange -= MediaStreamFacadeOnStateChange;

                    await msf.CloseAsync().ConfigureAwait(false);

                    msf.DisposeBackground("MediaPlayerManager OnFailed");
                }
                catch (Exception ex2)
                {
                    Debug.WriteLine("MediaPlayerManager.MediaPlayerOnMediaFailed() cleanup failed: " + ex2.ExtendedMessage());
                }
            }
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

            Next();
        }

        void MediaPlayerOnMediaOpened(MediaPlayer sender, object args)
        {
            Debug.WriteLine("MediaPlayerManager.MediaPlayerOnMediaOpened()");

            sender.Play();

            var track = _track;

            if (null != track)
                TrackChanged.Invoke(this, track.Title);
        }

        public void Next()
        {
            Debug.WriteLine("MediaPlayerManager.Next()");

            if (++_trackIndex >= _tracks.Count)
                _trackIndex = 0;

            Play();
        }

        public void Previous()
        {
            Debug.WriteLine("MediaPlayerManager.Previous()");

            if (--_trackIndex < 0)
                _trackIndex = _tracks.Count - 1;

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
            Debug.WriteLine("MediaPlayerManager.StartPlaybackAsync()");

            _track = null;

            if (null == track)
            {
                StopMediaPlayer();
                return;
            }

            var url = track.Url;

            if (null == url)
            {
                StopMediaPlayer();
                return;
            }

            _mediaPlayer.AutoPlay = false;

            if (url.HasExtension(".pls"))
            {
                url = await GetUrlFromPlsPlaylistAsync(url).ConfigureAwait(false);
            }

            using (await _asyncLock.LockAsync(_cancellationToken).ConfigureAwait(false))
            {
                _track = track;

                try
                {
                    if (track.UseNativePlayer)
                    {
                        _mediaPlayer.SetUriSource(url);
                    }
                    else
                    {
                        InitializeMediaStream();

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
                {
                    return;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("MediaPlayerManager.StartPlaybackAsync() failed: " + ex.Message);
                }

                StopMediaPlayer();
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
                StopMediaPlayer();

                var mediaStreamFacade = _mediaStreamFacade;

                if (null == mediaStreamFacade)
                    return;

                mediaStreamFacade.RequestStop();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MediaPlayerManager.Stop() failed: " + ex.Message);
            }
        }

        void StopMediaPlayer()
        {
            Debug.WriteLine("MediaPlayerManager.StopMediaPlayer()");

            // TODO: How do we stop????

            //_mediaPlayer.SetUriSource(new Uri("ms-appx:///Assets/There is no such file"));
            _mediaPlayer.SetStreamSource(EmptyStream);
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
    }
}

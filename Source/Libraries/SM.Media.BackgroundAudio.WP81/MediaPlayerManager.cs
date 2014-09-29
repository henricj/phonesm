// -----------------------------------------------------------------------
//  <copyright file="MediaPlayerManager.cs" company="Henric Jungheim">
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Media.Playback;
using SM.Media.Buffering;
using SM.Media.Utility;
using SM.TsParser;

namespace SM.Media.BackgroundAudio
{
    sealed class MediaPlayerManager : IDisposable
    {
        readonly DefaultBufferingPolicy _bufferingPolicy;
        readonly MediaManagerParameters _mediaManagerParameters;
        readonly MediaPlayer _mediaPlayer;
        IMediaStreamFacade _mediaStreamFacade;

        public MediaPlayerManager(MediaPlayer mediaPlayer)
        {
            Debug.WriteLine("MediaPlayerManager.ctor()");

            if (null == mediaPlayer)
                throw new ArgumentNullException("mediaPlayer");

            _mediaPlayer = mediaPlayer;

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

        #region IDisposable Members

        public void Dispose()
        {
            Debug.WriteLine("MediaPlayerManager.Dispose()");

            _mediaPlayer.MediaOpened -= MediaPlayerOnMediaOpened;
            _mediaPlayer.MediaEnded -= MediaPlayerOnMediaEnded;
            _mediaPlayer.CurrentStateChanged -= MediaPlayerOnCurrentStateChanged;
            _mediaPlayer.MediaFailed -= MediaPlayerOnMediaFailed;
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

        void MediaStreamFacadeOnStateChange(object sender, TsMediaManagerStateEventArgs args)
        {
            Debug.WriteLine("MediaPlayerManager.MediaStreamFacadeOnStateChange(): " + args.State + " message " + args.Message);
        }

        void MediaPlayerOnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            Debug.WriteLine("MediaPlayerManager.MediaPlayerOnMediaFailed(): " + args.Error + "/" + args.ErrorMessage);

            var ex = args.ExtendedErrorCode;

            if (null != ex)
                Debug.WriteLine("MediaPlayerManager.MediaPlayerOnMediaFailed(): " + ex.Message);

            Failed.Invoke(this, ex);

            Stop();

            if (null != _mediaStreamFacade)
            {
                var msf = _mediaStreamFacade;

                _mediaStreamFacade = null;

                msf.StateChange -= MediaStreamFacadeOnStateChange;

                msf.DisposeBackground("MediaPlayerManager OnFailed");
            }
        }

        void MediaPlayerOnCurrentStateChanged(MediaPlayer sender, object args)
        {
            Debug.WriteLine("MediaPlayerManager.MediaPlayerOnCurrentStateChanged()");
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

            TrackChanged.Invoke(this, "Test");
        }

        public void Next()
        {
            Debug.WriteLine("MediaPlayerManager.Next()");

            Play();
        }

        public void Previous()
        {
            Debug.WriteLine("MediaPlayerManager.Previous()");

            Play();
        }

        public void Play()
        {
            Debug.WriteLine("MediaPlayerManager.Play()");

            var t = StartPlaybackAsync(new Uri("http://devimages.apple.com/iphone/samples/bipbop/bipbopall.m3u8"));

            TaskCollector.Default.Add(t, "MediaPlayerManager Play");
        }

        async Task StartPlaybackAsync(Uri url)
        {
            Debug.WriteLine("MediaPlayerManager.StartPlaybackAsync()");

            _mediaPlayer.AutoPlay = false;

            try
            {
                InitializeMediaStream();

                var mss = await _mediaStreamFacade.CreateMediaStreamSourceAsync(url, CancellationToken.None).ConfigureAwait(false);

                if (null == mss)
                {
                    Debug.WriteLine("AudioTrackStreamer.StartPlaybackAsync() unable to create media stream source");
                    return;
                }

                _mediaPlayer.SetMediaSource(mss);

                //_mediaPlayer.SetUriSource(url);

                return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MediaPlayerManager.StartPlaybackAsync() failed: " + ex.Message);
            }

            Stop();
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
                // TODO: How do we stop????
                _mediaPlayer.SetUriSource(new Uri("ms-appx:///Assets/There is no such file"));
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MediaPlayerManager.Stop() cleanup failed: " + ex.Message);
            }
        }
    }
}

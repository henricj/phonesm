// -----------------------------------------------------------------------
//  <copyright file="StreamingMediaPlugin.Windows.cs" company="Henric Jungheim">
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
using System.Diagnostics;
using System.Windows;
using Microsoft.PlayerFramework;
using SM.Media.Utility;

// We isolate the bits that use System.Windows in this partial class
// so that the rest of StreamingMediaPlugin's implementation can be
// shared by all platforms.

namespace SM.Media.MediaPlayer
{
    public partial class StreamingMediaPlugin : IPlugin
    {
        Microsoft.PlayerFramework.MediaPlayer _player;

        #region IPlugin Members

        public Microsoft.PlayerFramework.MediaPlayer MediaPlayer
        {
            get { return _player; }
            set
            {
                if (null != _player)
                {
                    _player.MediaLoading -= PlayerOnMediaLoading;
                    _player.MediaOpened -= PlayerOnMediaOpened;
                    _player.MediaEnding -= PlayerOnMediaEnding;
                    _player.MediaFailed -= PlayerOnMediaFailed;
                    _player.MediaEnded -= PlayerOnMediaEnded;
                    _player.MediaClosed -= PlayerOnMediaClosed;
#if WINDOWS_PHONE7
                    _player.Seeked -= PlayerOnSeeked;
                    _player.Scrubbing -= PlayerOnScrubbing;
#endif // WINDOWS_PHONE7
                }

                _player = value;

                if (null != _player)
                {
                    _player.MediaLoading += PlayerOnMediaLoading;
                    _player.MediaOpened += PlayerOnMediaOpened;
                    _player.MediaEnding += PlayerOnMediaEnding;
                    _player.MediaFailed += PlayerOnMediaFailed;
                    _player.MediaEnded += PlayerOnMediaEnded;
                    _player.MediaClosed += PlayerOnMediaClosed;
#if WINDOWS_PHONE7
                    _player.Seeked += PlayerOnSeeked;
                    _player.Scrubbing += PlayerOnScrubbing;
#endif // WINDOWS_PHONE7
                }
            }
        }

        #endregion

#if WINDOWS_PHONE7
        void PlayerOnScrubbing(object sender, ScrubProgressRoutedEventArgs scrubProgressRoutedEventArgs)
        {
            Debug.WriteLine("StreamingMediaPlugin Scrubbing to " + scrubProgressRoutedEventArgs.Position);

            _mediaStreamFacade.SeekTarget = scrubProgressRoutedEventArgs.Position;
        }

        void PlayerOnSeeked(object sender, SeekRoutedEventArgs seekRoutedEventArgs)
        {
            Debug.WriteLine("StreamingMediaPlugin Seeked to " + seekRoutedEventArgs.Position);

            _mediaStreamFacade.SeekTarget = seekRoutedEventArgs.Position;
        }
#endif // WINDOWS_PHONE7

        void PlayerOnMediaLoading(object sender, MediaPlayerDeferrableEventArgs mediaPlayerDeferrableEventArgs)
        {
            Debug.WriteLine("StreamingMediaPlugin MediaLoading");

            var task = PlaybackLoadingAsync((MediaLoadingEventArgs)mediaPlayerDeferrableEventArgs);

            TaskCollector.Default.Add(task, "StreamingMediaPlugin MediaLoading PlaybackLoadingAsync");
        }

        void PlayerOnMediaOpened(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("StreamingMediaPlugin MediaOpened " + _playbackSession);
        }

        void PlayerOnMediaClosed(object sender, RoutedEventArgs routedEventArgs)
        {
            Debug.WriteLine("StreamingMediaPlugin MediaClosed " + _playbackSession);
        }

        void PlayerOnMediaEnding(object sender, MediaPlayerDeferrableEventArgs mediaPlayerDeferrableEventArgs)
        {
            Debug.WriteLine("StreamingMediaPlugin MediaEnding " + _playbackSession);
        }

        void PlayerOnMediaEnded(object sender, MediaPlayerActionEventArgs mediaPlayerActionEventArgs)
        {
            Debug.WriteLine("StreamingMediaPlugin MediaEnded " + _playbackSession);
        }

        void PlayerOnMediaFailed(object sender, ExceptionRoutedEventArgs exceptionRoutedEventArgs)
        {
            Debug.WriteLine("StreamingMediaPlugin MediaFailed " + _playbackSession);

            PlaybackFailed();
        }

        void RequestMetadataUpdate()
        {
            var mediaPlayer = MediaPlayer;

            if (null == mediaPlayer)
                return;

            var task = mediaPlayer.Dispatcher.DispatchAsync((Action)UpdateMetadata);

            TaskCollector.Default.Add(task, "StreamingMediaPlugin RequestMetadataUpdate");
        }
    }
}

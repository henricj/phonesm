// -----------------------------------------------------------------------
//  <copyright file="AudioPlayer.cs" company="Mikael Koskinen">
//  Copyright (c) 2013.
//  <author>Mikael Koskinen</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2013 Mikael Koskinen <mikael.koskinen@live.com>
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
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Phone.BackgroundAudio;

namespace SM.Media.BackgroundAudioStreamingAgent
{
    public class AudioPlayer : AudioPlayerAgent
    {
        static readonly AudioTrack[] AudioTracks =
        {
            new AudioTrack(null, "Apple", null, null, null,
                "http://devimages.apple.com/iphone/samples/bipbop/bipbopall.m3u8",
                EnabledPlayerControls.All),
            new AudioTrack(null, "BBC", null, null, null,
                "http://www.bbc.co.uk/mediaselector/playlists/hls/radio/bbc_london.m3u8",
                EnabledPlayerControls.All),
            new AudioTrack(null, "NPR", null, null, null,
                "http://www.npr.org/streams/mp3/nprlive24.pls",
                EnabledPlayerControls.All)
        };

        static volatile bool _classInitialized;

        static int _currentTrack = -1;

        /// <remarks>
        ///     AudioPlayer instances can share the same process.
        ///     Static fields can be used to share state between AudioPlayer instances
        ///     or to communicate with the Audio Streaming agent.
        /// </remarks>
        public AudioPlayer()
        {
            if (!_classInitialized)
            {
                _classInitialized = true;
                // Subscribe to the managed exception handler
                Deployment.Current.Dispatcher.BeginInvoke(delegate
                                                          {
                                                              Application.Current.UnhandledException += AudioPlayer_UnhandledException;

                                                              TaskScheduler.UnobservedTaskException += AudioPlayer_UnobservedException;
                                                          });
            }
        }

        /// Code to execute on Unhandled Exceptions
        void AudioPlayer_UnhandledException(object sender, ApplicationUnhandledExceptionEventArgs e)
        {
            Debug.WriteLine("AudioPlayer.UnhandledException() " + e.ExceptionObject.Message);

            if (Debugger.IsAttached)
            {
                // An unhandled exception has occurred; break into the debugger
                Debugger.Break();
            }
        }

        void AudioPlayer_UnobservedException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Debug.WriteLine("AudioPlayer UnobservedException {0}", e.Exception.Message);

            if (Debugger.IsAttached)
                Debugger.Break();
        }

        /// <summary>
        ///     Called when the playstate changes, except for the Error state (see OnError)
        /// </summary>
        /// <param name="player">The BackgroundAudioPlayer</param>
        /// <param name="track">The track playing at the time the playstate changed</param>
        /// <param name="playState">The new playstate of the player</param>
        /// <remarks>
        ///     Play State changes cannot be cancelled. They are raised even if the application
        ///     caused the state change itself, assuming the application has opted-in to the callback.
        ///     Notable playstate events:
        ///     (a) TrackEnded: invoked when the player has no current track. The agent can set the next track.
        ///     (b) TrackReady: an audio track has been set and it is now ready for playack.
        ///     Call NotifyComplete() only once, after the agent request has been completed, including async callbacks.
        /// </remarks>
        protected override void OnPlayStateChanged(BackgroundAudioPlayer player, AudioTrack track, PlayState playState)
        {
            Debug.WriteLine("AudioPlayer.OnPlayStateChanged() track.Source {0} track.Tag {1} playState {2}",
                null == track ? "<no track>" : null == track.Source ? "<none>" : track.Source.ToString(),
                null == track ? "<no track>" : track.Tag ?? "<none>", playState);

            switch (playState)
            {
            case PlayState.TrackEnded:
                player.Track = GetNextTrack();
                break;
            case PlayState.TrackReady:
                player.Play();
                break;
            case PlayState.Shutdown:
                break;
            case PlayState.Unknown:
                break;
            case PlayState.Stopped:
                break;
            case PlayState.Paused:
                break;
            case PlayState.Playing:
                break;
            case PlayState.BufferingStarted:
                break;
            case PlayState.BufferingStopped:
                break;
            case PlayState.Rewinding:
                break;
            case PlayState.FastForwarding:
                break;
            default:
                Debug.WriteLine("AudioPlayer.OnPlayStateChanged() unknown playstate: " + playState);
                break;
            }

            NotifyComplete();
        }

        /// <summary>
        ///     Called when the user requests an action using application/system provided UI
        /// </summary>
        /// <param name="player">The BackgroundAudioPlayer</param>
        /// <param name="track">The track playing at the time of the user action</param>
        /// <param name="action">The action the user has requested</param>
        /// <param name="param">
        ///     The data associated with the requested action.
        ///     In the current version this parameter is only for use with the Seek action,
        ///     to indicate the requested position of an audio track
        /// </param>
        /// <remarks>
        ///     User actions do not automatically make any changes in system state; the agent is responsible
        ///     for carrying out the user actions if they are supported.
        ///     Call NotifyComplete() only once, after the agent request has been completed, including async callbacks.
        /// </remarks>
        protected override void OnUserAction(BackgroundAudioPlayer player, AudioTrack track, UserAction action, object param)
        {
            Debug.WriteLine("AudioPlayer.OnUserAction() track.Source {0} track.Tag {1} action {2}",
                null == track ? "<no track>" : null == track.Source ? "<none>" : track.Source.ToString(),
                null == track ? "<no track>" : track.Tag ?? "<none>", action);

            switch (action)
            {
            case UserAction.Play:
                UpdateTrack(player);

                if (PlayState.Playing != player.PlayerState && null != player.Track)
                    player.Play();

                break;
            case UserAction.Stop:
                player.Stop();

                break;
            case UserAction.Pause:
                if (PlayState.Playing == player.PlayerState)
                    player.Pause();

                break;
            case UserAction.FastForward:
                if (null != track && null != track.Source)
                    player.FastForward();

                break;
            case UserAction.Rewind:
                if (null != track && null != track.Source)
                    player.Rewind();

                break;
            case UserAction.Seek:
                if (null != track)
                    player.Position = (TimeSpan) param;

                break;
            case UserAction.SkipNext:
                player.Track = GetNextTrack();

                if (PlayState.Playing != player.PlayerState && null != player.Track)
                    player.Play();

                break;
            case UserAction.SkipPrevious:
                var previousTrack = GetPreviousTrack();
                if (previousTrack != null)
                    player.Track = previousTrack;

                if (PlayState.Playing != player.PlayerState && null != player.Track)
                    player.Play();

                break;
            }

            NotifyComplete();
        }

        static void UpdateTrack(BackgroundAudioPlayer player)
        {
            if (_currentTrack < 0)
                _currentTrack = 0;
            else if (_currentTrack >= AudioTracks.Length)
                _currentTrack = AudioTracks.Length - 1;

            var track = AudioTracks[_currentTrack];

            var playerTrack = player.Track;

            if (!ReferenceEquals(track, playerTrack) || playerTrack.Source != track.Source || playerTrack.Tag != track.Tag)
                player.Track = track;
        }

        /// <summary>
        ///     Implements the logic to get the next AudioTrack instance.
        ///     In a playlist, the source can be from a file, a web request, etc.
        /// </summary>
        /// <remarks>
        ///     The AudioTrack URI determines the source, which can be:
        ///     (a) Isolated-storage file (Relative URI, represents path in the isolated storage)
        ///     (b) HTTP URL (absolute URI)
        ///     (c) MediaStreamSource (null)
        /// </remarks>
        /// <returns>an instance of AudioTrack, or null if the playback is completed</returns>
        AudioTrack GetNextTrack()
        {
            Debug.WriteLine("AudioPlayer.GetNextTrack()");

            if (_currentTrack + 1 >= AudioTracks.Length)
                _currentTrack = 0;
            else
                ++_currentTrack;

            var track = AudioTracks[_currentTrack];

            Debug.WriteLine("AudioPlayer.GetNextTrack() track " + track.ToExtendedString());

            return track;
        }

        /// <summary>
        ///     Implements the logic to get the previous AudioTrack instance.
        /// </summary>
        /// <remarks>
        ///     The AudioTrack URI determines the source, which can be:
        ///     (a) Isolated-storage file (Relative URI, represents path in the isolated storage)
        ///     (b) HTTP URL (absolute URI)
        ///     (c) MediaStreamSource (null)
        /// </remarks>
        /// <returns>an instance of AudioTrack, or null if previous track is not allowed</returns>
        AudioTrack GetPreviousTrack()
        {
            Debug.WriteLine("AudioPlayer.GetPreviousTrack()");

            if (_currentTrack <= 0)
                _currentTrack = AudioTracks.Length - 1;
            else
                --_currentTrack;

            var track = AudioTracks[_currentTrack];

            Debug.WriteLine("AudioPlayer.GetPreviousTrack() track " + track.ToExtendedString());

            return track;
        }

        /// <summary>
        ///     Called whenever there is an error with playback, such as an AudioTrack not downloading correctly
        /// </summary>
        /// <param name="player">The BackgroundAudioPlayer</param>
        /// <param name="track">The track that had the error</param>
        /// <param name="error">The error that occurred</param>
        /// <param name="isFatal">If true, playback cannot continue and playback of the track will stop</param>
        /// <remarks>
        ///     This method is not guaranteed to be called in all cases. For example, if the background agent
        ///     itself has an unhandled exception, it won't get called back to handle its own errors.
        /// </remarks>
        protected override void OnError(BackgroundAudioPlayer player, AudioTrack track, Exception error, bool isFatal)
        {
            Debug.WriteLine("AudioPlayer.OnError() track.Source {0} track.Tag {1} error {2} isFatal {3}",
                null == track ? "<no track>" : null == track.Source ? "<none>" : track.Source.ToString(),
                null == track ? "<no track>" : track.Tag ?? "<none>",
                error, isFatal);

            if (isFatal)
                Abort();
            else
            {
                player.Track = null;

                NotifyComplete();
            }
        }

        /// <summary>
        ///     Called when the agent request is getting cancelled
        /// </summary>
        /// <remarks>
        ///     Once the request is Cancelled, the agent gets 5 seconds to finish its work,
        ///     by calling NotifyComplete()/Abort().
        /// </remarks>
        protected override void OnCancel()
        {
            Debug.WriteLine("AudioPlayer.OnCancel()");
        }
    }
}

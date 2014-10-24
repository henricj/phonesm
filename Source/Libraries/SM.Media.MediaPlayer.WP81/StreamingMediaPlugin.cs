// -----------------------------------------------------------------------
//  <copyright file="StreamingMediaPlugin.cs" company="Henric Jungheim">
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
using Windows.UI.Xaml;
using Microsoft.PlayerFramework;
using SM.Media.Utility;

namespace SM.Media.MediaPlayer
{
    public class StreamingMediaPlugin : IPlugin
    {
        IMediaStreamFacade _mediaStreamFacade;
        Microsoft.PlayerFramework.MediaPlayer _player;

        #region IPlugin Members

        public virtual void Load()
        {
            Debug.WriteLine("StreamingMediaPlugin.Load()");
        }

        public virtual void Update(IMediaSource mediaSource)
        {
            Debug.WriteLine("StreamingMediaPlugin.Update()");
        }

        public virtual void Unload()
        {
            Debug.WriteLine("StreamingMediaPlugin.Unload()");

            Cleanup();
        }

        public Microsoft.PlayerFramework.MediaPlayer MediaPlayer
        {
            get { return _player; }
            set
            {
                if (null != _player)
                {
                    _player.MediaLoading -= PlayerOnMediaLoading;
                    _player.MediaEnding -= PlayerOnMediaEnding;
                    _player.MediaFailed -= PlayerOnMediaFailed;
                    _player.MediaEnded -= PlayerOnMediaEnded;
                    _player.MediaClosed -= PlayerOnMediaClosed;
                }
                _player = value;
                if (null != _player)
                {
                    _player.MediaLoading += PlayerOnMediaLoading;
                    _player.MediaEnding += PlayerOnMediaEnding;
                    _player.MediaFailed += PlayerOnMediaFailed;
                    _player.MediaEnded += PlayerOnMediaEnded;
                    _player.MediaClosed += PlayerOnMediaClosed;
                }
            }
        }

        #endregion

        void PlayerOnMediaClosed(object sender, RoutedEventArgs routedEventArgs)
        {
            Debug.WriteLine("StreamingMediaPlugin MediaClosed");

            Stop();
        }

        void PlayerOnMediaEnded(object sender, MediaPlayerActionEventArgs mediaPlayerActionEventArgs)
        {
            Debug.WriteLine("StreamingMediaPlugin MediaEnded");

            Stop();
        }

        void PlayerOnMediaFailed(object sender, ExceptionRoutedEventArgs exceptionRoutedEventArgs)
        {
            Debug.WriteLine("StreamingMediaPlugin MediaFailed");

            Stop();

            Cleanup();
        }

        void PlayerOnMediaEnding(object sender, MediaPlayerDeferrableEventArgs mediaPlayerDeferrableEventArgs)
        {
            Debug.WriteLine("StreamingMediaPlugin MediaEnding");
        }

        async void PlayerOnMediaLoading(object sender, MediaPlayerDeferrableEventArgs mediaPlayerDeferrableEventArgs)
        {
            Debug.WriteLine("StreamingMediaPlugin MediaLoading");

            var mediaLoadingEventArgs = (MediaLoadingEventArgs)mediaPlayerDeferrableEventArgs;

            var source = mediaLoadingEventArgs.Source;

            if (null == source)
                return;

            MediaPlayerDeferral deferral = null;

            try
            {
                InitializeMediaStream();

                deferral = mediaPlayerDeferrableEventArgs.DeferrableOperation.GetDeferral();

                Debug.Assert(!deferral.CancellationToken.IsCancellationRequested, "MediaPlayer cancellation token is already cancelled");

                var mss = await _mediaStreamFacade.CreateMediaStreamSourceAsync(source, deferral.CancellationToken).ConfigureAwait(false);

                mediaLoadingEventArgs.Source = null;
                mediaLoadingEventArgs.MediaStreamSource = mss;

                deferral.Complete();
                deferral = null;
            }
            catch (OperationCanceledException)
            {
                Cleanup();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("StreamingMediaPlugin.PlayerOnMediaLoading() failed: " + ex.Message);
                Cleanup();
            }
            finally
            {
                if (null != deferral)
                    deferral.Cancel();
            }
        }

        void InitializeMediaStream()
        {
            if (null != _mediaStreamFacade)
                return;

            _mediaStreamFacade = CreateMediaStreamFacade();
        }

        protected virtual IMediaStreamFacade CreateMediaStreamFacade()
        {
            return MediaStreamFacadeSettings.Parameters.Create();
        }

        protected virtual void Stop()
        {
            Debug.WriteLine("StreamingMediaPlugin.Close()");

            var msf = _mediaStreamFacade;

            if (null == msf)
                return;

            msf.RequestStop();
        }

        protected virtual void Cleanup()
        {
            Debug.WriteLine("StreamingMediaPlugin.Cleanup()");

            var msf = _mediaStreamFacade;

            if (null == msf)
                return;

            _mediaStreamFacade = null;

            msf.DisposeBackground("StreamingMediaPlugin Unload");
        }
    }
}

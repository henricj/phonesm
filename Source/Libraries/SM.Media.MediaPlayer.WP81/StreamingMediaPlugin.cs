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

using System.Diagnostics;
using Windows.UI.Xaml;
using Microsoft.PlayerFramework;
using SM.Media.Utility;
using SM.Media.Web;
using SM.Media.Web.HttpClientReader;

namespace SM.Media.MediaPlayer
{
    public class StreamingMediaPlugin : IPlugin
    {
        static readonly IApplicationInformation ApplicationInformation = ApplicationInformationFactory.DefaultTask.Result;

        readonly IHttpClientsParameters _httpClientsParameters;
        IMediaStreamFacade _mediaStreamFacade;
        Microsoft.PlayerFramework.MediaPlayer _player;

        public StreamingMediaPlugin()
        {
            var userAgent = ApplicationInformation.CreateUserAgent();

            _httpClientsParameters = new HttpClientsParameters
                                     {
                                         UserAgent = userAgent
                                     };
        }

        #region IPlugin Members

        public void Load()
        {
            Debug.WriteLine("StreamingMediaPlugin.Load()");
        }

        public void Update(IMediaSource mediaSource)
        {
            Debug.WriteLine("StreamingMediaPlugin.Update()");
        }

        public void Unload()
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

            Close();
        }

        void PlayerOnMediaEnded(object sender, MediaPlayerActionEventArgs mediaPlayerActionEventArgs)
        {
            Debug.WriteLine("StreamingMediaPlugin MediaEnded");

            Close();
        }

        void PlayerOnMediaFailed(object sender, ExceptionRoutedEventArgs exceptionRoutedEventArgs)
        {
            Debug.WriteLine("StreamingMediaPlugin MediaFailed");

            Close();

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

                var mss = await _mediaStreamFacade.CreateMediaStreamSourceAsync(source, deferral.CancellationToken).ConfigureAwait(false);

                mediaLoadingEventArgs.Source = null;
                mediaLoadingEventArgs.MediaStreamSource = mss;

                deferral.Complete();
                deferral = null;
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

            _mediaStreamFacade = MediaStreamFacadeSettings.Parameters.Create();

            _mediaStreamFacade.SetParameter(_httpClientsParameters);
        }

        void Close()
        {
            Debug.WriteLine("StreamingMediaPlugin.Close()");

            _mediaStreamFacade.RequestStop();
        }

        void Cleanup()
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

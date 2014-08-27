﻿// -----------------------------------------------------------------------
//  <copyright file="MediaElementWrapperStreamingMediaPlugin.cs" company="Henric Jungheim">
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
using System.Windows;
using Microsoft.PlayerFramework;
using SM.Media.Utility;
using SM.Media.Web;
using SM.Media.Web.HttpClientReader;

namespace SM.Media.MediaPlayer
{
    public class MediaElementWrapperStreamingMediaPlugin : IMediaPlugin
    {
        static readonly IApplicationInformation ApplicationInformation = ApplicationInformationFactory.Default;
        IHttpClientsParameters _httpClients;
        MediaElementWrapper _mediaElement;

        IHttpClientsParameters HttpClients
        {
            get
            {
                
                if (null == _httpClients)
                {
                    _httpClients = new HttpClientsParameters
                                   {
                                       UserAgent = ApplicationInformation.CreateUserAgent()
                                   };
                }

                return _httpClients;
            }
        }

        #region IMediaPlugin Members

        public void Load()
        {
            Debug.WriteLine("StreamingMediaPlugin.Load()");

            MediaPlayer.MediaClosed += MediaPlayer_MediaClosed;
        }

        public void Update(IMediaSource mediaSource)
        {
            Debug.WriteLine("MediaElementWrapperStreamingMediaPlugin.Update()");
        }

        public void Unload()
        {
            Debug.WriteLine("MediaElementWrapperStreamingMediaPlugin.Unload()");

            MediaPlayer.MediaClosed -= MediaPlayer_MediaClosed;

            if (null != _mediaElement)
            {
                _mediaElement.Cleanup();

                _mediaElement = null;
            }
        }

        public Microsoft.PlayerFramework.MediaPlayer MediaPlayer { get; set; }

        public IMediaElement MediaElement
        {
            get
            {
                Debug.WriteLine("MediaElementWrapperStreamingMediaPlugin MediaElement getter ({0})", null == _mediaElement ? "not cached" : "cached");

                if (null != _mediaElement)
                    return _mediaElement;

                var mediaStreamFacade = MediaStreamFacadeSettings.Parameters.Create();

                mediaStreamFacade.SetParameter(HttpClients);

                _mediaElement = new MediaElementWrapper(mediaStreamFacade);

                return _mediaElement;
            }
        }

        #endregion

        void MediaPlayer_MediaClosed(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("MediaElementWrapperStreamingMediaPlugin MediaClosed");

            if (null == _mediaElement)
                return;

            _mediaElement.Close();
        }
    }
}

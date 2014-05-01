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

using System.Windows;
using Microsoft.PlayerFramework;
using SM.Media.Utility;
using SM.Media.Web;
using SM.Media.Web.HttpClientReader;

namespace SM.Media.MediaPlayer
{
    public class StreamingMediaPlugin : IMediaPlugin
    {
        static readonly IApplicationInformation ApplicationInformation = ApplicationInformationFactory.Default;
        IHttpClients _httpClients;
        MediaElementWrapper _mediaElement;

        IHttpClients HttpClients
        {
            get
            {
                if (null == _httpClients)
                    _httpClients = new HttpClients(userAgent: ApplicationInformation.CreateUserAgent());

                return _httpClients;
            }
        }

        #region IMediaPlugin Members

        public void Load()
        {
            MediaPlayer.MediaClosed += MediaPlayer_MediaClosed;
        }

        public void Update(IMediaSource mediaSource)
        { }

        public void Unload()
        {
            MediaPlayer.MediaClosed -= MediaPlayer_MediaClosed;

            if (null != _mediaElement)
            {
                _mediaElement.Cleanup();

                _mediaElement = null;
            }

            if (null != _httpClients)
            {
                _httpClients.Dispose();

                _httpClients = null;
            }
        }

        public Microsoft.PlayerFramework.MediaPlayer MediaPlayer { get; set; }

        public IMediaElement MediaElement
        {
            get
            {
                if (null != _mediaElement)
                    return _mediaElement;

                _mediaElement = new MediaElementWrapper(MediaStreamFacadeSettings.Parameters.Create(HttpClients));

                return _mediaElement;
            }
        }

        #endregion

        void MediaPlayer_MediaClosed(object sender, RoutedEventArgs e)
        {
            if (null == _mediaElement)
                return;

            _mediaElement.Close();
        }
    }
}

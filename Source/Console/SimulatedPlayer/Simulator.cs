// -----------------------------------------------------------------------
//  <copyright file="Simulator.cs" company="Henric Jungheim">
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
using System.Threading;
using SM.Media;
using SM.Media.Web;

namespace SimulatedPlayer
{
    sealed class Simulator : IDisposable
    {
        readonly IHttpClients _httpClients;
        MediaStreamFascade _mediaStreamFascade;
        MediaStreamFascadeParameters _mediaStreamFascadeParameters;

        public Simulator(IHttpClients httpClients)
        {
            if (httpClients == null)
                throw new ArgumentNullException("httpClients");

            _httpClients = httpClients;
        }

        #region IDisposable Members

        public void Dispose()
        {
            using (_mediaStreamFascade)
            { }
        }

        #endregion

        public void Start()
        {
            var mediaElementManager = new SimulatedMediaElementManager();

            _mediaStreamFascadeParameters = new MediaStreamFascadeParameters(_httpClients, () => new SimulatedMediaStreamSource(mediaElementManager));

            _mediaStreamFascade = new MediaStreamFascade(_mediaStreamFascadeParameters, ((IMediaElementManager)mediaElementManager).SetSourceAsync);

            _mediaStreamFascade.Source = new Uri(
                //"http://www.nasa.gov/multimedia/nasatv/NTV-Public-IPS.m3u8"
                "http://devimages.apple.com/iphone/samples/bipbop/bipbopall.m3u8"
                );

            Thread.Sleep(750);

            mediaElementManager.Play();
        }
    }
}

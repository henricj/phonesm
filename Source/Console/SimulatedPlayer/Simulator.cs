﻿// -----------------------------------------------------------------------
//  <copyright file="Simulator.cs" company="Henric Jungheim">
//  Copyright (c) 2012-2016.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012-2016 Henric Jungheim <software@henric.org>
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
using System.Threading;
using System.Threading.Tasks;
using SM.Media;
using SM.Media.Simulator;
using SM.Media.Utility;
using SM.Media.Web.HttpClientReader;

namespace SimulatedPlayer
{
    sealed class Simulator : IDisposable
    {
        static readonly string[] Sources =
        {
            "http://devimages.apple.com/iphone/samples/bipbop/bipbopall.m3u8",
            "http://www.npr.org/streams/mp3/nprlive24.pls",
            "http://iphone-streaming.ustream.tv/uhls/6540154/streams/live/iphone/playlist.m3u8",
            null,
            "https://devimages.apple.com.edgekey.net/streaming/examples/bipbop_16x9/bipbop_16x9_variant.m3u8"
        };

        readonly IHttpClientFactoryParameters _httpClientFactoryParameters;
        int _count;
        IMediaStreamFacade _mediaStreamFacade;

        public Simulator(IHttpClientFactoryParameters httpClientFactoryParameters)
        {
            if (httpClientFactoryParameters == null)
                throw new ArgumentNullException("httpClientFactoryParameters");

            _httpClientFactoryParameters = httpClientFactoryParameters;
        }

        #region IDisposable Members

        public void Dispose()
        {
            using (_mediaStreamFacade)
            { }
        }

        #endregion

        public async Task StartAsync()
        {
            var mediaElementManager = new SimulatedMediaElementManager();

            _mediaStreamFacade = new MediaStreamFacade();

            _mediaStreamFacade.SetParameter(_httpClientFactoryParameters);

            _mediaStreamFacade.SetParameter(new SimulatedMediaStreamConfigurator(mediaElementManager));

            var source = new Uri(Sources[0]);

            var mss = await _mediaStreamFacade.CreateMediaStreamSourceAsync(source, CancellationToken.None).ConfigureAwait(false);

            if (null == mss)
            {
                Debug.WriteLine("Unable to create media stream source");
                return;
            }

            mediaElementManager.SetSource(mss);

            Thread.Sleep(750);

            mediaElementManager.Play();

            return;
#pragma warning disable 162
            var timer = new Timer(_ =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                var gcMemory = GC.GetTotalMemory(true).BytesToMiB();

                var source2 = Sources[_count];

                Debug.WriteLine("Switching to {0} (GC {1:F3} MiB)", source, gcMemory);

                var url = null == source ? null : new Uri(source2);

                if (++_count >= Sources.Length)
                    _count = 0;
            });

            timer.Change(TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));
#pragma warning restore 162
        }
    }
}

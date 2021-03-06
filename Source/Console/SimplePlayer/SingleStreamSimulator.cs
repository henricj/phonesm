﻿// -----------------------------------------------------------------------
//  <copyright file="SingleStreamSimulator.cs" company="Henric Jungheim">
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
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using SM.Media;
using SM.Media.Builder;
using SM.Media.MediaManager;
using SM.Media.Simulator;
using SM.Media.Utility;

namespace SimplePlayer
{
    public sealed class SingleStreamSimulator : IDisposable
    {
        CancellationTokenSource _cancellationTokenSource;

        MediaStreamFacade _mediaStreamFacade;

        #region IDisposable Members

        public void Dispose()
        {
            _cancellationTokenSource.CancelDisposeSafe();
        }

        #endregion

        public async Task StartAsync(Uri source, CancellationToken cancellationToken)
        {
            // ReSharper disable once PossiblyMistakenUseOfParamsMethod
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _mediaStreamFacade = new MediaStreamFacade();

            var builder = _mediaStreamFacade.Builder as BuilderBase;

            if (null == builder)
                throw new Exception("Unsupported builder type");

            var containerBuilder = builder.ContainerBuilder;

            containerBuilder.RegisterType<SingleStreamMediaManager>().As<IMediaManager>().InstancePerMatchingLifetimeScope("builder-scope");

            var mediaElementManager = new SimulatedMediaElementManager();

            _mediaStreamFacade.SetParameter(new SimulatedMediaStreamConfigurator(mediaElementManager));

            var mss = await _mediaStreamFacade.CreateMediaStreamSourceAsync(source, _cancellationTokenSource.Token).ConfigureAwait(false);

            if (null == mss)
            {
                Debug.WriteLine("Unable to create media stream source");
                return;
            }

            mediaElementManager.SetSource(mss);

            mediaElementManager.Play();
        }
    }
}

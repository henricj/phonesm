// -----------------------------------------------------------------------
//  <copyright file="HlsModule.cs" company="Henric Jungheim">
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
using Ninject;
using Ninject.Modules;
using SM.Media.Hls;
using SM.Media.Segments;

namespace SM.Media
{
    public class HlsModule : NinjectModule
    {
        public override void Load()
        {
            Bind<ISegmentManagerFactoryInstance>().To<HlsPlaylistSegmentManagerFactory>().InSingletonScope();

            Bind<Func<HlsProgramManager>>()
                .ToMethod(ctx => () => ctx.Kernel.Get<HlsProgramManager>());
            Bind<IHlsProgramStreamFactory>().To<HlsProgramStreamFactory>().InSingletonScope();
            Bind<IHlsSegmentsFactory>().To<HlsSegmentsFactory>();
            Bind<IHlsStreamSegments>().To<HlsStreamSegments>();
            Bind<IHlsStreamSegmentsFactory>().To<HlsStreamSegmentsFactory>().InSingletonScope();

            Bind<IHlsPlaylistSegmentManagerPolicy>().To<HlsPlaylistSegmentManagerPolicy>().InSingletonScope();
        }
    }
}

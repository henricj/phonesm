// -----------------------------------------------------------------------
//  <copyright file="SmMediaModule.cs" company="Henric Jungheim">
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
using Ninject;
using Ninject.Activation;
using Ninject.Modules;
using Ninject.Parameters;
using SM.Media.AAC;
using SM.Media.Ac3;
using SM.Media.Buffering;
using SM.Media.Builder;
using SM.Media.Content;
using SM.Media.MediaParser;
using SM.Media.MP3;
using SM.Media.Playlists;
using SM.Media.Pls;
using SM.Media.Segments;
using SM.Media.Utility;
using SM.Media.Web;
using SM.TsParser.Utility;

namespace SM.Media
{
    class SmMediaModule : NinjectModule, IBuilderScopeModule
    {
        #region IBuilderScopeModule Members

        public Func<IContext, object> Scope { get; set; }

        #endregion

        public override void Load()
        {
            var scope = Scope;

            Bind<IMediaManager>().To<TsMediaManager>().InScope(scope);

            Bind<IHttpHeaderReader>().To<HttpHeaderReader>().InSingletonScope();
            Bind<IContentTypeDetector>().ToConstant(new ContentTypeDetector(ContentTypes.AllTypes));
            Bind<IWebContentTypeDetector>().To<WebContentTypeDetector>().InSingletonScope();

            Bind<ISegmentManagerFactoryFinder>().To<SegmentManagerFactoryFinder>().InSingletonScope();
            Bind<ISegmentManagerFactory>().To<SegmentManagerFactory>().InSingletonScope();
            Bind<IMediaElementManager>().To<NullMediaElementManager>().InSingletonScope();

            Bind<ITsPesPacketPool>().To<TsPesPacketPool>().InScope(scope);
            Bind<IBufferPoolParameters>().To<DefaultBufferPoolParameters>().InSingletonScope();
            Bind<IBufferPool>().To<BufferPool>().InScope(scope);
            Bind<Func<IBufferPool>>().ToMethod(ctx => () => ctx.Kernel.Get<IBufferPool>());

            Bind<IBufferingManagerFactory>().To<BufferingManagerFactory>().InScope(scope);

            Bind<IWebCacheFactory>().To<WebCacheFactory>().InSingletonScope();

            Bind<ISegmentManagerFactoryInstance>().To<SimpleSegmentManagerFactory>().InSingletonScope();
            Bind<ISegmentManagerFactoryInstance>().To<PlaylistSegmentManagerFactory>().InSingletonScope();
            Bind<ISegmentManagerFactoryInstance>().To<PlsSegmentManagerFactory>().InSingletonScope();

            Bind<IMediaParserFactoryFinder>().To<MediaParserFactoryFinder>().InSingletonScope();
            Bind<IMediaParserFactory>().To<MediaParserFactory>().InSingletonScope();

            Bind<IMediaParserFactoryInstance>().To<AacMediaParserFactory>().InSingletonScope();
            Bind<IMediaParserFactoryInstance>().To<Ac3MediaParserFactory>().InSingletonScope();
            Bind<IMediaParserFactoryInstance>().To<Mp3MediaParserFactory>().InSingletonScope();

            //Bind<MediaParserFactoryBase<AacMediaParser>.FactoryDelegate>()
            //    .ToMethod(ctx =>
            //        (bufferingManager, checkForSamples) =>
            //            ctx.Kernel.Get<AacMediaParser>(new ConstructorArgument("bufferingManager", bufferingManager), new ConstructorArgument("checkForSamples", checkForSamples)))
            //    .InSingletonScope();

            Bind<IMediaManagerParameters>().To<MediaManagerParameters>().InSingletonScope();
            Bind<IPlaylistSegmentManagerParameters>().To<PlaylistSegmentManagerParameters>().InSingletonScope();
            Bind<IBufferingPolicy>().To<DefaultBufferingPolicy>();
        }
    }
}
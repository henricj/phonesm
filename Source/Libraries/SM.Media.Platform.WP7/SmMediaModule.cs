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
using System.Net.Http;
using Ninject;
using Ninject.Activation;
using Ninject.Modules;
using SM.Media.AAC;
using SM.Media.Ac3;
using SM.Media.Buffering;
using SM.Media.Builder;
using SM.Media.Content;
using SM.Media.H262;
using SM.Media.H264;
using SM.Media.Hls;
using SM.Media.MediaParser;
using SM.Media.MP3;
using SM.Media.Pes;
using SM.Media.Pls;
using SM.Media.Segments;
using SM.Media.Utility;
using SM.Media.Web;
using SM.Media.Web.HttpClientReader;
using SM.TsParser;
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

            Bind<IContentTypeDetector>().ToConstant(new ContentTypeDetector(ContentTypes.AllTypes));

            Bind<ISegmentManagerFactoryFinder>().To<SegmentManagerFactoryFinder>().InSingletonScope();
            Bind<ISegmentManagerFactory>().To<SegmentManagerFactory>().InSingletonScope();
            Bind<ISegmentReaderManagerFactory>().To<SegmentReaderManagerFactory>().InSingletonScope();

            Bind<ITsPesPacketPool>().To<TsPesPacketPool>().InScope(scope);
            Bind<IBufferPool>().To<BufferPool>().InScope(scope);
            Bind<IBufferPoolParameters>().To<DefaultBufferPoolParameters>().InSingletonScope();
            Bind<Func<IBufferPool>>().ToMethod(ctx => () => ctx.Kernel.Get<IBufferPool>());

            Bind<IWebReaderManager>().To<HttpClientWebReaderManager>().InSingletonScope();

            Bind<ISegmentManagerFactoryInstance>().To<SimpleSegmentManagerFactory>().InSingletonScope();
            Bind<ISegmentManagerFactoryInstance>().To<HlsPlaylistSegmentManagerFactory>().InSingletonScope();
            Bind<ISegmentManagerFactoryInstance>().To<PlsSegmentManagerFactory>().InSingletonScope();

            Bind<IMediaParserFactoryFinder>().To<MediaParserFactoryFinder>().InSingletonScope();
            Bind<IMediaParserFactory>().To<MediaParserFactory>().InSingletonScope();

            Bind<IMediaParserFactoryInstance>().To<AacMediaParserFactory>().InSingletonScope();
            Bind<IMediaParserFactoryInstance>().To<Ac3MediaParserFactory>().InSingletonScope();
            Bind<IMediaParserFactoryInstance>().To<Mp3MediaParserFactory>().InSingletonScope();
            Bind<IMediaParserFactoryInstance>().To<TsMediaParserFactory>().InSingletonScope();

            Bind<Func<AacMediaParser>>().ToMethod(ctx => () => ctx.Kernel.Get<AacMediaParser>());
            Bind<Func<Ac3MediaParser>>().ToMethod(ctx => () => ctx.Kernel.Get<Ac3MediaParser>());
            Bind<Func<Mp3MediaParser>>().ToMethod(ctx => () => ctx.Kernel.Get<Mp3MediaParser>());
            Bind<Func<TsMediaParser>>().ToMethod(ctx => () => ctx.Kernel.Get<TsMediaParser>());

            Bind<IPesStreamFactoryInstance>().To<AacStreamHandlerFactory>().InScope(scope);
            Bind<IPesStreamFactoryInstance>().To<Ac3StreamHandlerFactory>().InScope(scope);
            Bind<IPesStreamFactoryInstance>().To<H262StreamHandlerFactory>().InScope(scope);
            Bind<IPesStreamFactoryInstance>().To<H264StreamHandlerFactory>().InScope(scope);
            Bind<IPesStreamFactoryInstance>().To<Mp3StreamHandlerFactory>().InScope(scope);

            Bind<IPesHandlerFactory>().To<PesHandlerFactory>().InSingletonScope();

            Bind<Func<PesStreamParameters>>().ToMethod(ctx => () => ctx.Kernel.Get<PesStreamParameters>());

            Bind<ITsDecoder>().To<TsDecoder>();
            Bind<ITsTimestamp>().To<TsTimestamp>();
            Bind<IPesHandlers>().To<PesHandlers>();

            Bind<IMediaManagerParameters>().To<MediaManagerParameters>().InSingletonScope();
            Bind<IHlsPlaylistSegmentManagerPolicy>().To<HlsPlaylistSegmentManagerPolicy>().InSingletonScope();
            Bind<IBufferingPolicy>().To<DefaultBufferingPolicy>();
            Bind<IBufferingManager>().To<BufferingManager>();
            Bind<Func<IBufferingManager>>().ToMethod(ctx => () => ctx.Kernel.Get<IBufferingManager>());

            Bind<IRetryManager>().To<RetryManager>().InSingletonScope();

            Bind<IHttpClients>().To<HttpClients>().InSingletonScope();
            Bind<IHttpClientsParameters>().To<HttpClientsParameters>().InSingletonScope();

            Bind<Func<HttpClientHandler>>().ToMethod(ctx => () => ctx.Kernel.Get<HttpClientHandler>());
        }
    }
}

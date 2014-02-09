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
using SM.Media.Buffering;
using SM.Media.Builder;
using SM.Media.Content;
using SM.Media.Playlists;
using SM.Media.Pls;
using SM.Media.Segments;
using SM.Media.Web;

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

            var b = Bind<IMediaManager>().To<TsMediaManager>();

            if (null != scope)
                b.InScope(scope);

            Bind<IHttpHeaderReader>().To<HttpHeaderReader>().InSingletonScope();
            Bind<IContentTypeDetector>().ToConstant(new ContentTypeDetector(ContentTypes.AllTypes));
            Bind<IWebContentTypeDetector>().To<WebContentTypeDetector>().InSingletonScope();
            Bind<ISegmentManagerFactoryFinder>().To<SegmentManagerFactoryFinder>().InSingletonScope();
            Bind<ISegmentManagerFactory>().To<SegmentManagerFactory>().InSingletonScope();
            Bind<IMediaElementManager>().To<NullMediaElementManager>().InSingletonScope();
            Bind<MediaManagerParameters.BufferingManagerFactoryDelegate>().ToMethod(
                ctx =>
                {
                    var bufferingPolicy = ctx.Kernel.Get<IBufferingPolicy>();

                    return (readers, queueThrottling, reportBufferingChange) =>
                        new BufferingManager(queueThrottling, reportBufferingChange, bufferingPolicy);
                });
            Bind<Func<Uri, IWebCache>>().ToMethod(ctx => url => ctx.Kernel.Get<WebCache>(new ConstructorArgument("url", url, false)));

            Bind<ISegmentManagerFactoryInstance>().To<SimpleSegmentManagerFactory>();
            Bind<ISegmentManagerFactoryInstance>().To<PlaylistSegmentManagerFactory>();
            Bind<ISegmentManagerFactoryInstance>().To<PlsSegmentManagerFactory>();

            Bind<IMediaManagerParameters>().To<MediaManagerParameters>().InSingletonScope();
            Bind<IPlaylistSegmentManagerParameters>().To<PlaylistSegmentManagerParameters>().InSingletonScope();
            Bind<IBufferingPolicy>().To<DefaultBufferingPolicy>();
        }
    }
}

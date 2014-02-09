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

using Autofac;
using SM.Media.Buffering;
using SM.Media.Content;
using SM.Media.Playlists;
using SM.Media.Pls;
using SM.Media.Segments;
using SM.Media.Web;

namespace SM.Media
{
    public class SmMediaModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<TsMediaManager>().As<IMediaManager>().InstancePerLifetimeScope();

            builder.RegisterType<HttpHeaderReader>().As<IHttpHeaderReader>().SingleInstance();
            builder.RegisterInstance(new ContentTypeDetector(ContentTypes.AllTypes)).As<IContentTypeDetector>();
            builder.RegisterType<WebContentTypeDetector>().As<IWebContentTypeDetector>().SingleInstance();

            builder.RegisterType<SegmentManagerFactoryFinder>().As<ISegmentManagerFactoryFinder>().SingleInstance();
            builder.RegisterType<SegmentManagerFactory>().As<ISegmentManagerFactory>().SingleInstance();
            builder.RegisterType<NullMediaElementManager>().As<IMediaElementManager>().SingleInstance();

            builder.Register<MediaManagerParameters.BufferingManagerFactoryDelegate>(
                ctx =>
                {
                    var bufferingPolicy = ctx.Resolve<IBufferingPolicy>();

                    return (readers, queueThrottling, reportBufferingChange) =>
                        new BufferingManager(queueThrottling, reportBufferingChange, bufferingPolicy);
                })
                   .SingleInstance();

            builder.RegisterType<WebCacheFactory>().As<IWebCacheFactory>().SingleInstance();

            builder.RegisterType<SimpleSegmentManagerFactory>().As<ISegmentManagerFactoryInstance>().SingleInstance().PreserveExistingDefaults();
            builder.RegisterType<PlaylistSegmentManagerFactory>().As<ISegmentManagerFactoryInstance>().SingleInstance().PreserveExistingDefaults();
            builder.RegisterType<PlsSegmentManagerFactory>().As<ISegmentManagerFactoryInstance>().SingleInstance().PreserveExistingDefaults();

            builder.RegisterType<MediaManagerParameters>().As<IMediaManagerParameters>().SingleInstance();
            builder.RegisterType<PlaylistSegmentManagerParameters>().As<IPlaylistSegmentManagerParameters>().SingleInstance();
            builder.RegisterType<DefaultBufferingPolicy>().As<IBufferingPolicy>().InstancePerLifetimeScope();
        }
    }
}

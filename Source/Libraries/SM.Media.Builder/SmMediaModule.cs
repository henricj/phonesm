// -----------------------------------------------------------------------
//  <copyright file="SmMediaModule.cs" company="Henric Jungheim">
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

using Autofac;
using SM.Media.AAC;
using SM.Media.Ac3;
using SM.Media.Audio.Shoutcast;
using SM.Media.Buffering;
using SM.Media.Content;
using SM.Media.H262;
using SM.Media.H264;
using SM.Media.MediaManager;
using SM.Media.MediaParser;
using SM.Media.Metadata;
using SM.Media.MP3;
using SM.Media.Pes;
using SM.Media.Pls;
using SM.Media.Segments;
using SM.Media.TransportStream;
using SM.Media.TransportStream.TsParser;
using SM.Media.TransportStream.TsParser.Utility;
using SM.Media.Utility;
using SM.Media.Utility.RandomGenerators;
using SM.Media.Utility.TextEncodings;
using SM.Media.Web;

namespace SM.Media
{
    public class SmMediaModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterInstance(new ContentTypeDetector(ContentTypes.AllTypes)).As<IContentTypeDetector>();

            builder.RegisterType<SegmentManagerFactoryFinder>().As<ISegmentManagerFactoryFinder>().SingleInstance();
            builder.RegisterType<SegmentManagerFactory>().As<ISegmentManagerFactory>().SingleInstance();
            builder.RegisterType<SegmentReaderManagerFactory>().As<ISegmentReaderManagerFactory>().SingleInstance();

            builder.RegisterType<TsPesPacketPool>().As<ITsPesPacketPool>().SingleInstance();
            builder.RegisterType<BufferPool>().As<IBufferPool>().SingleInstance();
            builder.RegisterType<DefaultBufferPoolParameters>().As<IBufferPoolParameters>().SingleInstance();

            builder.RegisterType<SimpleSegmentManagerFactory>().As<ISegmentManagerFactoryInstance>().SingleInstance().PreserveExistingDefaults();
            builder.RegisterType<PlsSegmentManagerFactory>().As<ISegmentManagerFactoryInstance>().SingleInstance().PreserveExistingDefaults();

            builder.RegisterType<MediaParserFactoryFinder>().As<IMediaParserFactoryFinder>().SingleInstance();
            builder.RegisterType<MediaParserFactory>().As<IMediaParserFactory>().SingleInstance();
            builder.RegisterType<WebMetadataFactory>().As<IWebMetadataFactory>().SingleInstance();
            builder.RegisterType<MetadataSink>().As<IMetadataSink>();

            builder.RegisterType<Utf8ShoutcastEncodingSelector>().As<IShoutcastEncodingSelector>().SingleInstance();
            builder.RegisterType<ShoutcastMetadataFilterFactory>().As<IShoutcastMetadataFilterFactory>().SingleInstance();

            builder.RegisterType<AacMediaParserFactory>().As<IMediaParserFactoryInstance>().SingleInstance().PreserveExistingDefaults();
            builder.RegisterType<Ac3MediaParserFactory>().As<IMediaParserFactoryInstance>().SingleInstance().PreserveExistingDefaults();
            builder.RegisterType<Mp3MediaParserFactory>().As<IMediaParserFactoryInstance>().SingleInstance().PreserveExistingDefaults();
            builder.RegisterType<TsMediaParserFactory>().As<IMediaParserFactoryInstance>().SingleInstance().PreserveExistingDefaults();

            builder.RegisterType<AacMediaParser>().AsSelf().ExternallyOwned();
            builder.RegisterType<Ac3MediaParser>().AsSelf().ExternallyOwned();
            builder.RegisterType<Mp3MediaParser>().AsSelf().ExternallyOwned();
            builder.RegisterType<TsMediaParser>().AsSelf().ExternallyOwned();

            builder.RegisterType<AacStreamHandlerFactory>().As<IPesStreamFactoryInstance>().InstancePerLifetimeScope().PreserveExistingDefaults();
            builder.RegisterType<Ac3StreamHandlerFactory>().As<IPesStreamFactoryInstance>().InstancePerLifetimeScope().PreserveExistingDefaults();
            builder.RegisterType<H262StreamHandlerFactory>().As<IPesStreamFactoryInstance>().InstancePerLifetimeScope().PreserveExistingDefaults();
            builder.RegisterType<H264StreamHandlerFactory>().As<IPesStreamFactoryInstance>().InstancePerLifetimeScope().PreserveExistingDefaults();
            builder.RegisterType<Mp3StreamHandlerFactory>().As<IPesStreamFactoryInstance>().InstancePerLifetimeScope().PreserveExistingDefaults();

            builder.RegisterType<PesHandlerFactory>().As<IPesHandlerFactory>().SingleInstance();

            builder.RegisterType<PesStreamParameters>().AsSelf();

            builder.RegisterType<TsDecoder>().As<ITsDecoder>();
            builder.RegisterType<TsTimestamp>().As<ITsTimestamp>();
            builder.RegisterType<PesHandlers>().As<IPesHandlers>();

            builder.RegisterType<WebReaderManagerParameters>().As<IWebReaderManagerParameters>().SingleInstance();
            builder.RegisterType<MediaManagerParameters>().As<IMediaManagerParameters>().SingleInstance();
            builder.RegisterType<PlsSegmentManagerPolicy>().As<IPlsSegmentManagerPolicy>().SingleInstance().PreserveExistingDefaults();
            builder.RegisterType<DefaultBufferingPolicy>().As<IBufferingPolicy>().InstancePerMatchingLifetimeScope("builder-scope");

            builder.RegisterType<BufferingManager>().As<IBufferingManager>();

            builder.RegisterType<RetryManager>().As<IRetryManager>().SingleInstance();

            builder.RegisterType<SmEncodings>().As<ISmEncodings>().SingleInstance();

            builder.RegisterType<UserAgent>().As<IUserAgent>().SingleInstance();

            builder.RegisterType<XorShift1024Star>().As<IRandomGenerator>();
            builder.RegisterType<XorShift1024Star>().As<IRandomGenerator<ulong>>();
        }
    }
}

// -----------------------------------------------------------------------
//  <copyright file="TsMediaManagerBuilder.cs" company="Henric Jungheim">
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
using System.Threading.Tasks;
using Autofac;
using Autofac.Core;
using SM.Media.Builder;
using SM.Media.Segments;
using SM.Media.Web;

namespace SM.Media
{
    sealed class TsMediaManagerBuilder : BuilderBase<IMediaManager>
    {
        static readonly IModule[] Modules = { new SmMediaModule() };

        public TsMediaManagerBuilder(IHttpClients httpClients)
            : base(Modules)
        {
            RegisterSingleton(httpClients);

            ContainerBuilder.Register<Func<Task<ISegmentReaderManager>>>(
                ctx =>
                {
                    var segmentManagerFactory = ctx.Resolve<ISegmentManagerFactory>();
                    var clients = ctx.Resolve<IHttpClients>();

                    return async () =>
                                 {
                                     var playlist = await segmentManagerFactory.CreateAsync(new[] { Source }, CancellationToken.None).ConfigureAwait(false);

                                     return new SegmentReaderManager(new[] { playlist }, clients.CreateSegmentClient);
                                 };
                });
        }

        public Uri Source { get; set; }
    }
}
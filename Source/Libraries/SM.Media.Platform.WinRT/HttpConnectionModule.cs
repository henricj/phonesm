// -----------------------------------------------------------------------
//  <copyright file="HttpConnectionModule.cs" company="Henric Jungheim">
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
using SM.Media.Web;
using SM.Media.Web.HttpConnection;
using SM.Media.Web.HttpConnectionReader;

namespace SM.Media
{
    public class HttpConnectionModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<StreamSocketWrapper>().As<ISocket>().ExternallyOwned();

            builder.RegisterType<HttpConnection>().As<IHttpConnection>().ExternallyOwned();
            builder.RegisterType<HttpConnectionFactory>().As<IHttpConnectionFactory>().SingleInstance();
            builder.RegisterType<HttpConnectionRequestFactory>().As<IHttpConnectionRequestFactory>().SingleInstance();
            builder.RegisterType<HttpConnectionRequestFactoryParameters>().As<IHttpConnectionRequestFactoryParameters>().SingleInstance();

            builder.RegisterType<HttpConnectionWebReaderManager>().As<IWebReaderManager>().SingleInstance();

            builder.RegisterType<HttpEncoding>().As<IHttpEncoding>().SingleInstance();
            builder.RegisterType<HttpHeaderSerializer>().As<IHttpHeaderSerializer>().SingleInstance();
            builder.RegisterType<UserAgentEncoder>().As<IUserAgentEncoder>().SingleInstance();
        }
    }
}

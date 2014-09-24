using System;
using System.Net.Http;
using Ninject;
using Ninject.Modules;
using SM.Media.Web;
using SM.Media.Web.HttpClientReader;

namespace SM.Media
{
    class HttpClientModule : NinjectModule
    {
        public override void Load()
        {
            Bind<IWebReaderManager>().To<HttpClientWebReaderManager>().InSingletonScope();

            Bind<IHttpClients>().To<HttpClients>().InSingletonScope();
            Bind<IHttpClientsParameters>().To<HttpClientsParameters>().InSingletonScope();
            Bind<IProductInfoHeaderValueFactory>().To<ProductInfoHeaderValueFactory>().InSingletonScope();

            Bind<Func<HttpClientHandler>>().ToMethod(ctx => () => ctx.Kernel.Get<HttpClientHandler>());
        }
    }
}
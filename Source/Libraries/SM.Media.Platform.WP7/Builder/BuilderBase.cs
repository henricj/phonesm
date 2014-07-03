// -----------------------------------------------------------------------
//  <copyright file="BuilderBase.cs" company="Henric Jungheim">
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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Ninject;
using Ninject.Activation;
using Ninject.Modules;
using SM.Media.Utility;

namespace SM.Media.Builder
{
    public interface IBuilderScopeModule
    {
        Func<IContext, object> Scope { get; set; }
    }

    public abstract class BuilderBase : IBuilder
    {
        IKernel _kernel;

        public IKernel Kernel
        {
            get
            {
                var kernel = _kernel;

                if (null == kernel)
                {
                    var newKernel = CreateKernel();

                    kernel = Interlocked.CompareExchange(ref _kernel, newKernel, null);

                    if (null == kernel)
                        kernel = newKernel;
                    else
                        newKernel.DisposeSafe();
                }

                return kernel;
            }
        }

        #region IBuilder Members

        public void Register<TService, TImplementation>()
            where TImplementation : TService
        {
            ThrowIfBusy();

            Kernel.Rebind<TService>().To<TImplementation>();
        }

        public void RegisterSingleton<TService, TImplementation>()
            where TImplementation : TService
        {
            ThrowIfBusy();

            Kernel.Rebind<TService>().To<TImplementation>().InSingletonScope();
        }

        public void RegisterSingleton<TService>(TService instance)
            where TService : class
        {
            ThrowIfBusy();

            Kernel.Rebind<TService>().ToConstant(instance);
        }

        public void RegisterSingletonFactory<TService>(Func<TService> factory)
        {
            ThrowIfBusy();

            Kernel.Rebind<TService>().ToMethod(ctx => factory()).InSingletonScope();
        }

        public void RegisterTransientFactory<TService>(Func<TService> factory)
        {
            ThrowIfBusy();

            Kernel.Rebind<TService>().ToMethod(ctx => factory());
        }

        public void Dispose()
        {
            var kernel = Interlocked.Exchange(ref _kernel, null);

            if (null == kernel)
                return;

            kernel.DisposeBackground("IoC Dispose");
        }

        #endregion

        protected abstract IKernel CreateKernel();

        protected abstract void ThrowIfBusy();
    }

    public class BuilderBase<TBuild> : BuilderBase, IBuilder<TBuild>
    {
        readonly INinjectModule[] _modules;
        IBuilderHandle<TBuild> _handle;

        protected BuilderBase(params INinjectModule[] modules)
        {
            if (null != modules && modules.Length > 0)
                _modules = modules;
        }

        #region IBuilder<TBuild> Members

        public TBuild Create()
        {
            if (null != _handle)
                throw new InvalidOperationException("The builder is in use");

            var handle = new BuilderHandle<TBuild>(this);

            if (null != Interlocked.CompareExchange(ref _handle, handle, null))
            {
                handle.DisposeSafe();

                throw new InvalidOperationException("The builder is in use");
            }

            return handle.Instance;
        }

        public void Destroy(TBuild instance)
        {
            var handle = Interlocked.Exchange(ref _handle, null);

            if (null == handle)
                throw new InvalidOperationException("No handle");

            if (!ReferenceEquals(instance, handle.Instance))
                throw new InvalidOperationException("Wrong instance");

            handle.DisposeSafe();
        }

        #endregion

        protected override IKernel CreateKernel()
        {
            foreach (var module in _modules.OfType<IBuilderScopeModule>())
                module.Scope = ctx => _handle;

            return new StandardKernel(_modules);
        }

        internal TBuild Get()
        {
            return Kernel.Get<TBuild>();
        }

        internal void Release(TBuild instance)
        {
            var ok = Kernel.Release(instance);

            if (!ok)
                Debug.WriteLine("BuilderBase<>.Release() not released");
        }

        protected override void ThrowIfBusy()
        {
            if (null != _handle)
                throw new InvalidOperationException("Configuration is done");
        }
    }
}

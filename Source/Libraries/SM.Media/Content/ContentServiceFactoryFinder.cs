// -----------------------------------------------------------------------
//  <copyright file="ContentServiceFactoryFinder.cs" company="Henric Jungheim">
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
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SM.Media.Content
{
    public interface IContentServiceFactoryFinder<TService, TParameter>
    {
        IContentServiceFactoryInstance<TService, TParameter> GetFactory(ContentType contentType);
        void Register(ContentType contentType, IContentServiceFactoryInstance<TService, TParameter> factory);
        void Deregister(ContentType contentType);
    }

    public class ContentServiceFactoryFinder<TService, TParameter> : IContentServiceFactoryFinder<TService, TParameter>
    {
        volatile Dictionary<ContentType, IContentServiceFactoryInstance<TService, TParameter>> _factories;

        public ContentServiceFactoryFinder(IEnumerable<IContentServiceFactoryInstance<TService, TParameter>> factoryInstances)
        {
            _factories = factoryInstances
                .SelectMany(fi => fi.KnownContentTypes,
                    (fi, contentType) => new
                                         {
                                             ContentType = contentType,
                                             Instance = fi
                                         })
                .ToDictionary(v => v.ContentType, v => v.Instance);
        }

        #region IContentServiceFactoryFinder<TService,TParameter> Members

        public IContentServiceFactoryInstance<TService, TParameter> GetFactory(ContentType contentType)
        {
            IContentServiceFactoryInstance<TService, TParameter> factory;

            if (_factories.TryGetValue(contentType, out factory))
                return factory;

            return null;
        }

        public void Register(ContentType contentType, IContentServiceFactoryInstance<TService, TParameter> factory)
        {
            SafeChangeFactories(factories => factories[contentType] = factory);
        }

        public void Deregister(ContentType contentType)
        {
            SafeChangeFactories(factories => factories.Remove(contentType));
        }

        #endregion

        void SafeChangeFactories(Action<Dictionary<ContentType, IContentServiceFactoryInstance<TService, TParameter>>> changeAction)
        {
            var oldFactories = _factories;

            for (; ; )
            {
                var newFactories = new Dictionary<ContentType, IContentServiceFactoryInstance<TService, TParameter>>(oldFactories);

                changeAction(newFactories);

#pragma warning disable 420
                var factories = Interlocked.CompareExchange(ref _factories, newFactories, oldFactories);
#pragma warning restore 420

                if (oldFactories == factories)
                    return;

                oldFactories = factories;
            }
        }
    }
}

// -----------------------------------------------------------------------
//  <copyright file="TsDescriptorFactory.cs" company="Henric Jungheim">
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

using System.Collections.Generic;
using System.Linq;

namespace SM.Media.TransportStream.TsParser.Descriptor
{
    public interface ITsDescriptorFactory
    {
        TsDescriptor Create(byte code, byte[] buffer, int offset, int length);
    }

    public class TsDescriptorFactory : ITsDescriptorFactory
    {
        readonly ITsDescriptorFactoryInstance[] _factories;

        public TsDescriptorFactory(IEnumerable<ITsDescriptorFactoryInstance> factories)
        {
            var allFactories = factories.OrderBy(f => f.Type.Code).ToArray();

            var maxIndex = allFactories.Max(f => f.Type.Code);

            _factories = new ITsDescriptorFactoryInstance[maxIndex + 1];

            foreach (var factory in allFactories)
                _factories[factory.Type.Code] = factory;
        }

        #region ITsDescriptorFactory Members

        public TsDescriptor Create(byte code, byte[] buffer, int offset, int length)
        {
            if (code >= _factories.Length)
                return null;

            var factory = _factories[code];

            if (null == factory)
                return null;

            return factory.Create(buffer, offset, length);
        }

        #endregion
    }
}

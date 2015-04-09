// -----------------------------------------------------------------------
//  <copyright file="GeneratorStreamSourceFactoryBase.cs" company="Henric Jungheim">
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

using System;

namespace SM.Media.Audio.Generator
{
    public class GeneratorStreamSourceFactoryBase<TMediaStreamSource> : IStreamSourceFactory<TMediaStreamSource>
        where TMediaStreamSource : class
    {
        readonly Func<IAudioStreamSourceParameters, Action<ulong, byte[]>> _generatorFactory;
        readonly IGeneratorStreamSourceFactory<TMediaStreamSource> _generatorStreamSourceFactory;

        public GeneratorStreamSourceFactoryBase(IGeneratorStreamSourceFactory<TMediaStreamSource> generatorStreamSourceFactory,
            Func<IAudioStreamSourceParameters, Action<ulong, byte[]>> generatorFactory)
        {
            if (null == generatorStreamSourceFactory)
                throw new ArgumentNullException("generatorStreamSourceFactory");
            if (null == generatorFactory)
                throw new ArgumentNullException("generatorFactory");

            _generatorStreamSourceFactory = generatorStreamSourceFactory;
            _generatorFactory = generatorFactory;
        }

        #region IStreamSourceFactory<TMediaStreamSource> Members

        public TMediaStreamSource CreateSource(IAudioStreamSourceParameters parameters)
        {
            var generator = _generatorFactory(parameters);

            return _generatorStreamSourceFactory
                .CreateFactory(generator)
                .CreateSource(parameters);
        }

        #endregion
    }
}
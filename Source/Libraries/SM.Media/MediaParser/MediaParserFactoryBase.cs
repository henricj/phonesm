// -----------------------------------------------------------------------
//  <copyright file="MediaParserFactoryBase.cs" company="Henric Jungheim">
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
using System.Threading;
using System.Threading.Tasks;
using SM.Media.Content;

namespace SM.Media.MediaParser
{
    public abstract class MediaParserFactoryBase<TMediaParser> : IMediaParserFactoryInstance
        where TMediaParser : IMediaParser
    {
        readonly Func<TMediaParser> _parserFactory;

        protected MediaParserFactoryBase(Func<TMediaParser> parserFactory)
        {
            if (null == parserFactory)
                throw new ArgumentNullException("parserFactory");

            _parserFactory = parserFactory;
        }

        #region IMediaParserFactoryInstance Members

        public abstract ICollection<ContentType> KnownContentTypes { get; }

        public Task<IMediaParser> CreateAsync(IMediaParserParameters parameter, ContentType contentType, CancellationToken cancellationToken)
        {
            var mediaParser = _parserFactory();

            return TaskEx.FromResult<IMediaParser>(mediaParser);
        }

        #endregion
    }
}

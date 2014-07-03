// -----------------------------------------------------------------------
//  <copyright file="MediaStreamFacade.cs" company="Henric Jungheim">
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
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using SM.Media.Builder;

namespace SM.Media
{
    public interface IMediaStreamFacade : IMediaStreamFacadeBase<MediaStreamSource>
    { }

    public class MediaStreamFacade : MediaStreamFacadeBase, IMediaStreamFacade
    {
        public MediaStreamFacade()
            : base(CreateBuilder())
        { }

        #region IMediaStreamFacade Members

        public async Task<MediaStreamSource> CreateMediaStreamSourceAsync(Uri source, CancellationToken cancellationToken)
        {
            if (null == source)
                return null;

            Exception exception;

            try
            {
                var mediaManager = await CreateMediaManagerAsync(source, cancellationToken).ConfigureAwait(false);

                return (MediaStreamSource)mediaManager.MediaStreamSource;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MediaStreamFacade.CreateAsync() failed: " + ex.Message);

                exception = new AggregateException(ex.Message, ex);
            }

            await CloseAsync().ConfigureAwait(false);

            throw exception;
        }

        #endregion

        static IBuilder<IMediaManager> CreateBuilder()
        {
            var builder = new TsMediaManagerBuilder();

            return builder;
        }
    }
}

// -----------------------------------------------------------------------
//  <copyright file="WebReaderExtensions.cs" company="Henric Jungheim">
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
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using SM.Media.Content;
using SM.Media.Utility;

namespace SM.Media.Web
{
    public static class WebReaderExtensions
    {
        public static IWebReader CreateChild(this IWebReader webReader, Uri url, ContentKind contentKind, ContentType contentType = null)
        {
            return webReader.Manager.CreateReader(url, contentKind, webReader, contentType);
        }

        public static IWebCache CreateWebCache(this IWebReader webReader, Uri url, ContentKind contentKind, ContentType contentType = null)
        {
            return webReader.Manager.CreateWebCache(url, contentKind, webReader, contentType);
        }

        public static Task<ContentType> DetectContentTypeAsync(this IWebReader webReader, Uri url, ContentKind contentKind, CancellationToken cancellationToken)
        {
            return webReader.Manager.DetectContentTypeAsync(url, contentKind, cancellationToken, webReader);
        }

        public static async Task<TReturn> ReadStreamAsync<TReturn>(this IWebReader webReader, Uri url, IRetry retry,
            Func<Uri, Stream, TReturn> reader, CancellationToken cancellationToken)
        {
            for (; ; )
            {
                using (var response = await webReader.GetWebStreamAsync(url, true, cancellationToken))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        var actualUrl = response.ActualUrl;

                        using (var stream = await response.GetStreamAsync(cancellationToken).ConfigureAwait(false))
                        {
                            return reader(actualUrl, stream);
                        }
                    }

                    if (!RetryPolicy.IsRetryable((HttpStatusCode)response.HttpStatusCode))
                        response.EnsureSuccessStatusCode();

                    var canRetry = await retry.CanRetryAfterDelayAsync(cancellationToken)
                                              .ConfigureAwait(false);

                    if (!canRetry)
                        response.EnsureSuccessStatusCode();
                }
            }
        }

        public static async Task<TReturn> ReadStreamAsync<TReturn>(this IWebReader webReader, Uri url, Retry retry,
            Func<Uri, Stream, CancellationToken, Task<TReturn>> reader, CancellationToken cancellationToken)
        {
            for (; ; )
            {
                using (var response = await webReader.GetWebStreamAsync(url, false, cancellationToken))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        var actualUrl = response.ActualUrl;

                        using (var stream = await response.GetStreamAsync(cancellationToken).ConfigureAwait(false))
                        {
                            return await reader(actualUrl, stream, cancellationToken).ConfigureAwait(false);
                        }
                    }

                    if (!RetryPolicy.IsRetryable((HttpStatusCode)response.HttpStatusCode))
                        response.EnsureSuccessStatusCode();

                    var canRetry = await retry.CanRetryAfterDelayAsync(cancellationToken)
                                              .ConfigureAwait(false);

                    if (!canRetry)
                        response.EnsureSuccessStatusCode();
                }
            }
        }
    }
}

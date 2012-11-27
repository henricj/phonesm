// -----------------------------------------------------------------------
//  <copyright file="M3U8ParserAsyncExtensions.cs" company="Henric Jungheim">
//  Copyright (c) 2012.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012 Henric Jungheim <software@henric.org>
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

namespace SM.Media.M3U8
{
    public static class M3U8ParserAsyncExtensions
    {
        static bool IsRetryableException(Exception ex)
        {
            // TODO: We should probably turn this around to only retry known exceptions (like timeouts, name lookup failures, etc).
            // TODO: There should be a policy object somewhere that implements this filter.  What is appropriate for streaming from one
            // website might not be appropriate for another.


            if (ex is OperationCanceledException)
                return false;

            var webException = ex as WebException;
            if (null == webException)
                return true;

            var httpResponse = webException.Response as HttpWebResponse;
            if (null == httpResponse)
                return true;

            if (httpResponse.StatusCode == HttpStatusCode.NotFound)
                return false;

            return true;
        }

        public static async Task ParseAsync(this M3U8Parser parser, Uri playlist, CancellationToken cancellationToken)
        {
            var playlistString = await new Retry(4, 100, IsRetryableException)
                                           .CallAsync(async () => await new WebClient().DownloadStringTaskAsync(playlist))
                                           .WithCancellation(cancellationToken);

            using (var sr = new StringReader(playlistString))
            {
                parser.Parse(sr);
            }
        }
    }
}
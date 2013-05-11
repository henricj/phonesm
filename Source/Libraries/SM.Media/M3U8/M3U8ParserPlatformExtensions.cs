// -----------------------------------------------------------------------
//  <copyright file="M3U8ParserPlatformExtensions.cs" company="Henric Jungheim">
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
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using SM.Media.Utility;

namespace SM.Media.M3U8
{
    public static class M3U8ParserPlatformExtensions
    {
        static readonly MediaTypeWithQualityHeaderValue AcceptMpegurlHeader = new MediaTypeWithQualityHeaderValue("application/vnd.apple.mpegurl");
        static readonly MediaTypeWithQualityHeaderValue AcceptAnyHeader = new MediaTypeWithQualityHeaderValue("*/*");

        public static async Task ParseAsync(this M3U8Parser parser, Uri playlist, CancellationToken cancellationToken)
        {
            var playlistString = await new Retry(4, 100, RetryPolicy.IsWebExceptionRetryable)
                .CallAsync(async () =>
                                 {
                                     using (var httpClient = new HttpClient())
                                     {
                                         httpClient.DefaultRequestHeaders.Accept.Add(AcceptMpegurlHeader);
                                         httpClient.DefaultRequestHeaders.Accept.Add(AcceptAnyHeader);

                                         var response = await httpClient.GetAsync(playlist, HttpCompletionOption.ResponseContentRead, cancellationToken);

                                         response.EnsureSuccessStatusCode();

                                         return await response.Content.ReadAsStringAsync();
                                     }
                                 })
                .WithCancellation(cancellationToken);

            using (var sr = new StringReader(playlistString))
            {
                parser.Parse(playlist, sr);
            }
        }
    }
}
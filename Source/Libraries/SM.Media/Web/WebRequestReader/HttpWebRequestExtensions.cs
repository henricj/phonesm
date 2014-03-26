// -----------------------------------------------------------------------
//  <copyright file="HttpWebRequestExtensions.cs" company="Henric Jungheim">
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

using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace SM.Media.Web.WebRequestReader
{
    public static class HttpWebRequestExtensions
    {
        static readonly byte[] NoData = new byte[0];

        public static async Task<byte[]> ReadAsByteArrayAsync(this HttpWebResponse response, CancellationToken cancellationToken)
        {
            var contentLength = response.ContentLength;

            if (0 == contentLength)
                return NoData;

            if (contentLength > 2 * 1024 * 1024)
                throw new WebException("Too much data for GetByteArrayAsync: " + contentLength);

            using (var buffer = contentLength > 0 ? new MemoryStream((int)contentLength) : new MemoryStream())
            {
                using (var stream = response.GetResponseStream())
                {
                    await stream.CopyToAsync(buffer, 4096, cancellationToken).ConfigureAwait(false);
                }

                return buffer.ToArray();
            }
        }

        public static async Task<HttpWebResponse> SendAsync(this HttpWebRequest request, CancellationToken cancellationToken)
        {
            var task = Task<System.Net.WebResponse>.Factory.FromAsync(request.BeginGetResponse, request.EndGetResponse, null);

            using (cancellationToken.Register(r => ((WebRequest)r).Abort(), request, false))
            {
                return (HttpWebResponse)await task.ConfigureAwait(false);
            }
        }
    }
}

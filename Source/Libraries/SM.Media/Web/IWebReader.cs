// -----------------------------------------------------------------------
//  <copyright file="IWebReader.cs" company="Henric Jungheim">
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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SM.Media.Content;

namespace SM.Media.Web
{
    public interface IWebStreamResponse : IDisposable
    {
        bool IsSuccessStatusCode { get; }
        Uri ActualUrl { get; }
        int HttpStatusCode { get; }
        long? ContentLength { get; }
        void EnsureSuccessStatusCode();
        Task<Stream> GetStreamAsync(CancellationToken cancellationToken);
    }

    public class WebResponse
    {
        public Uri RequestUri { get; set; }
        public long? ContentLength { get; set; }
        public IEnumerable<KeyValuePair<string, IEnumerable<string>>> Headers { get; set; }
        public ContentType ContentType { get; set; }
    }

    public interface IWebReader : IDisposable
    {
        Uri BaseAddress { get; }
        Uri RequestUri { get; }
        ContentType ContentType { get; }
        IWebReaderManager Manager { get; }

        Task<IWebStreamResponse> GetWebStreamAsync(Uri url, bool waitForContent, CancellationToken cancellationToken,
            Uri referrer = null, long? from = null, long? to = null, WebResponse response = null);

        Task<byte[]> GetByteArrayAsync(Uri url, CancellationToken cancellationToken, WebResponse webResponse = null);
    }
}

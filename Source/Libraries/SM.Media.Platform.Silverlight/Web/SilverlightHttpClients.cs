// -----------------------------------------------------------------------
//  <copyright file="SilverlightHttpClients.cs" company="Henric Jungheim">
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
using System.Net.Http;

namespace SM.Media.Web
{
    public sealed class SilverlightHttpClients : IHttpClients, IDisposable
    {
        HttpClient _rootHttpClient;

        #region IDisposable Members

        public void Dispose()
        {
            var client = _rootHttpClient;

            if (null != client)
            {
                _rootHttpClient = null;
                client.Dispose();
            }
        }

        #endregion

        #region IHttpClients Members

        public HttpClient RootPlaylistClient
        {
            get
            {
                if (null == _rootHttpClient)
                    _rootHttpClient = new HttpClient();

                return _rootHttpClient;
            }
        }

        public HttpClient CreatePlaylistClient(Uri rootPlaylist)
        {
            return CreateHttpClient(rootPlaylist);
        }

        public HttpClient CreateSegmentClient(Uri segmentPlaylist)
        {
            return CreateHttpClient(segmentPlaylist);
        }

        public HttpClient CreateBinaryClient(Uri referrer)
        {
            return CreateHttpClient(referrer);
        }

        #endregion

        static HttpClient CreateHttpClient(Uri baseAddress)
        {
            var httpClient = new HttpClient
                             {
                                 BaseAddress = baseAddress
                             };

            return httpClient;
        }
    }
}

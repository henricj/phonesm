// -----------------------------------------------------------------------
//  <copyright file="ProgramManager.cs" company="Henric Jungheim">
//  Copyright (c) 2012, 2013.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012, 2013 Henric Jungheim <software@henric.org>
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
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SM.Media.M3U8;
using SM.Media.Utility;
using SM.Media.Web;
using RetryPolicy = SM.Media.Utility.RetryPolicy;

namespace SM.Media.Playlists
{
    public class ProgramManager : ProgramManagerBase, IProgramManager
    {
        static readonly IDictionary<long, Program> NoPrograms = new Dictionary<long, Program>();

        public ProgramManager(IHttpClients httpClients)
            : base(httpClients)
        { }

        #region IProgramManager Members

        public IEnumerable<Uri> Playlists { get; set; }

        public async Task<IDictionary<long, Program>> LoadAsync(CancellationToken cancellationToken)
        {
            var parser = new M3U8Parser();
            Uri actualPlaylist = null;

            var playlists = Playlists;

            foreach (var playlist in playlists)
            {
                actualPlaylist = playlist;

                var localPlaylist = playlist;

                var httpClient = HttpClients.RootPlaylistClient;

                var playlistString = await new Retry(4, 100, RetryPolicy.IsWebExceptionRetryable)
                    .CallAsync(async () =>
                                     {
                                         var response = await httpClient.GetAsync(localPlaylist, HttpCompletionOption.ResponseContentRead, cancellationToken)
                                                                        .ConfigureAwait(false);

                                         response.EnsureSuccessStatusCode();

                                         return await response.Content.ReadAsStringAsync()
                                                              .ConfigureAwait(false);
                                     })
                    .ConfigureAwait(false);

                using (var sr = new StringReader(playlistString))
                {
                    parser.Parse(actualPlaylist, sr);
                }

                break;
            }

            if (null == actualPlaylist)
                return NoPrograms;

            return Load(actualPlaylist, parser);
        }

        #endregion
    }
}

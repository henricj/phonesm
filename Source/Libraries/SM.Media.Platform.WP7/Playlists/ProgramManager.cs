// -----------------------------------------------------------------------
//  <copyright file="ProgramManager.cs" company="Henric Jungheim">
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
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SM.Media.M3U8;

namespace SM.Media.Playlists
{
    public class ProgramManager : ProgramManagerBase, IProgramManager
    {
        internal static readonly Encoding M3UEncoding = Encoding.GetEncoding("iso-8859-1");
        static readonly IDictionary<long, Program> NoPrograms = new Dictionary<long, Program>();

        public IDictionary<long, Program> Load(Uri playlist)
        {
            var parser = new M3U8Parser();

            using (var f = new WebClient().OpenReadTaskAsync(playlist).Result)
            {
                // The "HTTP Live Streaming" draft says US ASCII; the original .m3u says Windows 1252 (a superset of US ASCII).
                var encoding = ".m3u" == Path.GetExtension(playlist.LocalPath) ? M3UEncoding : Encoding.UTF8;

                parser.Parse(f, encoding);
            }

            return Load(playlist, parser);
        }

        public async Task<IDictionary<long, Program>> LoadAsync(IEnumerable<Uri> playlistUrls, CancellationToken cancellationToken)
        {
            var parser = new M3U8Parser();
            Uri actualPlaylist = null;

            foreach (var playlist in playlistUrls)
            {
                actualPlaylist = playlist;

                await parser.ParseAsync(actualPlaylist, cancellationToken);
            }

            if (null == actualPlaylist)
                return NoPrograms;

            return Load(actualPlaylist, parser);
        }
    }
}

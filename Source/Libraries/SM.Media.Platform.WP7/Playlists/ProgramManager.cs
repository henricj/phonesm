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
    public class ProgramManager : ProgramManagerBase, IDisposable
    {
        internal static readonly Encoding M3uEncoding = Encoding.GetEncoding("iso-8859-1");
        int _isDisposed;

        #region IDisposable Members

        public void Dispose()
        {
            if (0 != Interlocked.Exchange(ref _isDisposed, 1))
                return;

            Dispose(false);
        }

        #endregion

        protected virtual void Dispose(bool disposing)
        { }

        public IDictionary<long, Program> Load(Uri playlist)
        {
            var parser = new M3U8Parser();

            using (var f = new WebClient().OpenReadTaskAsync(playlist).Result)
            {
                // The "HTTP Live Streaming" draft says US ASCII; the original .m3u says Windows 1252 (a superset of US ASCII).
                var encoding = ".m3u" == Path.GetExtension(playlist.LocalPath) ? M3uEncoding : Encoding.UTF8;

                parser.Parse(f, encoding);
            }

            return Load(playlist, parser);
        }

        public async Task<IDictionary<long, Program>> LoadAsync(Uri playlist, CancellationToken cancellationToken)
        {
            var parser = new M3U8Parser();

            await parser.ParseAsync(playlist, cancellationToken);

            return Load(playlist, parser);
        }
    }
}

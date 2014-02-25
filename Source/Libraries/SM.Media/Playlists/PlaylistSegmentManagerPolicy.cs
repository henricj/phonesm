// -----------------------------------------------------------------------
//  <copyright file="PlaylistSegmentManagerPolicy.cs" company="Henric Jungheim">
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SM.Media.Content;
using SM.Media.M3U8;
using SM.Media.Web;

namespace SM.Media.Playlists
{
    public interface IPlaylistSegmentManagerPolicy
    {
        Task<ISubProgram> CreateSubProgramAsync(ICollection<Uri> source, ContentType contentType, CancellationToken cancellationToken);
    }

    public class PlaylistSegmentManagerPolicy : IPlaylistSegmentManagerPolicy
    {
        public static Func<IEnumerable<ISubProgram>, ISubProgram> SelectSubProgram = programs => programs.FirstOrDefault();
        readonly IHttpClients _httpClients;
        readonly SegmentsFactory _segmentsFactory;
        readonly IWebCacheFactory _webCacheFactory;
        readonly IWebContentTypeDetector _webContentTypeDetector;

        public PlaylistSegmentManagerPolicy(IHttpClients httpClients, IWebCacheFactory webCacheFactory,
            IWebContentTypeDetector webContentTypeDetector)
        {
            if (null == httpClients)
                throw new ArgumentNullException("httpClients");
            if (null == webCacheFactory)
                throw new ArgumentNullException("webCacheFactory");
            if (null == webContentTypeDetector)
                throw new ArgumentNullException("webContentTypeDetector");

            _httpClients = httpClients;
            _webCacheFactory = webCacheFactory;
            _webContentTypeDetector = webContentTypeDetector;
            _segmentsFactory = new SegmentsFactory(httpClients);
        }

        #region IPlaylistSegmentManagerPolicy Members

        public Task<ISubProgram> CreateSubProgramAsync(ICollection<Uri> source, ContentType contentType, CancellationToken cancellationToken)
        {
            var programManager = CreateProgramManager(source, contentType, cancellationToken);

            return LoadSubProgram(programManager, contentType, cancellationToken);
        }

        #endregion

        protected virtual IStreamSegments CreateStreamSegments(M3U8Parser parser)
        {
            return _segmentsFactory.CreateStreamSegments(parser);
        }

        protected virtual IProgramManager CreateProgramManager(ICollection<Uri> source, ContentType contentType, CancellationToken cancellationToken)
        {
            var programManager = new ProgramManager(_httpClients, CreateStreamSegments, _webCacheFactory, _webContentTypeDetector)
                                 {
                                     Playlists = source
                                 };

            return programManager;
        }

        protected virtual async Task<ISubProgram> LoadSubProgram(IProgramManager programManager, ContentType contentType, CancellationToken cancellationToken)
        {
            ISubProgram subProgram;

            try
            {
                var programs = await programManager.LoadAsync(cancellationToken).ConfigureAwait(false);

                var program = programs.Values.FirstOrDefault();

                if (null == program)
                {
                    Debug.WriteLine("PlaylistSegmentManagerFactory.SetMediaSource(): program not found");
                    throw new FileNotFoundException("Unable to load program");
                }

                subProgram = SelectSubProgram(program.SubPrograms);

                if (null == subProgram)
                {
                    Debug.WriteLine("PlaylistSegmentManagerFactory.SetMediaSource(): no sub programs found");
                    throw new FileNotFoundException("Unable to load program stream");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("PlaylistSegmentManagerFactory.SetMediaSource(): unable to load playlist: " + ex.Message);
                throw;
            }

            return subProgram;
        }
    }
}

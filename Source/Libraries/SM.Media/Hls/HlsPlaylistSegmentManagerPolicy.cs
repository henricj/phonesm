// -----------------------------------------------------------------------
//  <copyright file="HlsPlaylistSegmentManagerPolicy.cs" company="Henric Jungheim">
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
using SM.Media.Playlists;
using SM.Media.Utility;
using SM.Media.Web;

namespace SM.Media.Hls
{
    public interface IHlsPlaylistSegmentManagerPolicy
    {
        Task<ISubProgram> CreateSubProgramAsync(ICollection<Uri> source, ContentType contentType, CancellationToken cancellationToken);
    }

    public class HlsPlaylistSegmentManagerPolicy : IHlsPlaylistSegmentManagerPolicy
    {
        public static Func<IEnumerable<ISubProgram>, ISubProgram> SelectSubProgram = programs => programs.FirstOrDefault();
        readonly IPlatformServices _platformServices;
        readonly IRetryManager _retryManager;
        readonly IWebReaderManager _webReaderManager;

        public HlsPlaylistSegmentManagerPolicy(IWebReaderManager webReaderManager, IRetryManager retryManager, IPlatformServices platformServices)
        {
            if (null == webReaderManager)
                throw new ArgumentNullException("webReaderManager");
            if (null == retryManager)
                throw new ArgumentNullException("retryManager");
            if (null == platformServices)
                throw new ArgumentNullException("platformServices");

            _webReaderManager = webReaderManager;
            _retryManager = retryManager;
            _platformServices = platformServices;
        }

        #region IHlsPlaylistSegmentManagerPolicy Members

        public Task<ISubProgram> CreateSubProgramAsync(ICollection<Uri> source, ContentType contentType, CancellationToken cancellationToken)
        {
            var programManager = CreateProgramManager(source, contentType);

            return LoadSubProgram(programManager, contentType, cancellationToken);
        }

        #endregion

        protected virtual IProgramManager CreateProgramManager(ICollection<Uri> source, ContentType contentType)
        {
            if (ContentTypes.M3U != contentType && ContentTypes.M3U8 != contentType)
            {
                throw new NotSupportedException(string.Format("Content type {0} not supported by this program manager",
                    null == contentType ? "<unknown>" : contentType.ToString()));
            }

            var programManager = new HlsProgramManager(_webReaderManager, _retryManager, _platformServices)
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

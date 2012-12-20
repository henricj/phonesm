// -----------------------------------------------------------------------
//  <copyright file="Simulator.cs" company="Henric Jungheim">
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
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using SM.Media;
using SM.Media.Playlists;
using SM.Media.Segments;

namespace SimulatedPlayer
{
    sealed class Simulator : IDisposable
    {
        ProgramManager _programManager;
        SegmentReaderManager _segmentReaderManager;
        TsMediaManager _tsMediaManager;

        #region IDisposable Members

        public void Dispose()
        {
            using (_tsMediaManager)
            { }
        }

        #endregion

        public async Task Run()
        {
            _programManager = new ProgramManager { Playlists = new[] { new Uri("http://www.nasa.gov/multimedia/nasatv/NTV-Public-IPS.m3u8") } };
            //var programManager = new ProgramManager { Playlists = new[] { new Uri("http://devimages.apple.com/iphone/samples/bipbop/bipbopall.m3u8") } };

            SM.Media.Playlists.Program program;
            ISubProgram subProgram;

            try
            {
                var programs = await _programManager.LoadAsync();

                program = programs.Values.FirstOrDefault();

                if (null == program)
                {
                    return;
                }

                subProgram = program.SubPrograms.FirstOrDefault();

                if (null == subProgram)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                return;
            }

            Func<Uri, HttpWebRequest> webRequestFactory = new HttpWebRequestFactory(program.Url).Create;

            var playlist = new PlaylistSegmentManager(uri => new CachedWebRequest(uri, webRequestFactory), subProgram);

            var mediaElementManager = new SimulatedMediaElementManager();

            _tsMediaManager = new TsMediaManager(mediaElementManager, mm => new SimulatedMediaStreamSource(mm, mediaElementManager));

            _segmentReaderManager = new SegmentReaderManager(playlist);

            _tsMediaManager.Play(_segmentReaderManager);
        }
    }
}

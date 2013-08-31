// -----------------------------------------------------------------------
//  <copyright file="Simulator.cs" company="Henric Jungheim">
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
using System.Linq;
using System.Threading.Tasks;
using SM.Media;
using SM.Media.Playlists;
using SM.Media.Segments;
using SM.Media.Web;

namespace SimulatedPlayer
{
    sealed class Simulator : IDisposable
    {
        readonly IHttpClients _httpClients;
        readonly SegmentsFactory _segmentsFactory;
        ProgramManager _programManager;
        SegmentReaderManager _segmentReaderManager;
        TsMediaManager _tsMediaManager;

        public Simulator(IHttpClients httpClients)
        {
            if (httpClients == null)
                throw new ArgumentNullException("httpClients");

            _httpClients = httpClients;

            _segmentsFactory = new SegmentsFactory(_httpClients);
        }

        #region IDisposable Members

        public void Dispose()
        {
            using (_tsMediaManager)
            { }
        }

        #endregion

        public async Task Run()
        {
            _programManager = new ProgramManager(_httpClients, _segmentsFactory.CreateStreamSegments)
                              {
                                  Playlists = new[]
                                              {
                                                  //new Uri("http://www.nasa.gov/multimedia/nasatv/NTV-Public-IPS.m3u8")
                                                  new Uri("http://devimages.apple.com/iphone/samples/bipbop/bipbopall.m3u8")
                                              }
                              };

            ISubProgram subProgram;

            try
            {
                var programs = await _programManager.LoadAsync();

                var program = programs.Values.FirstOrDefault();

                if (null == program)
                    return;

                subProgram = program.SubPrograms.FirstOrDefault();

                if (null == subProgram)
                    return;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Program load failed: " + ex.Message);
                return;
            }

            var playlist = new PlaylistSegmentManager(uri => new CachedWebRequest(uri, _httpClients.CreatePlaylistClient(uri)), subProgram, _segmentsFactory.CreateStreamSegments);

            var mediaElementManager = new SimulatedMediaElementManager();

            _segmentReaderManager = new SegmentReaderManager(new[] { playlist }, _httpClients.CreateSegmentClient);

            _tsMediaManager = new TsMediaManager(_segmentReaderManager, mediaElementManager, new SimulatedMediaStreamSource(mediaElementManager));

            _tsMediaManager.Play();
        }
    }
}

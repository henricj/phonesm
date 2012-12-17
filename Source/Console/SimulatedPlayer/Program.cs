// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Henric Jungheim">
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
    interface ISimulatedMediaElement
    {
        void ReportOpenMediaCompleted();
        void ReportSeekCompleted(long ticks);
        void ReportGetSampleProgress(double progress);
        void ReportGetSampleCompleted(int streamType, IStreamSample sample);
    }

    interface ISimulatedMediaStreamSource : IMediaStreamSource
    {
        void OpenMediaAsync();
        void SeekAsync(long seekToTime);
        void GetSampleAsync(int streamType);
        void CloseMedia();
    }

    class Program
    {
        static async Task Do()
        {
            var programManager = new ProgramManager { Playlists = new[] { new Uri("http://www.nasa.gov/multimedia/nasatv/NTV-Public-IPS.m3u8") } };
            //var programManager = new ProgramManager { Playlists = new[] { new Uri("http://devimages.apple.com/iphone/samples/bipbop/bipbopall.m3u8") } };

            SM.Media.Playlists.Program program;
            ISubProgram subProgram;

            try
            {
                var programs = await programManager.LoadAsync();

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

            var tsMediaManager = new TsMediaManager(mediaElementManager, mm => new SimulatedMediaStreamSource(mm, mediaElementManager));

            tsMediaManager.Play(new SegmentReaderManager(playlist));
        }

        static void Main(string[] args)
        {
            Do().Wait();

            Console.WriteLine("Press any key to exit");

            Console.ReadLine();
        }
    }
}

// -----------------------------------------------------------------------
//  <copyright file="AudioTrackStreamer.cs" company="Mikael Koskinen">
//  Copyright (c) 2013.
//  <author>Mikael Koskinen</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2013 Mikael Koskinen <mikael.koskinen@live.com>
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
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using Microsoft.Phone.BackgroundAudio;
using SM.Media.Playlists;
using SM.Media.Segments;
using SM.Media.Utility;
using SM.Media.Web;
using SM.TsParser;

namespace SM.Media.BackgroundAudioStreamingAgent
{
    /// <summary>
    /// A background agent that performs per-track streaming for playback
    /// </summary>
    public class AudioTrackStreamer : AudioStreamingAgent, IMediaElementManager
    {
        private AudioStreamer currentStreamer;
        private TsMediaManager tsMediaManager;

        protected async override void OnBeginStreaming(AudioTrack track, AudioStreamer streamer)
        {
            GlobalPlatformServices.Default = new PlatformServices();
            currentStreamer = streamer;

            var httpClients = new HttpClients();
            var segmentsFactory = new SegmentsFactory(httpClients);

            var programManager = new ProgramManager(httpClients, segmentsFactory.CreateStreamSegments)
                                     {
                                         Playlists = new[] { new Uri(track.Tag) }
                                     };

            var programs = await programManager.LoadAsync();
            var program = programs.Values.First();
            var subProgram = program.SubPrograms.First();

            var programClient = httpClients.CreatePlaylistClient(program.Url);
            var playlist = new PlaylistSegmentManager(uri => new CachedWebRequest(uri, programClient), subProgram, segmentsFactory.CreateStreamSegments);
            var segmentReaderManager = new SegmentReaderManager(new[] { playlist }, httpClients.CreateSegmentClient);

            var tsMediaStreamSource = new TsMediaStreamSource();
            tsMediaManager = new TsMediaManager(segmentReaderManager, this, tsMediaStreamSource, streams =>
                                                                                                     {
                                                                                                         var firstAudio = streams.Streams.First(x => x.StreamType.Contents == TsStreamType.StreamContents.Audio);

                                                                                                         var others = streams.Streams.Where(x => x.Pid != firstAudio.Pid);
                                                                                                         foreach (
                                                                                                             var programStream in others)
                                                                                                         {
                                                                                                             programStream.BlockStream = true;
                                                                                                         }
                                                                                                     });

            tsMediaManager.OnStateChange += TsMediaManagerOnOnStateChange;
            tsMediaManager.Play();
        }

        private void TsMediaManagerOnOnStateChange(object sender, TsMediaManagerStateEventArgs tsMediaManagerStateEventArgs)
        {
            var message = string.Format("Media manager state in background agent: {0}, message: {1}", tsMediaManagerStateEventArgs.State,
                                        tsMediaManagerStateEventArgs.Message);
            Debug.WriteLine(message);
        }


        /// <summary>
        /// Called when the agent request is getting cancelled
        /// The call to base.OnCancel() is necessary to release the background streaming resources
        /// </summary>
        protected override void OnCancel()
        {
            base.OnCancel();
        }

        public Task SetSource(IMediaStreamSource source)
        {
            var mediaStreamSource = (MediaStreamSource)source;

            currentStreamer.SetSource(mediaStreamSource);

            return TplTaskExtensions.CompletedTask;
        }

        public Task Close()
        {
            NotifyComplete();
            return TplTaskExtensions.CompletedTask;
        }
    }
}
// -----------------------------------------------------------------------
//  <copyright file="AudioTrackStreamer.cs" company="Mikael Koskinen">
//  Copyright (c) 2013.
//  <author>Mikael Koskinen</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2013 Mikael Koskinen <mikael.koskinen@live.com>
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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using Microsoft.Phone.BackgroundAudio;
using Microsoft.Phone.Info;
using SM.Media.Buffering;
using SM.Media.Utility;
using SM.Media.Web;
using SM.TsParser;

namespace SM.Media.BackgroundAudioStreamingAgent
{
    /// <summary>
    ///     A background agent that performs per-track streaming for playback
    /// </summary>
    public class AudioTrackStreamer : AudioStreamingAgent
    {
        MediaStreamFascade _mediaStreamFascade;
        readonly IMediaStreamFascadeParameters _mediaStreamFascadeParameters;
        static readonly IApplicationInformation ApplicationInformation = ApplicationInformationFactory.Default;

        public AudioTrackStreamer()
        {
            var httpClients = new HttpClients(userAgent: ApplicationInformation.CreateUserAgent());

            _mediaStreamFascadeParameters = MediaStreamFascadeParameters.Create<TsMediaStreamSource>(httpClients);

            _mediaStreamFascadeParameters.MediaManagerParameters.ProgramStreamsHandler =
                streams =>
                {
                    var firstAudio = streams.Streams.First(x => x.StreamType.Contents == TsStreamType.StreamContents.Audio);

                    var others = streams.Streams.Where(x => x.Pid != firstAudio.Pid);
                    foreach (
                        var programStream in others)
                        programStream.BlockStream = true;
                };

            _mediaStreamFascadeParameters.MediaManagerParameters.BufferingPolicy =
                new DefaultBufferingPolicy
                {
                    BytesMinimum = 200 * 1024
                };
        }

#if DEBUG
        readonly Timer _memoryPoll = new Timer(
            _ => Debug.WriteLine("<{0:F}MiB/{1:F}MiB>",
                DeviceStatus.ApplicationCurrentMemoryUsage.BytesToMiB(),
                DeviceStatus.ApplicationPeakMemoryUsage.BytesToMiB()));

#endif

        [Conditional("DEBUG")]
        void StartPoll()
        {
#if DEBUG
            Debug.WriteLine("<Limit {0:F}MiB>", DeviceStatus.ApplicationMemoryUsageLimit.BytesToMiB());
            _memoryPoll.Change(TimeSpan.Zero, TimeSpan.FromSeconds(15));
#endif
        }

        [Conditional("DEBUG")]
        void StopPoll()
        {
#if DEBUG
            _memoryPoll.Change(Timeout.Infinite, Timeout.Infinite);
#endif
        }

        protected override void OnBeginStreaming(AudioTrack track, AudioStreamer streamer)
        {
            Debug.WriteLine("AudioPlayer.OnBeginStreaming() track.Source {0} track.Tag {1}",
                null == track ? "<no track>" : null == track.Source ? "<none>" : track.Source.ToString(),
                null == track ? "<no track>" : track.Tag ?? "<none>");

            try
            {
                if (null == GlobalPlatformServices.Default)
                    GlobalPlatformServices.Default = new PlatformServices();

                if (null == track.Tag)
                {
                    Debug.WriteLine("AudioTrackStreamer.OnBeginStreaming() null url");

                    NotifyComplete();

                    return;
                }

                Uri url;
                if (!Uri.TryCreate(track.Tag, UriKind.Absolute, out url))
                {
                    Debug.WriteLine("AudioTrackStreamer.OnBeginStreaming() invalid url: " + track.Tag);

                    NotifyComplete();

                    return;
                }

                if (null != _mediaStreamFascade)
                {
                    _mediaStreamFascade.StateChange -= TsMediaManagerOnOnStateChange;
                    _mediaStreamFascade.DisposeSafe();
                }

                _mediaStreamFascade = new MediaStreamFascade(_mediaStreamFascadeParameters, mss => SetSourceAsync(mss, streamer))
                                      {
                                          Source = url
                                      };

                _mediaStreamFascade.StateChange += TsMediaManagerOnOnStateChange;

                _mediaStreamFascade.Play();

                StartPoll();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("AudioTrackStreamer.OnBeginStreamingAudio() failed: " + ex.Message);
                NotifyComplete();
            }
        }

        void TsMediaManagerOnOnStateChange(object sender, TsMediaManagerStateEventArgs tsMediaManagerStateEventArgs)
        {
            var message = string.Format("Media manager state in background agent: {0}, message: {1}", tsMediaManagerStateEventArgs.State,
                tsMediaManagerStateEventArgs.Message);
            Debug.WriteLine(message);
        }

        /// <summary>
        ///     Called when the agent request is getting cancelled
        ///     The call to base.OnCancel() is necessary to release the background streaming resources
        /// </summary>
        protected override void OnCancel()
        {
            Debug.WriteLine("AudioTrackStreamer.OnCancel()");

            if (null != _mediaStreamFascade)
                _mediaStreamFascade.RequestStop();

            StopPoll();

            base.OnCancel();
        }

        Task SetSourceAsync(IMediaStreamSource source, AudioStreamer streamer)
        {
            Debug.WriteLine("AudioTrackStreamer.SetSourceAsync()");

            var mediaStreamSource = (MediaStreamSource)source;

            streamer.SetSource(mediaStreamSource);

            return TplTaskExtensions.CompletedTask;
        }
    }
}

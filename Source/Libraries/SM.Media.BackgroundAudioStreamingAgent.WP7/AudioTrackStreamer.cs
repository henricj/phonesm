﻿// -----------------------------------------------------------------------
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
using SM.Media.MediaParser;
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
        readonly IBufferingPolicy _bufferingPolicy;
        readonly IHttpClients _httpClients;
        readonly IMediaManagerParameters _mediaManagerParameters;
        IMediaStreamFascade _mediaStreamFascade;
        static readonly IApplicationInformation ApplicationInformation = ApplicationInformationFactory.Default;

        public AudioTrackStreamer()
        {
            _httpClients = new HttpClients(userAgent: ApplicationInformation.CreateUserAgent());

            _mediaManagerParameters = new MediaManagerParameters
                                      {
                                          ProgramStreamsHandler =
                                              streams =>
                                              {
                                                  var firstAudio = streams.Streams.First(x => x.StreamType.Contents == TsStreamType.StreamContents.Audio);

                                                  var others = streams.Streams.Where(x => x.Pid != firstAudio.Pid);
                                                  foreach (
                                                      var programStream in others)
                                                      programStream.BlockStream = true;
                                              }
                                      };

            _bufferingPolicy = new DefaultBufferingPolicy
                               {
                                   BytesMinimumStarting = 24 * 1024,
                                   BytesMinimum = 64 * 1024
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

                if (null == track || null == track.Tag)
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

                InitializeMediaStream(streamer);

                _mediaStreamFascade.Source = url;

                _mediaStreamFascade.Play();

                StartPoll();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("AudioTrackStreamer.OnBeginStreamingAudio() failed: " + ex.Message);
                NotifyComplete();
            }
        }

        void InitializeMediaStream(AudioStreamer streamer)
        {
            if (null != _mediaStreamFascade)
                return;

            _mediaStreamFascade = MediaStreamFascadeSettings.Parameters.Create(_httpClients, mss => SetSourceAsync(mss, streamer));

            _mediaStreamFascade.SetParameter(_bufferingPolicy);

            _mediaStreamFascade.SetParameter(_mediaManagerParameters);

            _mediaStreamFascade.StateChange += TsMediaManagerOnStateChange;
        }

        void TsMediaManagerOnStateChange(object sender, TsMediaManagerStateEventArgs tsMediaManagerStateEventArgs)
        {
            var state = tsMediaManagerStateEventArgs.State;

            Debug.WriteLine("Media manager state in background agent: {0}, message: {1}", state, tsMediaManagerStateEventArgs.Message);

            if (null == _mediaStreamFascade)
                return;

            if (TsMediaManager.MediaState.Closed == state || TsMediaManager.MediaState.Error == state)
            {
                var mediaStreamFascade = _mediaStreamFascade;

                _mediaStreamFascade = null;

                mediaStreamFascade.StateChange -= TsMediaManagerOnStateChange;
                mediaStreamFascade.CloseAsync().ContinueWith(
                    t =>
                    {
                        mediaStreamFascade.DisposeSafe();

                        var exception = t.Exception;

                        Debug.WriteLine("AudioTrackStreamer.TsMediaManagerOnOnStateChange() calling NotifyComplete() exception " + exception);

                        NotifyComplete();
                        //Abort();
                    });
            }
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

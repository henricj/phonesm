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
using Microsoft.Phone.BackgroundAudio;
using Microsoft.Phone.Info;
using SM.Media.Buffering;
using SM.Media.Utility;
using SM.Media.Web;
using SM.Media.Web.HttpClientReader;
using SM.TsParser;

namespace SM.Media.BackgroundAudioStreamingAgent
{
    /// <summary>
    ///     A background agent that performs per-track streaming for playback
    /// </summary>
    public sealed class AudioTrackStreamer : AudioStreamingAgent, IDisposable
    {
        readonly IBufferingPolicy _bufferingPolicy;
        readonly IHttpClientsParameters _httpClientsParameters;
        readonly IMediaManagerParameters _mediaManagerParameters;
        IMediaStreamFacade _mediaStreamFacade;
        static readonly IApplicationInformation ApplicationInformation = ApplicationInformationFactory.Default;
        readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        int _isDisposed;

        public AudioTrackStreamer()
        {
            _httpClientsParameters = new HttpClientsParameters { UserAgent = ApplicationInformation.CreateUserAgent() };

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

        protected override async void OnBeginStreaming(AudioTrack track, AudioStreamer streamer)
        {
            Debug.WriteLine("AudioPlayer.OnBeginStreaming() track.Source {0} track.Tag {1}",
                null == track ? "<no track>" : null == track.Source ? "<none>" : track.Source.ToString(),
                null == track ? "<no track>" : track.Tag ?? "<none>");

            var callNotifyComplete = true;

            try
            {
                if (null == track || null == track.Tag)
                {
                    Debug.WriteLine("AudioTrackStreamer.OnBeginStreaming() null url");
                    return;
                }

                Uri url;
                if (!Uri.TryCreate(track.Tag, UriKind.Absolute, out url))
                {
                    Debug.WriteLine("AudioTrackStreamer.OnBeginStreaming() invalid url: " + track.Tag);
                    return;
                }

                InitializeMediaStream();

                var mss = await _mediaStreamFacade.CreateMediaStreamSourceAsync(url, CancellationToken.None).ConfigureAwait(false);

                if (null == mss)
                {
                    Debug.WriteLine("AudioTrackStreamer.OnBeginStreamingAudio() unable to create media stream source");
                    return;
                }

                streamer.SetSource(mss);

                StartPoll();

                callNotifyComplete = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("AudioTrackStreamer.OnBeginStreamingAudio() failed: " + ex.Message);
            }
            finally
            {
                if (callNotifyComplete)
                    NotifyComplete();
            }
        }

        void InitializeMediaStream()
        {
            if (null != _mediaStreamFacade)
                return;

            _mediaStreamFacade = MediaStreamFacadeSettings.Parameters.Create();

            _mediaStreamFacade.SetParameter(_httpClientsParameters);

            _mediaStreamFacade.SetParameter(_bufferingPolicy);

            _mediaStreamFacade.SetParameter(_mediaManagerParameters);

            _mediaStreamFacade.StateChange += TsMediaManagerOnStateChange;
        }

        void TsMediaManagerOnStateChange(object sender, TsMediaManagerStateEventArgs tsMediaManagerStateEventArgs)
        {
            var state = tsMediaManagerStateEventArgs.State;

            Debug.WriteLine("Media manager state in background agent: {0}, message: {1}", state, tsMediaManagerStateEventArgs.Message);

            if (null == _mediaStreamFacade)
                return;

            if (TsMediaManager.MediaState.Closed == state || TsMediaManager.MediaState.Error == state)
            {
                var mediaStreamFacade = _mediaStreamFacade;

                _mediaStreamFacade = null;

                mediaStreamFacade.StateChange -= TsMediaManagerOnStateChange;
                mediaStreamFacade.CloseAsync().ContinueWith(
                    t =>
                    {
                        mediaStreamFacade.DisposeSafe();

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

            TryCancel();

            if (null != _mediaStreamFacade)
                _mediaStreamFacade.RequestStop();

            StopPoll();

            base.OnCancel();

            Dispose();
        }

        void TryCancel()
        {
            try
            {
                if (!_cancellationTokenSource.IsCancellationRequested)
                    _cancellationTokenSource.Cancel();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("AudioTrackStreamer.OnCancel() failed: " + ex.Message);
            }
        }

        public void Dispose()
        {
            if (0 != Interlocked.Exchange(ref _isDisposed, 1))
                return;

            TryCancel();

#if DEBUG
            _memoryPoll.Dispose();
#endif

            _cancellationTokenSource.Dispose();
        }
    }
}

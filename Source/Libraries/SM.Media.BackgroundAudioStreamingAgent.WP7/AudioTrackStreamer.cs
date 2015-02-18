// -----------------------------------------------------------------------
//  <copyright file="AudioTrackStreamer.cs" company="Mikael Koskinen">
//  Copyright (c) 2013.
//  <author>Mikael Koskinen</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2013 Mikael Koskinen <mikael.koskinen@live.com>
// Copyright (c) 2012-2015 Henric Jungheim <software@henric.org>
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
using Microsoft.Phone.BackgroundAudio;
using Microsoft.Phone.Info;
using SM.Media.Buffering;
using SM.Media.MediaManager;
using SM.Media.Utility;
using SM.TsParser;

namespace SM.Media.BackgroundAudioStreamingAgent
{
    /// <summary>
    ///     A background agent that performs per-track streaming for playback
    /// </summary>
    public sealed class AudioTrackStreamer : AudioStreamingAgent, IDisposable
    {
        readonly IBufferingPolicy _bufferingPolicy;
        readonly IMediaManagerParameters _mediaManagerParameters;
        IMediaStreamFacade _mediaStreamFacade;
        CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        int _isDisposed;

        public AudioTrackStreamer()
        {
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

            MediaStreamFacadeSettings.Parameters.UseHttpConnection = true;

            _bufferingPolicy = new DefaultBufferingPolicy
            {
                BytesMinimumStarting = 24 * 1024,
                BytesMinimum = 64 * 1024
            };
        }

#if DEBUG
        readonly Timer _memoryPoll = new Timer(_ => DumpMemory());

        static void DumpMemory()
        {
            Debug.WriteLine("<{0:F}MiB/{1:F}MiB>",
                DeviceStatus.ApplicationCurrentMemoryUsage.BytesToMiB(),
                DeviceStatus.ApplicationPeakMemoryUsage.BytesToMiB());
        }
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

            Debug.Assert(null == _mediaStreamFacade, "_mediaStreamFacade is in use");

            if (_cancellationTokenSource.IsCancellationRequested)
                _cancellationTokenSource = new CancellationTokenSource();

            var callCleanup = true;

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

                Debug.Assert(null != _mediaStreamFacade);

                var mss = await _mediaStreamFacade.CreateMediaStreamSourceAsync(url, _cancellationTokenSource.Token).ConfigureAwait(false);

                if (null == mss)
                {
                    Debug.WriteLine("AudioTrackStreamer.OnBeginStreamingAudio() unable to create media stream source");
                    return;
                }

                streamer.SetSource(mss);

                StartPoll();

                callCleanup = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("AudioTrackStreamer.OnBeginStreamingAudio() failed: " + ex.Message);
            }
            finally
            {
                if (callCleanup)
                {
                    Debug.WriteLine("AudioPlayer.OnBeginStreaming() cleanup");

                    if (null == _mediaStreamFacade)
                    {
                        Debug.WriteLine("AudioTrackStreamer.OnBeginStreaming() NotifyComplete");
                        NotifyComplete();
                    }
                    else
                    {
                        var cleanupTask = CleanupMediaStreamFacade();

                        TaskCollector.Default.Add(cleanupTask, "OnBeginStreaming CleanupMediaStreamFacade");
                    }
                }
            }
        }

        void InitializeMediaStream()
        {
            if (null != _mediaStreamFacade)
                return;

            _mediaStreamFacade = MediaStreamFacadeSettings.Parameters.Create();

            _mediaStreamFacade.SetParameter(_bufferingPolicy);

            _mediaStreamFacade.SetParameter(_mediaManagerParameters);

            _mediaStreamFacade.StateChange += TsMediaManagerOnStateChange;
        }

        void TsMediaManagerOnStateChange(object sender, MediaManagerStateEventArgs mediaManagerStateEventArgs)
        {
            var state = mediaManagerStateEventArgs.State;

            Debug.WriteLine("Media manager state in background agent: {0}, message: {1}", state, mediaManagerStateEventArgs.Message);

            if (null == _mediaStreamFacade)
                return;

            if (MediaManagerState.Closed == state || MediaManagerState.Error == state)
            {
                var cleanupTask = CleanupMediaStreamFacade();

                TaskCollector.Default.Add(cleanupTask, "TsMediaManagerOnStateChange CleanupMediaStreamFacade");
            }
        }

        async Task CleanupMediaStreamFacade()
        {
            Debug.WriteLine("AudioTrackStreamer.CleanupMediaStreamFacade()");

            var mediaStreamFacade = Interlocked.Exchange(ref _mediaStreamFacade, null);

            if (null == mediaStreamFacade)
                return;

            mediaStreamFacade.StateChange -= TsMediaManagerOnStateChange;

            try
            {
                var localMediaStreamFacade = mediaStreamFacade;

                var closeTask = TaskEx.Run(() => localMediaStreamFacade.CloseAsync());

                var task = await TaskEx.WhenAny(closeTask, TaskEx.Delay(6000)).ConfigureAwait(false);

                var cleanupTask = closeTask.ContinueWith(t =>
                {
                    var ex = t.Exception;
                    if (null != ex)
                        Debug.WriteLine("AudioTrackStreamer.CleanupMediaStreamFacade() CloseAsync() failed: " + ex.Message);

                    localMediaStreamFacade.DisposeSafe();
                });

                if (!ReferenceEquals(task, closeTask))
                {
                    Debug.WriteLine("AudioTrackStreamer.TsMediaManagerOnStateChange CloseAsync timeout");

                    Abort();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("AudioTrackStreamer.TsMediaManagerOnStateChange CloseAsync failed: " + ex.ExtendedMessage());

                Abort();
            }

            mediaStreamFacade = null;

            Debug.WriteLine("AudioTrackStreamer.CleanupMediaStreamFacade() NotifyComplete");
            NotifyComplete();

            // Should this Collect be inside the #if DEBUG?
            // The available memory is usually only on the
            // order of a couple of megabytes on a 512MB device.
            // We are at a point where we can afford to stall
            // for a while and we have just released a large
            // number of objects.
            GC.Collect();
#if DEBUG
            GC.WaitForPendingFinalizers();
            GC.Collect();

            DumpMemory();
#endif
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

            var cleanupTask = CleanupMediaStreamFacade();

            TaskCollector.Default.Add(cleanupTask, "OnCancel CleanupMediaStreamFacade");
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
            Debug.WriteLine("AudioTrackStreamer.Dispose()");

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

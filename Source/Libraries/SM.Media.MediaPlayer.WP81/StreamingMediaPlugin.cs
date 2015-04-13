// -----------------------------------------------------------------------
//  <copyright file="StreamingMediaPlugin.cs" company="Henric Jungheim">
//  Copyright (c) 2012-2015.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
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
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PlayerFramework;
using SM.Media.Configuration;
using SM.Media.MediaManager;
using SM.Media.Metadata;
using SM.Media.TransportStream.TsParser;
using SM.Media.Utility;

namespace SM.Media.MediaPlayer
{
    public sealed partial class StreamingMediaPlugin
    {
        static readonly MediaManagerParameters MediaManagerParameters = new MediaManagerParameters
        {
            ProgramStreamsHandler = pss =>
            {
                var hasVideo = false;

                foreach (var stream in pss.Streams)
                {
                    switch (stream.StreamType.Contents)
                    {
                        case TsStreamType.StreamContents.Audio:
                            break;
                        case TsStreamType.StreamContents.Video:
                            if (hasVideo)
                                stream.BlockStream = true;
                            else
                                hasVideo = true;
                            break;
                        default:
                            stream.BlockStream = true;
                            break;
                    }
                }
            }
        };

        readonly AsyncLock _asyncLock = new AsyncLock();
        readonly ActionMetadataSink _metadataSink;
        readonly MetadataState _metadataState = new MetadataState();
        IConfigurationMetadata _configurationMetadata;
        CancellationTokenSource _mediaLoadingCancellationTokenSource = new CancellationTokenSource();
        IMediaStreamFacade _mediaStreamFacade;
        PlaybackSession _playbackSession;
        CancellationTokenSource _unloadCancellationTokenSource = new CancellationTokenSource();

        public StreamingMediaPlugin()
        {
            _metadataSink = new ActionMetadataSink(RequestMetadataUpdate);
        }

        #region IPlugin Members

        public void Load()
        {
            Debug.WriteLine("StreamingMediaPlugin.Load()");

            if (_unloadCancellationTokenSource.IsCancellationRequested)
            {
                _unloadCancellationTokenSource.Dispose();
                _unloadCancellationTokenSource = new CancellationTokenSource();
            }
        }

        public void Update(IMediaSource mediaSource)
        {
            Debug.WriteLine("StreamingMediaPlugin.Update()");
        }

        public void Unload()
        {
            Debug.WriteLine("StreamingMediaPlugin.Unload()");

            _unloadCancellationTokenSource.Cancel();

            CleanupAsync().Wait();
        }

        #endregion

        void UpdateMetadata()
        {
            try
            {
                var mediaPlayer = MediaPlayer;

                if (null == mediaPlayer)
                    return;

                var position = mediaPlayer.Position;

                var nextEvent = _metadataSink.Update(_metadataState, position);

                var configuration = _metadataState.ConfigurationMetadata;

                if (null == configuration)
                    return;

                if (ReferenceEquals(configuration, _configurationMetadata))
                    return;

                _configurationMetadata = configuration;

                var availableAudioStreams = mediaPlayer.AvailableAudioStreams;

                availableAudioStreams.Clear();

                if (null != configuration.Audio)
                    availableAudioStreams.Add(configuration.Audio.CreateAudioStream());

                if (null != configuration.AlternateStreams)
                {
                    var audioStreams = configuration.AlternateStreams
                        .OfType<IAudioConfigurationSource>()
                        .Select(AudioStreamFactory.CreateAudioStream);

                    availableAudioStreams.AddRange(audioStreams);
                }

                var count = 0;

                foreach (var audioStream in availableAudioStreams)
                {
                    ++count;

                    if (string.IsNullOrWhiteSpace(audioStream.Name))
                        audioStream.Name = count.ToString(CultureInfo.CurrentCulture);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("StreamingMediaPlugin.UpdateMetadata() failed: " + ex.ExtendedMessage());
            }
        }

        IMediaStreamFacade InitializeMediaStream()
        {
            if (null != _mediaStreamFacade && _mediaStreamFacade.IsDisposed)
                _mediaStreamFacade = null;

            if (null == _mediaStreamFacade)
                _mediaStreamFacade = CreateMediaStreamFacade();

            return _mediaStreamFacade;
        }

        IMediaStreamFacade CreateMediaStreamFacade()
        {
            var mediaStreamFacade = MediaStreamFacadeSettings.Parameters.Create();

#if WINDOWS_APP || WINDOWS_PHONE_APP
            mediaStreamFacade.SetParameter(MediaManagerParameters);
#endif

            mediaStreamFacade.SetParameter(_metadataSink);

            return mediaStreamFacade;
        }

        async Task PlayAsync(PlaybackSession playbackSession, Uri source, CancellationToken cancellationToken)
        {
            try
            {
                using (playbackSession)
                {
                    _playbackSession = playbackSession;

                    await playbackSession.PlayAsync(source, cancellationToken).ConfigureAwait(false);

                    _playbackSession = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("StreamingMediaPlugin.PlayAsync() failed: " + ex.ExtendedMessage());
            }
        }

        async Task CleanupAsync()
        {
            Debug.WriteLine("StreamingMediaPlugin.CleanupAsync()");

            try
            {
                var playbackSession = _playbackSession;

                if (null != playbackSession)
                    await playbackSession.CloseAsync().ConfigureAwait(false);

                using (await _asyncLock.LockAsync(CancellationToken.None).ConfigureAwait(false))
                {
                    if (_mediaStreamFacade.IsDisposed)
                        _mediaStreamFacade = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("StreamingMediaPlugin.CleanupAsync() failed: " + ex.Message);
            }
        }

        async Task PlaybackLoadingAsync(MediaLoadingEventArgs mediaLoadingEventArgs)
        {
            // ReSharper disable once PossiblyMistakenUseOfParamsMethod
            var mediaLoadingCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_unloadCancellationTokenSource.Token);

            var oldTokenSource = Interlocked.Exchange(ref _mediaLoadingCancellationTokenSource, mediaLoadingCancellationTokenSource);

            if (null != oldTokenSource)
                oldTokenSource.CancelDisposeSafe();

            var source = mediaLoadingEventArgs.Source;

            if (null == source)
                return;

            MediaPlayerDeferral deferral = null;

            try
            {
                deferral = mediaLoadingEventArgs.DeferrableOperation.GetDeferral();

                if (deferral.CancellationToken.IsCancellationRequested)
                    return;

                using (var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(deferral.CancellationToken, mediaLoadingCancellationTokenSource.Token))
                {
                    var cancellationToken = linkedTokenSource.Token;

                    PlaybackSession playbackSession;

                    using (await _asyncLock.LockAsync(cancellationToken).ConfigureAwait(false))
                    {
                        playbackSession = _playbackSession;

                        if (null != playbackSession)
                        {
                            await playbackSession.StopAsync(cancellationToken);

                            playbackSession = _playbackSession;

                            if (null != playbackSession)
                            {
                                Debug.WriteLine("StreamingMediaPlugin MediaLoading non-null _playbackSession");

                                return;
                            }
                        }

                        if (string.Equals(source.Scheme, "stop", StringComparison.OrdinalIgnoreCase))
                        {
                            Debug.WriteLine("StreamingMediaPlugin MediaLoading stop");
                            return;
                        }

                        var passThrough = StreamingMediaSettings.Parameters.IsPassThrough;

                        if (null != passThrough)
                        {
                            if (passThrough(source))
                            {
                                Debug.WriteLine("StreamingMediaPlugin.PlayerOnMediaLoading() passing through " + source);

                                deferral.Complete();
                                deferral = null;

                                return;
                            }
                        }

                        playbackSession = new PlaybackSession(InitializeMediaStream());

                        var playTask = PlayAsync(playbackSession, source, cancellationToken);

                        TaskCollector.Default.Add(playTask, "StreamingMediaPlugin MediaLoading playTask");
                    }

                    mediaLoadingEventArgs.MediaStreamSource = await playbackSession.GetMediaSourceAsync(cancellationToken).ConfigureAwait(false);
                    mediaLoadingEventArgs.Source = null;

                    //Debug.WriteLine("StreamingMediaPlugin MediaLoading deferral.Complete()");
                    deferral.Complete();
                    deferral = null;
                }
            }
            catch (OperationCanceledException)
            { }
            catch (Exception ex)
            {
                Debug.WriteLine("StreamingMediaPlugin.PlayerOnMediaLoading() failed: " + ex.Message);
            }
            finally
            {
                if (null != deferral)
                {
                    Debug.WriteLine("StreamingMediaPlugin MediaLoading deferral.Cancel()");
                    deferral.Cancel();
                }
            }
        }

        void PlaybackFailed()
        {
            var playbackSession = _playbackSession;

            if (null != playbackSession)
            {
                var task = playbackSession.OnMediaFailedAsync();

                TaskCollector.Default.Add(task, "StreamingMediaPlugin.PlaybackFailed() PlayerOnMediaFailedAsync");
            }
        }
    }
}

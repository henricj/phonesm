﻿// -----------------------------------------------------------------------
//  <copyright file="SimulatedMediaStreamSource.cs" company="Henric Jungheim">
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using SM.Media;
using SM.Media.Configuration;
using SM.Media.Utility;

namespace SimulatedPlayer
{
    class SimulatedMediaStreamSource : ISimulatedMediaStreamSource
    {
        readonly CommandWorker _commandWorker = new CommandWorker();
        readonly object _lock = new object();
        readonly ISimulatedMediaElement _mediaElement;
        readonly IMediaManager _mediaManager;
        readonly List<IStreamSource> _mediaStreams = new List<IStreamSource>();
        readonly List<CommandWorker.Command> _pendingGets = new List<CommandWorker.Command>();
        readonly object _stateLock = new object();
        bool _isClosed;
        State _state;

        public SimulatedMediaStreamSource(IMediaManager mediaManager, ISimulatedMediaElement mediaElement)
        {
            _mediaManager = mediaManager;
            _mediaElement = mediaElement;
        }

        #region ISimulatedMediaStreamSource Members

        public void Dispose()
        {
            using (_commandWorker)
            { }
        }

        public void ReportProgress(double obj)
        { }

        public void MediaStreamOnConfigurationComplete(object sender, ConfigurationEventArgs e)
        {
            lock (_lock)
            {
                _mediaStreams.Add(e.StreamSource);

                var streamType = _mediaStreams.Count - 1;
                e.StreamSource.SetSink(sample => StreamSampleHandler(streamType, sample));

                if (_mediaStreams.Count < 2)
                    return;
            }

            Debug.WriteLine("SimulatedMediaStreamSource: ReportOpenMediaCompleted ({0} streams)", 2);

            _mediaManager.ValidateEvent(MediaStreamFsm.MediaEvent.CallingReportOpenMediaCompleted);

            _mediaElement.ReportOpenMediaCompleted();
        }

        public void OpenMediaAsync()
        {
            Debug.WriteLine("SimulatedMediaStreamSource.OpenMediaAsync()");
            _mediaManager.ValidateEvent(MediaStreamFsm.MediaEvent.OpenMediaAsyncCalled);

            _mediaManager.OpenMedia();
        }

        public void SeekAsync(long seekToTime)
        {
            var seekTimestamp = TimeSpan.FromTicks(seekToTime);

            Debug.WriteLine("SimulatedMediaStreamSource.SeekAsync({0})", seekTimestamp);
            _mediaManager.ValidateEvent(MediaStreamFsm.MediaEvent.SeekAsyncCalled);

            _commandWorker.SendCommand(new CommandWorker.Command(
                                           async () =>
                                           {
                                               if (_isClosed)
                                                   return;

                                               var position = await _mediaManager.SeekMediaAsync(seekTimestamp);

                                               if (_isClosed)
                                                   return;

                                               _mediaManager.ValidateEvent(MediaStreamFsm.MediaEvent.CallingReportSeekCompleted);
                                               _mediaElement.ReportSeekCompleted(position.Ticks);


                                               CommandWorker.Command[] pendingGets;

                                               lock (_stateLock)
                                               {
                                                   pendingGets = _pendingGets.ToArray();

                                                   _pendingGets.Clear();

                                                   _state = State.Play;
                                               }

                                               foreach (var getCmd in pendingGets)
                                                   _commandWorker.SendCommand(getCmd);
                                           }));
        }

        public void GetSampleAsync(int streamType)
        {
            var command = new CommandWorker.Command(
                () =>
                {
                    IStreamSource streamSource = null;

                    if (streamType < 0)
                        return null;

                    lock (_lock)
                    {
                        if (streamType >= _mediaStreams.Count)
                            return null;

                        streamSource = _mediaStreams[streamType];
                    }

                    if (null == streamSource)
                    {
                        _mediaElement.ReportGetSampleProgress(0);
                        return null;
                    }

                    streamSource.GetNextSample();

                    return null;
                });

            lock (_stateLock)
            {
                var state = _state;

                Debug.WriteLine("SimulatedMediaStreamSource.GetSampleAsync({0}) state {1}", streamType, state);

                _mediaManager.ValidateEvent(MediaStreamFsm.MediaEvent.GetSampleAsyncCalled);

                if (State.Play != state)
                {
                    Debug.WriteLine("SimulatedMediaStreamSource defer Get({0})", streamType);
                    _pendingGets.Add(command);
                }
                else
                    _commandWorker.SendCommand(command);
            }
        }

        public void CloseMedia()
        {
            Debug.WriteLine("SimulatedMediaStreamSource.CloseMedia()");
            _mediaManager.ValidateEvent(MediaStreamFsm.MediaEvent.CloseMediaCalled);

            lock (_stateLock)
            {
                _isClosed = true;

                _state = State.Closed;
            }

            _mediaManager.CloseMedia();
        }

        #endregion

        void StreamSampleHandler(int streamType, IStreamSample sample)
        {
            _mediaManager.ValidateEvent(MediaStreamFsm.MediaEvent.CallingReportSampleCompleted);
            _mediaElement.ReportGetSampleCompleted(streamType, sample);
        }

        public Task CloseAsync()
        {
            return TplTaskExtensions.CompletedTask;
        }

        #region Nested type: State

        enum State
        {
            Idle,
            Open,
            Seek,
            Play,
            Closed,
            WaitForClose
        }

        #endregion
    }
}
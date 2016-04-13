// -----------------------------------------------------------------------
//  <copyright file="AudioMetadataHandler.cs" company="Henric Jungheim">
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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Phone.BackgroundAudio;
using SM.Media.Metadata;
using SM.Media.Utility;

namespace SM.Media.BackgroundAudioStreamingAgent
{
    sealed class AudioMetadataHandler : IDisposable
    {
        readonly object _lock = new object();
        readonly MetadataSink _metadataSink;
        readonly MetadataState _state = new MetadataState();
        readonly Timer _timer;
        readonly SignalTask _updateTask;
        string _artist;
        string _defaultTitle;
        TimeSpan _lastReport = TimeSpan.Zero;
        string _title;

        public AudioMetadataHandler(CancellationToken cancellationToken)
        {
            _updateTask = new SignalTask(Update, cancellationToken);

            _metadataSink = new ActionMetadataSink(_updateTask.Fire);

            _timer = new Timer(obj => ((AudioMetadataHandler)obj).Refresh(), this, Timeout.Infinite, Timeout.Infinite);
        }

        public IMetadataSink MetadataSink
        {
            get { return _metadataSink; }
        }

        public string DefaultTitle
        {
            get { return _defaultTitle; }
            set
            {
                _defaultTitle = value;
                Refresh();
            }
        }

        public string Title
        {
            get { return _title; }
        }

        #region IDisposable Members

        public void Dispose()
        {
            _timer.Dispose();

            _updateTask.Dispose();
        }

        #endregion

        Task Update()
        {
            Debug.WriteLine("AudioMetadataHandler.Update()");

            try
            {
                var player = BackgroundAudioPlayer.Instance;

                if (null == player)
                    return TplTaskExtensions.CompletedTask;

                var track = player.Track;

                if (null == track)
                {
                    Debug.WriteLine("AudioMetadataHandler.Update() no track: " + player.PlayerState);

                    _timer.Change(333, Timeout.Infinite);

                    return TplTaskExtensions.CompletedTask;
                }

                var position = player.Position;

                TimeSpan? nextEvent = null;

                lock (_lock)
                {
                    nextEvent = _metadataSink.Update(_state, position);
                }

                var title = GetTitle();
                var artist = GetArtist();

                if (title != _title || artist != _artist)
                {
                    Debug.WriteLine("AudioMetadataHandler.Update() set \"" + title + '"');

                    _title = title;
                    _artist = artist;

                    track.BeginEdit();

                    if (null != title)
                        track.Title = title;

                    if (null != artist)
                        track.Artist = artist;

                    track.EndEdit();
                }

                if (nextEvent.HasValue && nextEvent > position)
                {
                    if (nextEvent.Value != _lastReport)
                    {
                        _lastReport = nextEvent.Value;
                        ReportNextEvent(position, _lastReport);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("AudioMetadataHandler.Update() failed: " + ex.Message);
            }

            return TplTaskExtensions.CompletedTask;
        }

        void ReportNextEvent(TimeSpan position, TimeSpan nextEvent)
        {
            try
            {
                var player = BackgroundAudioPlayer.Instance;

                if (null == player)
                    return;

                var playerPosition = position;

                if (nextEvent <= playerPosition)
                {
                    _timer.Change(Timeout.Infinite, Timeout.Infinite);
                    Refresh();

                    return;
                }

                var state = player.PlayerState;

                if (PlayState.Playing == state)
                    _timer.Change((long)(nextEvent - playerPosition + TimeSpan.FromSeconds(0.5)).TotalMilliseconds, Timeout.Infinite);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("BackgroundAudioRun.UpdateTimer() failed: " + ex.ExtendedMessage());
            }
        }

        string GetTitle()
        {
            if (null != _state.TrackMetadata && !string.IsNullOrWhiteSpace(_state.TrackMetadata.Title))
                return _state.TrackMetadata.Title;

            if (null != _state.StreamMetadata && !string.IsNullOrWhiteSpace(_state.StreamMetadata.Name))
                return _state.StreamMetadata.Name;

            return DefaultTitle;
        }

        string GetArtist()
        {
            if (null != _state.TrackMetadata && !string.IsNullOrWhiteSpace(_state.TrackMetadata.Artist))
                return _state.TrackMetadata.Artist;

            return null;
        }

        public void Reset()
        {
            Debug.WriteLine("AudioMetadataHandler.Reset()");

            _timer.Change(Timeout.Infinite, Timeout.Infinite);

            lock (_lock)
            {
                _title = null;
                _artist = null;
                _metadataSink.Reset();
                _lastReport = TimeSpan.Zero;
            }

            _updateTask.Fire();
        }

        public void Refresh()
        {
            Debug.WriteLine("AudioMetadataHandler.Refresh()");

            _updateTask.Fire();
        }
    }
}

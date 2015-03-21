// -----------------------------------------------------------------------
//  <copyright file="MetadataHandler.cs" company="Henric Jungheim">
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
using Windows.Media;
using SM.Media.Metadata;
using SM.Media.Utility;

namespace SM.Media.BackgroundAudio
{
    sealed class MetadataHandler : IDisposable
    {
        readonly Func<TimeSpan> _getPosition;
        readonly object _lock = new object();
        readonly MetadataSink _metadataSink;
        readonly ForegroundNotifier _notifier;
        readonly Action<TimeSpan> _reportNextEvent;
        readonly MetadataState _state = new MetadataState();
        readonly SystemMediaTransportControls _systemMediaTransportControls;
        readonly SignalTask _updateTask;
        string _artist;
        string _defaultTitle;
        TimeSpan _lastReport = TimeSpan.Zero;
        string _title;

        public MetadataHandler(SystemMediaTransportControls systemMediaTransportControls, ForegroundNotifier notifier, Func<TimeSpan> getPosition, Action<TimeSpan> reportNextEvent, CancellationToken cancellationToken)
        {
            if (null == systemMediaTransportControls)
                throw new ArgumentNullException("systemMediaTransportControls");
            if (null == notifier)
                throw new ArgumentNullException("notifier");
            if (null == getPosition)
                throw new ArgumentNullException("getPosition");
            if (null == reportNextEvent)
                throw new ArgumentNullException("reportNextEvent");

            _systemMediaTransportControls = systemMediaTransportControls;
            _notifier = notifier;
            _getPosition = getPosition;
            _reportNextEvent = reportNextEvent;

            _updateTask = new SignalTask(Update, cancellationToken);

            _metadataSink = new ActionMetadataSink(_updateTask.Fire);
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
            _updateTask.Dispose();
        }

        #endregion

        Task Update()
        {
            Debug.WriteLine("SmtcMetadataHandler.Update()");

            try
            {
                var smtc = _systemMediaTransportControls;

                var position = _getPosition();

                TimeSpan? nextEvent = null;

                lock (_lock)
                {
                    nextEvent = _metadataSink.Update(_state, position);
                }

                var title = GetTitle();
                var artist = GetArtist();

                if (title != _title || artist != _artist)
                {
                    Debug.WriteLine("SmtcMetadataHandler.Update() set " + title);

                    _title = title;
                    _artist = artist;

                    smtc.DisplayUpdater.ClearAll();
                    smtc.DisplayUpdater.Type = MediaPlaybackType.Music;

                    var properties = smtc.DisplayUpdater.MusicProperties;

                    if (null != title)
                        properties.Title = title;

                    if (null != artist)
                        properties.Artist = artist;

                    smtc.DisplayUpdater.Update();

                    _notifier.Notify("track", title);
                }

                if (nextEvent.HasValue && nextEvent > position)
                {
                    if (nextEvent.Value != _lastReport)
                    {
                        _lastReport = nextEvent.Value;
                        _reportNextEvent(_lastReport);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SmtcMetadataSink.Update() failed: " + ex.Message);
            }

            return TplTaskExtensions.CompletedTask;
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
            Debug.WriteLine("SmtcMetadataHandler.Reset()");

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
            Debug.WriteLine("SmtcMetadataHandler.Refresh()");

            _updateTask.Fire();
        }
    }
}

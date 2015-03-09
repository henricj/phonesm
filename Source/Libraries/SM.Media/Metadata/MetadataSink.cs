// -----------------------------------------------------------------------
//  <copyright file="MetadataSink.cs" company="Henric Jungheim">
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
using System.Collections.Generic;
using System.Diagnostics;

namespace SM.Media.Metadata
{
    /// <summary>
    ///     Provide access to the track, segment, and stream metadata.  Do not block in
    ///     the implementation for any of these methods.
    /// </summary>
    public interface IMetadataSink
    {
        void ReportStreamMetadata(TimeSpan timestamp, IStreamMetadata streamMetadata);
        void ReportSegmentMetadata(TimeSpan timestamp, ISegmentMetadata segmentMetadata);
        void ReportTrackMetadata(ITrackMetadata trackMetadata);
    }

    public class MetadataState
    {
        public ISegmentMetadata SegmentMetadata { get; set; }
        public TimeSpan SegmentTimestamp { get; set; }

        public IStreamMetadata StreamMetadata { get; set; }
        public TimeSpan StreamTimestamp { get; set; }

        public ITrackMetadata TrackMetadata { get; set; }
    }

    public class MetadataSink : IMetadataSink
    {
        readonly object _lock = new object();
        readonly MetadataState _metadataState = new MetadataState();
        readonly Queue<ITrackMetadata> _pendingTracks = new Queue<ITrackMetadata>();
        TimeSpan _position;

        #region IMetadataSink Members

        public virtual void ReportStreamMetadata(TimeSpan timestamp, IStreamMetadata streamMetadata)
        {
            Debug.WriteLine("MetadataSink.ReportStreamMetadata() " + timestamp + " " + streamMetadata);

            lock (_lock)
            {
                _metadataState.StreamTimestamp = timestamp;
                _metadataState.StreamMetadata = streamMetadata;
                _metadataState.SegmentMetadata = null;
                _metadataState.TrackMetadata = null;
            }
        }

        public virtual void ReportSegmentMetadata(TimeSpan timestamp, ISegmentMetadata segmentMetadata)
        {
            Debug.WriteLine("MetadataSink.ReportSegmentMetadata() " + timestamp + " " + segmentMetadata);

            lock (_lock)
            {
                _metadataState.SegmentTimestamp = timestamp;
                _metadataState.SegmentMetadata = segmentMetadata;
            }
        }

        public virtual void ReportTrackMetadata(ITrackMetadata trackMetadata)
        {
            if (trackMetadata == null)
                throw new ArgumentNullException("trackMetadata");

            Debug.WriteLine("MetadataSink.ReportTrackMetadata() " + trackMetadata);

            if (!trackMetadata.TimeStamp.HasValue)
                throw new ArgumentException("A timestamp is required", "trackMetadata");
            if (trackMetadata.TimeStamp.Value < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException("trackMetadata", "The timestamp cannot be negative");

            lock (_lock)
            {
                _pendingTracks.Enqueue(trackMetadata);
            }
        }

        #endregion

        TimeSpan? ProcessPendingTracks()
        {
            while (_pendingTracks.Count > 0)
            {
                if (null == _metadataState.TrackMetadata)
                {
                    var track = _pendingTracks.Dequeue();

                    _metadataState.TrackMetadata = track;

                    Debug.Assert(track.TimeStamp.HasValue, "Invalid track metadata (no timestamp)");

                    continue;
                }

                var timeStamp = _metadataState.TrackMetadata.TimeStamp;

                if (timeStamp > _position)
                    return timeStamp;

                var pendingTrack = _pendingTracks.Peek();

                Debug.Assert(pendingTrack.TimeStamp.HasValue, "Invalid track metadata (no timestamp)");

                if (pendingTrack.TimeStamp > _position)
                    return pendingTrack.TimeStamp;

                var dequeueTrack = _pendingTracks.Dequeue();

                Debug.Assert(ReferenceEquals(pendingTrack, dequeueTrack), "Dequeue track mismatch");

                _metadataState.TrackMetadata = pendingTrack;
            }

            if (null != _metadataState.TrackMetadata)
            {
                var timeStamp = _metadataState.TrackMetadata.TimeStamp;

                Debug.Assert(timeStamp.HasValue, "Invalid track metadata (no timestamp)");

                if (timeStamp > _position)
                    return timeStamp;
            }

            return null;
        }

        public virtual void Reset()
        {
            lock (_lock)
            {
                _position = TimeSpan.Zero;

                _metadataState.StreamMetadata = null;
                _metadataState.StreamTimestamp = TimeSpan.Zero;
                _metadataState.SegmentMetadata = null;
                _metadataState.SegmentTimestamp = TimeSpan.Zero;
                _metadataState.TrackMetadata = null;

                _pendingTracks.Clear();
            }
        }

        public virtual TimeSpan? Update(MetadataState state, TimeSpan position)
        {
            lock (_lock)
            {
                _position = position;

                var nextEvent = ProcessPendingTracks();

                var streamMetadata = _metadataState.StreamMetadata;

                var segmentMetadata = _metadataState.SegmentMetadata;

                state.SegmentMetadata = segmentMetadata;
                state.SegmentTimestamp = _metadataState.SegmentTimestamp;
                state.StreamMetadata = streamMetadata;
                state.StreamTimestamp = _metadataState.StreamTimestamp;
                state.TrackMetadata = _metadataState.TrackMetadata;

                if (_metadataState.StreamTimestamp > _position && _metadataState.StreamTimestamp < nextEvent)
                    nextEvent = _metadataState.StreamTimestamp;

                if (_metadataState.SegmentTimestamp > _position && _metadataState.SegmentTimestamp < nextEvent)
                    nextEvent = _metadataState.SegmentTimestamp;

                return nextEvent;
            }
        }
    }

    public static class MetadataSinkExtensions
    {
        public static void SetParameter(this IMediaStreamFacadeBase mediaStreamFacade, IMetadataSink metadataSink)
        {
            mediaStreamFacade.Builder.RegisterSingleton(metadataSink);
        }
    }
}

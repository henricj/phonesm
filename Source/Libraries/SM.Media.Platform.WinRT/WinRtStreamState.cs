// -----------------------------------------------------------------------
//  <copyright file="WinRtStreamState.cs" company="Henric Jungheim">
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
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Media.Core;
using SM.Media.Content;
using SM.Media.Utility;
using SM.TsParser;

namespace SM.Media
{
    class WinRtStreamState
    {
        public readonly IMediaStreamDescriptor Descriptor;
        readonly ContentType _contentType;
        readonly string _name;
        readonly object _sampleLock = new object();
        readonly IStreamSource _streamSource;
        uint _bufferingProgress;
        MediaStreamSourceSampleRequestDeferral _deferral;
        bool _isClosed;
        uint _reportedBufferingProgress;
        MediaStreamSourceSampleRequest _request;

        public WinRtStreamState(string name, ContentType contentType, IStreamSource streamSource, IMediaStreamDescriptor descriptor)
        {
            if (null == name)
                throw new ArgumentNullException("name");
            if (null == contentType)
                throw new ArgumentNullException("contentType");
            if (null == streamSource)
                throw new ArgumentNullException("streamSource");
            if (null == descriptor)
                throw new ArgumentNullException("descriptor");

            _name = name;
            _contentType = contentType;
            _streamSource = streamSource;
            Descriptor = descriptor;
        }

        public string Name
        {
            get { return _name; }
        }

        public bool IsBuffering
        {
            get { return _streamSource.BufferingProgress.HasValue; }
        }

        public TimeSpan? PresentationTimestamp
        {
            get { return _streamSource.PresentationTimestamp; }
        }

        public ContentType ContentType
        {
            get { return _contentType; }
        }

        public void CheckForSamples()
        {
            MediaStreamSourceSampleRequestDeferral deferral = null;
            MediaStreamSourceSampleRequest request = null;

            try
            {
                lock (_sampleLock)
                {
                    deferral = _deferral;

                    if (null == deferral)
                        return;

                    _deferral = null;

                    request = _request;

                    if (null != request)
                        _request = null;

                    if (_isClosed)
                        return;
                }

                if (!TryCompleteRequest(request))
                    return;

                var localDeferral = deferral;

                request = null;
                deferral = null;

                localDeferral.Complete();
            }
            finally
            {
                if (null != deferral || null != request)
                {
                    lock (_sampleLock)
                    {
                        SmDebug.Assert(null == _deferral);
                        SmDebug.Assert(null == _request);

                        if (!_isClosed)
                        {
                            _deferral = deferral;
                            _request = request;

                            deferral = null;
                        }
                    }

                    if (null != deferral)
                        deferral.Complete();
                }
            }
        }

        bool TryCompleteRequest(MediaStreamSourceSampleRequest request)
        {
            if (_isClosed)
                return true;

            TsPesPacket packet = null;

            try
            {
                packet = _streamSource.GetNextSample();

                if (null == packet)
                {
                    if (_streamSource.IsEof)
                    {
                        //Debug.WriteLine("Sample {0} eof", _name);
                        return true;
                    }

                    if (_streamSource.BufferingProgress.HasValue)
                        _bufferingProgress = (uint)(Math.Round(100 * _streamSource.BufferingProgress.Value));
                    else
                        _bufferingProgress = 0;

                    if (_bufferingProgress != _reportedBufferingProgress)
                    {
                        //Debug.WriteLine("Sample {0} buffering {1}%", _name, _bufferingProgress);

                        request.ReportSampleProgress(_bufferingProgress);
                        _reportedBufferingProgress = _bufferingProgress;
                    }

                    return false;
                }

                _bufferingProgress = _reportedBufferingProgress = 100;

                var presentationTimestamp = packet.PresentationTimestamp;

#if WORKING_PROCESSED_EVENT
                var packetBuffer = packet.Buffer.AsBuffer(packet.Index, packet.Length);
#else
                // Make a copy of the buffer since Sample.Processed doesn't always seem to
                // get called.
                var packetBuffer = WindowsRuntimeBuffer.Create(packet.Buffer, packet.Index, packet.Length, packet.Length);
#endif

                var mediaStreamSample = MediaStreamSample.CreateFromBuffer(packetBuffer, presentationTimestamp);

                if (null == mediaStreamSample)
                    throw new InvalidOperationException("MediaStreamSamples cannot be null");

                if (packet.DecodeTimestamp.HasValue)
                    mediaStreamSample.DecodeTimestamp = packet.DecodeTimestamp.Value;

                if (packet.Duration.HasValue)
                    mediaStreamSample.Duration = packet.Duration.Value;

                //Debug.WriteLine("Sample {0} at {1}. duration {2} length {3}",
                //    _name, mediaStreamSample.Timestamp, mediaStreamSample.Duration, packet.Length);

                request.Sample = mediaStreamSample;

#if WORKING_PROCESSED_EVENT
                var localPacket = packet;

                request.Sample.Processed += (sender, args) => _streamSource.FreeSample(localPacket);

                // Prevent the .FreeSample() below from freeing this packet.
                packet = null;
#endif

                return true;
            }
            finally
            {
                if (null != packet)
                    _streamSource.FreeSample(packet);
            }
        }

        public void SampleRequested(MediaStreamSourceSampleRequest request)
        {
            //Debug.WriteLine("StreamState.SampleRequested() " + _name);

            SmDebug.Assert(null == _deferral);
            SmDebug.Assert(null == _request);

            if (TryCompleteRequest(request))
                return;

            var deferral = request.GetDeferral();

            lock (_sampleLock)
            {
                SmDebug.Assert(null == _deferral);
                SmDebug.Assert(null == _request);

                if (!_isClosed)
                {
                    _request = request;
                    _deferral = deferral;

                    deferral = null;
                }
            }

            if (null != deferral)
                deferral.Complete();
        }

        public void Cancel()
        {
            MediaStreamSourceSampleRequestDeferral deferral;

            lock (_sampleLock)
            {
                deferral = _deferral;

                if (null == deferral)
                    return;

                _deferral = null;
                _request = null;
            }

            if (null == deferral)
                return;

            deferral.Complete();
        }

        public void Close()
        {
            lock (_sampleLock)
                _isClosed = true;

            Cancel();
        }

        public bool DiscardPacketsBefore(TimeSpan value)
        {
            return _streamSource.DiscardPacketsBefore(value);
        }

        public override string ToString()
        {
            return string.Format(_name + " " + _contentType + " closed: " + _isClosed + " pending: " + (null != _deferral));
        }
    }
}

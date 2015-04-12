// -----------------------------------------------------------------------
//  <copyright file="AudioMediaParser.cs" company="Henric Jungheim">
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
using SM.Media.Configuration;
using SM.Media.MediaParser;
using SM.Media.Metadata;
using SM.Media.TransportStream.TsParser;
using SM.Media.TransportStream.TsParser.Utility;

namespace SM.Media.Audio
{
    public abstract class AudioMediaParser<TParser, TConfigurator> : MediaParserBase<TConfigurator>
        where TParser : class, IAudioParser
        where TConfigurator : IConfigurationSource
    {
        readonly IMetadataSink _metadataSink;
        protected TParser Parser;
        IAudioParser _audioParser;

        protected AudioMediaParser(TsStreamType streamType, TConfigurator configurator, ITsPesPacketPool pesPacketPool, IMetadataSink metadataSink)
            : base(streamType, configurator, pesPacketPool)
        {
            if (null == metadataSink)
                throw new ArgumentNullException("metadataSink");

            _metadataSink = metadataSink;
        }

        public override TimeSpan StartPosition
        {
            get { return Parser.StartPosition; }
            set { Parser.StartPosition = value; }
        }

        public override void InitializeStream(IStreamMetadata streamMetadata)
        {
            _metadataSink.ReportStreamMetadata(Parser.Position ?? TimeSpan.Zero, streamMetadata);
        }

        public override void StartSegment(ISegmentMetadata segmentMetadata)
        {
            _audioParser = Parser;

            var shoutcastMetadata = segmentMetadata as IShoutcastSegmentMetadata;

            if (null != shoutcastMetadata)
            {
                var icyMetaInt = shoutcastMetadata.IcyMetaInt;

                if (icyMetaInt.HasValue && icyMetaInt > 0)
                    _audioParser = new ShoutcastMetadataFilter(Parser, SetTrackMetadata, icyMetaInt.Value);
            }

            _metadataSink.ReportSegmentMetadata(Parser.Position ?? TimeSpan.Zero, segmentMetadata);
        }

        public override void SetTrackMetadata(ITrackMetadata trackMetadata)
        {
            _metadataSink.ReportTrackMetadata(trackMetadata);
        }

        public override void ProcessData(byte[] buffer, int offset, int length)
        {
            Debug.Assert(length > 0);
            Debug.Assert(offset + length <= buffer.Length);

            if (null == _audioParser)
                throw new InvalidOperationException("StartSegment has not been called");

            _audioParser.ProcessData(buffer, offset, length);

            PushStreams();
        }

        public override void FlushBuffers()
        {
            if (null != _audioParser)
                _audioParser.FlushBuffers();

            base.FlushBuffers();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                using (_audioParser)
                { }
            }

            base.Dispose(disposing);
        }
    }
}

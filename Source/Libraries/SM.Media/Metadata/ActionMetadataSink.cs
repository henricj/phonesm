// -----------------------------------------------------------------------
//  <copyright file="ActionMetadataSink.cs" company="Henric Jungheim">
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

namespace SM.Media.Metadata
{
    public class ActionMetadataSink : MetadataSink
    {
        readonly Action _updateAction;

        public ActionMetadataSink(Action updateAction)
        {
            if (null == updateAction)
                throw new ArgumentNullException("updateAction");

            _updateAction = updateAction;
        }

        public override void Reset()
        {
            base.Reset();

            _updateAction();
        }

        public override void ReportStreamMetadata(TimeSpan timestamp, IStreamMetadata streamMetadata)
        {
            base.ReportStreamMetadata(timestamp, streamMetadata);

            _updateAction();
        }

        public override void ReportSegmentMetadata(TimeSpan timestamp, ISegmentMetadata segmentMetadata)
        {
            base.ReportSegmentMetadata(timestamp, segmentMetadata);

            _updateAction();
        }

        public override void ReportTrackMetadata(ITrackMetadata trackMetadata)
        {
            base.ReportTrackMetadata(trackMetadata);

            _updateAction();
        }
    }
}

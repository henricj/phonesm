// -----------------------------------------------------------------------
//  <copyright file="DefaultBufferingPolicy.cs" company="Henric Jungheim">
//  Copyright (c) 2012-2014.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
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

namespace SM.Media.Buffering
{
    public class DefaultBufferingPolicy : IBufferingPolicy
    {
        int _bytesMaximum = 8192 * 1024;
        int _bytesMinimum = 300 * 1024;
        int _bytesMinimumStarting = 100 * 1024;
        TimeSpan _durationBufferingDone = TimeSpan.FromSeconds(9);
        TimeSpan _durationBufferingMax = TimeSpan.FromSeconds(20);
        TimeSpan _durationReadDisable = TimeSpan.FromSeconds(25);
        TimeSpan _durationReadEnable = TimeSpan.FromSeconds(12);
        TimeSpan _durationStartingDone = TimeSpan.FromSeconds(2.5);

        public int BytesMaximum
        {
            get { return _bytesMaximum; }
            set { _bytesMaximum = value; }
        }

        public int BytesMinimum
        {
            get { return _bytesMinimum; }
            set { _bytesMinimum = value; }
        }

        public int BytesMinimumStarting
        {
            get { return _bytesMinimumStarting; }
            set { _bytesMinimumStarting = value; }
        }

        public TimeSpan DurationReadEnable
        {
            get { return _durationReadEnable; }
            set { _durationReadEnable = value; }
        }

        public TimeSpan DurationBufferingDone
        {
            get { return _durationBufferingDone; }
            set { _durationBufferingDone = value; }
        }

        public TimeSpan DurationStartingDone
        {
            get { return _durationStartingDone; }
            set { _durationStartingDone = value; }
        }

        public TimeSpan DurationReadDisable
        {
            get { return _durationReadDisable; }
            set { _durationReadDisable = value; }
        }

        public TimeSpan DurationBufferingMax
        {
            get { return _durationBufferingMax; }
            set { _durationBufferingMax = value; }
        }

        #region IBufferingPolicy Members

        public virtual bool ShouldBlockReads(bool isReadBlocked, TimeSpan durationBuffered, int bytesBuffered, bool isExhausted, bool isAllExhausted)
        {
            if (isAllExhausted)
                return false;

            if (bytesBuffered > BytesMaximum)
                return true;

            if (isExhausted)
                return false;

            if (durationBuffered < DurationReadEnable)
                return false;

            if (durationBuffered > DurationReadDisable)
                return true;

            return isReadBlocked;
        }

        public virtual bool IsDoneBuffering(TimeSpan bufferDuration, int bytesBuffered, int bytesBufferedWhenExhausted, bool isStarting)
        {
            var bufferSize = Math.Max(0, bytesBuffered - bytesBufferedWhenExhausted);

            var durationDone = isStarting ? DurationStartingDone : DurationBufferingDone;
            var bytesMinimum = isStarting ? BytesMinimumStarting : BytesMinimum;

            return (bufferDuration >= durationDone && bufferSize >= bytesMinimum) || bytesBuffered >= BytesMaximum || bufferDuration > DurationBufferingMax;
        }

        public virtual float GetProgress(TimeSpan bufferDuration, int bytesBuffered, int bytesBufferedWhenExhausted, bool isStarting)
        {
            var durationDone = isStarting ? DurationStartingDone : DurationBufferingDone;
            var bytesMinimum = isStarting ? BytesMinimumStarting : BytesMinimum;

            var bufferSize = Math.Max(0, bytesBuffered - bytesBufferedWhenExhausted);

            var bufferingStatus1 = Math.Max(0, bufferDuration.Ticks / (float)durationDone.Ticks);
            var bufferingStatus2 = bufferSize / (float)bytesMinimum;
            var bufferingStatus3 = bytesBuffered / (float)BytesMaximum;
            var bufferingStatus4 = Math.Max(0, bufferDuration.Ticks / (float)DurationBufferingMax.Ticks);

            var bufferingStatus = Math.Max(Math.Max(Math.Min(bufferingStatus1, bufferingStatus2), bufferingStatus3), bufferingStatus4);

            if (bufferingStatus > 1.0f)
                bufferingStatus = 1.0f;
            else if (bufferingStatus < 0.0f)
                bufferingStatus = 0.0f;

            return bufferingStatus;
        }

        #endregion
    };
}

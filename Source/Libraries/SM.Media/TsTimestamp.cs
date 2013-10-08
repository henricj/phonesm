// -----------------------------------------------------------------------
//  <copyright file="TsTimestamp.cs" company="Henric Jungheim">
//  Copyright (c) 2012, 2013.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012, 2013 Henric Jungheim <software@henric.org>
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
using SM.TsParser;

namespace SM.Media
{
    public sealed class TsTimestamp : ITsTimestamp
    {
        TimeSpan? _timestampOffset;

        #region ITsTimestamp Members

        public TimeSpan StartPosition { get; set; }

        public TimeSpan? Offset
        {
            get { return _timestampOffset; }
        }

        public void Flush()
        {
            _timestampOffset = null;
        }

        public bool Update(TsPesPacket packet, bool isFirstPacketOfStream)
        {
            if (!isFirstPacketOfStream)
                return false;

            var startPosition = StartPosition;

            Debug.WriteLine("TsTimestamp.Update: Sync to start position {0} at {1}", startPosition, packet.PresentationTimestamp);

            var timestampOffset = packet.PresentationTimestamp - startPosition;

            if (!_timestampOffset.HasValue || timestampOffset < _timestampOffset)
            {
                _timestampOffset = timestampOffset;
                return true;
            }

            return false;
        }

        #endregion
    }
}

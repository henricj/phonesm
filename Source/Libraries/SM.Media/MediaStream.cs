// -----------------------------------------------------------------------
//  <copyright file="MediaStream.cs" company="Henric Jungheim">
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
using System.Collections.Generic;
using System.Diagnostics;
using SM.Media.Configuration;
using SM.Media.MediaParser;
using SM.TsParser;

namespace SM.Media
{
    public sealed class MediaStream : IMediaParserMediaStream, IDisposable
    {
        readonly IConfigurationSource _configurator;
        readonly Action<TsPesPacket> _freePacket;
        readonly List<TsPesPacket> _packets = new List<TsPesPacket>();
        readonly IStreamBuffer _streamBuffer;

        public MediaStream(IConfigurationSource configurator, IStreamBuffer streamBuffer, Action<TsPesPacket> freePacket)
        {
            if (null == streamBuffer)
                throw new ArgumentNullException("streamBuffer");
            if (null == freePacket)
                throw new ArgumentNullException("freePacket");

            _configurator = configurator;
            _streamBuffer = streamBuffer;
            _freePacket = freePacket;
        }

        public ICollection<TsPesPacket> Packets
        {
            get { return _packets; }
        }

        #region IDisposable Members

        public void Dispose()
        {
            Flush();
        }

        #endregion

        #region IMediaParserMediaStream Members

        public IConfigurationSource ConfigurationSource
        {
            get { return _configurator; }
        }

        public IStreamSource StreamSource
        {
            get { return _streamBuffer; }
        }

        #endregion

        public void Flush()
        {
            if (_packets.Count <= 0)
                return;

            foreach (var packet in _packets)
            {
                if (null == packet)
                    continue;

                _freePacket(packet);
            }

            _packets.Clear();
        }

        public void EnqueuePacket(TsPesPacket packet)
        {
            _packets.Add(packet);
        }

        public bool PushPackets()
        {
            //Debug.WriteLine("MediaStream.PushPackets() count {0} buffer: {1}", _packets.Count, _streamBuffer);

            if (_packets.Count <= 0)
                return false;

            if (!_streamBuffer.TryEnqueue(_packets))
            {
                Debug.WriteLine("MediaStream.PushPackets() the stream buffer was not ready to accept the packets: " + _streamBuffer);
                return false;
            }

            _packets.Clear();

            return true;
        }
    }
}

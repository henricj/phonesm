// -----------------------------------------------------------------------
//  <copyright file="H262StreamHandler.cs" company="Henric Jungheim">
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
using SM.Media.Configuration;
using SM.Media.Pes;
using SM.TsParser;
using SM.TsParser.Utility;

namespace SM.Media.H262
{
    public class H262StreamHandler : PesStreamHandler
    {
        readonly H262Configurator _configurator;
        readonly Action<TsPesPacket> _nextHandler;
        readonly ITsPesPacketPool _pesPacketPool;
        bool _foundframe;

        public H262StreamHandler(ITsPesPacketPool pesPacketPool, uint pid, TsStreamType streamType, Action<TsPesPacket> nextHandler)
            : base(pid, streamType)
        {
            if (null == pesPacketPool)
                throw new ArgumentNullException("pesPacketPool");
            if (null == nextHandler)
                throw new ArgumentNullException("nextHandler");

            _pesPacketPool = pesPacketPool;
            _nextHandler = nextHandler;
            _configurator = new H262Configurator(streamType.Description);
        }

        public override IConfigurationSource Configurator
        {
            get { return _configurator; }
        }

        public override void PacketHandler(TsPesPacket packet)
        {
            base.PacketHandler(packet);

            if (null == packet)
            {
                _nextHandler(null);

                return;
            }

            // Reject garbage packet
            if (packet.Length < 1)
            {
                _pesPacketPool.FreePesPacket(packet);
                return;
            }

            if (!_foundframe)
            {
                _configurator.Configure();
                _foundframe = true;
            }

            _nextHandler(packet);
        }
    }
}

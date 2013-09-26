// -----------------------------------------------------------------------
//  <copyright file="H262StreamHandler.cs" company="Henric Jungheim">
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
using SM.Media.Pes;
using SM.TsParser;

namespace SM.Media.H262
{
    class H262StreamHandler : PesStreamHandler
    {
        readonly Action<TsPesPacket> _nextHandler;
        readonly H262Configurator _configurator;
        bool _foundframe;

        public H262StreamHandler(uint pid, TsStreamType streamType, Action<TsPesPacket> nextHandler, H262Configurator configurator)
            : base(pid, streamType)
        {
            _nextHandler = nextHandler;
            _configurator = configurator;
        }

        public override void PacketHandler(TsPesPacket packet)
        {
            base.PacketHandler(packet);

            if (null == packet)
            {
                if (null != _nextHandler)
                    _nextHandler(null);

                return;
            }

            // Reject garbage packet
            if (packet.Length < 7)
                return;

            if (!_foundframe)
            {
                _configurator.Configure();
                _foundframe = true;
            }

            if (null != _nextHandler)
                _nextHandler(packet);
        }
    }
}

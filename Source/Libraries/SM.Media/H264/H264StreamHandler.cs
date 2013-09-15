// -----------------------------------------------------------------------
//  <copyright file="H264StreamHandler.cs" company="Henric Jungheim">
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

namespace SM.Media.H264
{
    public sealed class H264StreamHandler : PesStreamHandler
    {
        readonly IH264ConfiguratorSink _configuratorSink;
        readonly Action<TsPesPacket> _nextHandler;
        readonly NalUnitParser _parser;
        readonly RbspDecoder _rbspDecoder = new RbspDecoder();
        INalParser _currentParser;

        public H264StreamHandler(uint pid, TsStreamType streamType, Action<TsPesPacket> nextHandler, IH264ConfiguratorSink configuratorSink)
            : base(pid, streamType)
        {
            _nextHandler = nextHandler;
            _configuratorSink = configuratorSink;

            _parser = new NalUnitParser(ResolveHandler);
        }

        NalUnitParser.ParserStateHandler ResolveHandler(byte arg)
        {
            var nal_unit_type = arg & 0x1f;

            switch (nal_unit_type)
            {
                case 7: // SPS
                    _rbspDecoder.CompletionHandler = buffer => { _configuratorSink.SpsBytes = buffer; };
                    _currentParser = _rbspDecoder;
                    break;
                case 8: // PPS
                    _rbspDecoder.CompletionHandler = buffer => { _configuratorSink.PpsBytes = buffer; };
                    _currentParser = _rbspDecoder;
                    break;
                default:
                    _currentParser = null;
                    return null;
            }

            if (null == _currentParser)
                return null;

            return _currentParser.Parse;
        }

        public override void PacketHandler(TsPesPacket packet)
        {
            base.PacketHandler(packet);

            _parser.Reset();

            if (null == packet)
                _parser.Parse(null, 0, 0); // Propagate end-of-stream
            else
                _parser.Parse(packet.Buffer, packet.Index, packet.Length);

            if (null != _nextHandler)
                _nextHandler(packet);
        }
    }
}

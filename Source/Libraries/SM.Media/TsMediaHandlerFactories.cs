// -----------------------------------------------------------------------
//  <copyright file="TsMediaHandlerFactories.cs" company="Henric Jungheim">
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

using System.Collections.Generic;
using SM.Media.AAC;
using SM.Media.Ac3;
using SM.Media.H262;
using SM.Media.H264;
using SM.Media.MP3;
using SM.TsParser;

namespace SM.Media
{
    public static class TsMediaHandlerFactories
    {
        static readonly TsMediaParser.PacketHandlerFactory H262StreamHandlerFactory =
            (tsDecoder, pid, streamType, streamBuffer, nextHandler) =>
            {
                var configurator = new H262Configurator(streamType.Description);
                var streamHandler = new H262StreamHandler(pid, streamType, nextHandler, configurator);

                return new MediaStream(configurator, streamBuffer, streamHandler.PacketHandler);
            };

        static readonly TsMediaParser.PacketHandlerFactory Mp3StreamHandlerFactory =
            (tsDecoder, pid, streamType, streamBuffer, nextHandler) =>
            {
                var configurator = new Mp3Configurator(streamType.Description);
                var streamHandler = new Mp3StreamHandler(tsDecoder.PesPacketPool, pid, streamType, nextHandler, configurator.Configure);

                return new MediaStream(configurator, streamBuffer, streamHandler.PacketHandler);
            };

        static readonly TsMediaParser.PacketHandlerFactory AacStreamHandlerFactory =
            (tsDecoder, pid, streamType, streamBuffer, nextHandler) =>
            {
                var configurator = new AacConfigurator(streamType.Description);
                var streamHandler = new AacStreamHandler(tsDecoder.PesPacketPool, pid, streamType, nextHandler, configurator.Configure);

                return new MediaStream(configurator, streamBuffer, streamHandler.PacketHandler);
            };

        static readonly TsMediaParser.PacketHandlerFactory H264StreamHandlerFactory =
            (tsDecoder, pid, streamType, streamBuffer, nextHandler) =>
            {
                var configurator = new H264Configurator(streamType.Description);
                var streamHandler = new H264StreamHandler(pid, streamType, nextHandler, configurator);

                return new MediaStream(configurator, streamBuffer, streamHandler.PacketHandler);
            };

        static readonly TsMediaParser.PacketHandlerFactory Ac3StreamHandlerFactory =
            (tsDecoder, pid, streamType, streamBuffer, nextHandler) =>
            {
                var configurator = new Ac3Configurator(streamType.Description);
                var streamHandler = new Ac3StreamHandler(pid, streamType, nextHandler, configurator);

                return new MediaStream(configurator, streamBuffer, streamHandler.PacketHandler);
            };

        //    Table 2-34 Stream type assignments
        //    ISO/IEC 13818-1:2007/Amd.3:2009 (E)
        //    Rec. ITU-T H.222.0 (2006)/Amd.3 (03/2009)
        public static readonly IDictionary<byte, TsMediaParser.PacketHandlerFactory> DefaultFactories =
            new Dictionary<byte, TsMediaParser.PacketHandlerFactory>
            {
                { TsStreamType.H262StreamType, H262StreamHandlerFactory },
                { TsStreamType.Mp3Iso11172, Mp3StreamHandlerFactory },
                { TsStreamType.Mp3Iso13818, Mp3StreamHandlerFactory },
                { TsStreamType.H264StreamType, H264StreamHandlerFactory },
                { TsStreamType.AacStreamType, AacStreamHandlerFactory },
                { TsStreamType.Ac3StreamType, Ac3StreamHandlerFactory }
            };
    }
}

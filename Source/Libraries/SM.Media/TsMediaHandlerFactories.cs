//-----------------------------------------------------------------------
// <copyright file="TsMediaHandlerFactories.cs" company="Henric Jungheim">
// Copyright (c) 2012.
// <author>Henric Jungheim</author>
// </copyright>
//-----------------------------------------------------------------------
// Copyright (c) 2012 Henric Jungheim <software@henric.org> 
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
using SM.Media.H264;
using SM.Media.MP3;

namespace SM.Media
{
    public static class TsMediaHandlerFactories
    {
        static readonly MediaParser.PacketHandlerFactory Mp3StreamHandlerFactory =
            (pid, streamType, streamBuffer, nextHandler) =>
            {
                var configurator = new Mp3Configurator();
                var streamHandler = new Mp3StreamHandler(pid, streamType, nextHandler, configurator);

                return new MediaStream(configurator, streamBuffer, streamHandler.PacketHandler);
            };

        static readonly MediaParser.PacketHandlerFactory AacStreamHandlerFactory =
            (pid, streamType, streamBuffer, nextHandler) =>
            {
                var configurator = new AacConfigurator();
                var streamHandler = new AacStreamHandler(pid, streamType, nextHandler, configurator);

                return new MediaStream(configurator, streamBuffer, streamHandler.PacketHandler);
            };

        static readonly MediaParser.PacketHandlerFactory H264StreamHandlerFactory =
            (pid, streamType, streamBuffer, nextHandler) =>
            {
                var configurator = new H264Configurator();
                var streamHandler = new H264StreamHandler(pid, streamType, nextHandler, configurator);

                return new MediaStream(configurator, streamBuffer, streamHandler.PacketHandler);
            };

        public static IDictionary<byte, MediaParser.PacketHandlerFactory> DefaultFactories =
            new Dictionary<byte, MediaParser.PacketHandlerFactory>
            {
                { 0x03, Mp3StreamHandlerFactory },
                { 0x1b, H264StreamHandlerFactory },
                { 0x0f, AacStreamHandlerFactory }
            };
    }
}

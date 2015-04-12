// -----------------------------------------------------------------------
//  <copyright file="Mp3StreamHandler.cs" company="Henric Jungheim">
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

using SM.Media.Audio;
using SM.Media.TransportStream.TsParser;

namespace SM.Media.MP3
{
    public class Mp3StreamHandler : AudioStreamHandler
    {
        const int MinimumPacketSize = 24; // "Seen on the web somewhere..."   TODO: Verify this in the spec.

        const bool UseParser = true; // Have Mp3Parser parse the stream and submit frames to the OS.

        public Mp3StreamHandler(PesStreamParameters parameters)
            : base(parameters, new Mp3FrameHeader(), new Mp3Configurator(parameters.MediaStreamMetadata , parameters.StreamType.Description), MinimumPacketSize)
        {
            if (UseParser)
                Parser = new Mp3Parser(parameters.PesPacketPool, AudioConfigurator.Configure, NextHandler);
        }
    }
}

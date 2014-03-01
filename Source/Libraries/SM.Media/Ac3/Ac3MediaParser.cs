// -----------------------------------------------------------------------
//  <copyright file="Ac3MediaParser.cs" company="Henric Jungheim">
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

using SM.Media.Audio;
using SM.TsParser;
using SM.TsParser.Utility;

namespace SM.Media.Ac3
{
    public sealed class Ac3MediaParser : AudioMediaParser<Ac3Parser, Ac3Configurator>
    {
        static readonly TsStreamType StreamType = TsStreamType.FindStreamType(TsStreamType.Ac3StreamType);

        public Ac3MediaParser(ITsPesPacketPool pesPacketPool)
            : base(StreamType, new Ac3Configurator(), pesPacketPool)
        {
            Parser = new Ac3Parser(pesPacketPool, Configurator.Configure, SubmitPacket);
        }
    }
}

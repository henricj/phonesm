//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="Henric Jungheim">
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

using System;
using System.Net;
using SM.TsParser;

namespace TsDump
{
    class Program
    {
        static Action<TsPesPacket> _freePesHandler;

        static void FreePacket(TsPesPacket packet)
        {
            var h = _freePesHandler;

            if (null == h)
                return;

            h(packet);
        }

        static void Main(string[] args)
        {
            if (args.Length < 1)
                return;

            try
            {
                using (var parser = new SM.Media.TsMediaParser(null, (pid, streamType) => new PesStreamCopyHandler(pid, streamType, FreePacket).PacketHandler))
                {
                    _freePesHandler = parser.Decoder.FreePesPacket;

                    foreach (var arg in args)
                    {
                        Console.WriteLine("Reading {0}", arg);

                        var buffer = new byte[188 * 1024]; // new byte[16 * 1024];

                        using (var f = new WebClient().OpenRead(arg))
                        {
                            parser.Initialize();

                            var decoder = parser.Decoder;

                            var index = 0;
                            var eof = false;
                            var thresholdSize = buffer.Length - buffer.Length / 4;

                            while (!eof)
                            {
                                do
                                {
                                    var length = f.Read(buffer, index, buffer.Length - index);

                                    if (length < 1)
                                    {
                                        eof = true;
                                        break;
                                    }

                                    index += length;
                                } while (index < thresholdSize);

                                if (index > 0)
                                    decoder.Parse(buffer, 0, index);

                                index = 0;
                            }

                            decoder.ParseEnd();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}

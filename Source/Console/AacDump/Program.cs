// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Henric Jungheim">
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
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using SM.Media;
using SM.Media.AAC;
using SM.Media.Buffering;
using SM.Media.MediaParser;
using SM.Media.Utility;

namespace AacDump
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
                return;

            try
            {
                IStreamSource streamSource = null;

                using (var parser = new AacMediaParser(new NullBufferingManager(), new BufferPool(new DefaultBufferPoolParameters { BaseSize = 64 * 1024, Pools = 2}),
                    () =>
                    {
                        if (null == streamSource)
                            return;

                        for (; ; )
                        {
                            var packet = streamSource.GetNextSample();

                            if (null == packet)
                            {
                                if (streamSource.IsEof)
                                { }

                                return;
                            }

                            Console.WriteLine("{0} {1} {2}", packet.PresentationTimestamp, packet.Duration, packet.Length);

                            for (var i = 0; i < packet.Length; ++i)
                            {
                                if (i > 0 && 0 == (i & 0x03))
                                    Console.Write(0 == (i & 0x1f) ? '\n' : ' ');

                                Console.Write(packet.Buffer[packet.Index + i].ToString("x2"));
                            }

                            Console.WriteLine();

                            streamSource.FreeSample(packet);
                        }
                    }))
                {
                    parser.MediaStream.ConfigurationComplete += (sender, eventArgs) => streamSource = eventArgs.StreamSource;

                    foreach (var arg in args)
                    {
                        Console.WriteLine("Reading {0}", arg);

                        ReadAsync(arg, parser).Wait();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        static Task<Stream> OpenAsync(string path)
        {
            var uri = new Uri(path, UriKind.RelativeOrAbsolute);

            if (uri.IsAbsoluteUri)
                return new HttpClient().GetStreamAsync(uri);

            Stream s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 16384, true);

            return Task.FromResult(s);
        }

        static async Task ReadAsync(string arg, IMediaParser parser)
        {
            var buffer = new byte[16 * 1024];

            using (var f = await OpenAsync(arg).ConfigureAwait(false))
            {
                parser.Initialize();

                var index = 0;
                var eof = false;
                var thresholdSize = buffer.Length - buffer.Length / 4;

                while (!eof)
                {
                    do
                    {
                        var length = await f.ReadAsync(buffer, index, buffer.Length - index).ConfigureAwait(false);

                        if (length < 1)
                        {
                            eof = true;
                            break;
                        }

                        index += length;
                    } while (index < thresholdSize);

                    if (index > 0)
                        parser.ProcessData(buffer, 0, index);

                    index = 0;
                }

                parser.ProcessEndOfData();
            }
        }
    }
}

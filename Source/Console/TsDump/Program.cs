// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Henric Jungheim">
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
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using SM.Media;
using SM.TsParser;

namespace TsDump
{
    class NullBufferingManager : IBufferingManager
    {
        static readonly IBufferingQueue Queue = new NullBufferingQueue();

        #region IBufferingManager Members

        public double BufferingProgress
        {
            get { return 1; }
        }

        public bool IsBuffering
        {
            get { return false; }
        }

        public IBufferingQueue CreateQueue(IManagedBuffer managedBuffer)
        {
            return Queue;
        }

        public void Flush()
        { }

        public bool IsSeekAlreadyBuffered(TimeSpan position)
        {
            return true;
        }

        #endregion

        #region Nested type: NullBufferingQueue

        class NullBufferingQueue : IBufferingQueue
        {
            #region IBufferingQueue Members

            public void ReportEnqueue(int size, TimeSpan timestamp)
            { }

            public void ReportDequeue(int size, TimeSpan timestamp)
            { }

            public void ReportFlush()
            { }

            public void ReportExhaustion()
            { }

            public void ReportDone()
            { }

            #endregion
        }

        #endregion
    }

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
                using (var parser = new TsMediaParser(new NullBufferingManager(), () => { }, _ => { },
                    (pid, streamType) => new PesStreamCopyHandler(pid, streamType, FreePacket).PacketHandler))
                {
                    _freePesHandler = parser.Decoder.FreePesPacket;

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

        static async Task ReadAsync(string arg, TsMediaParser parser)
        {
            var buffer = new byte[188 * 1024]; // new byte[16 * 1024];

            using (var f = await OpenAsync(arg).ConfigureAwait(false))
            {
                parser.Initialize(ProgramStreamsHandler);

                var decoder = parser.Decoder;

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
                        decoder.Parse(buffer, 0, index);

                    index = 0;
                }

                decoder.ParseEnd();
            }
        }

        static void ProgramStreamsHandler(IProgramStreams programStreams)
        {
            Console.WriteLine("Program: " + programStreams.ProgramNumber);

            foreach (var s in programStreams.Streams)
                Console.WriteLine("   {0}({1}): {2}", s.StreamType.Contents, s.Pid, s.StreamType.Description);
        }
    }
}

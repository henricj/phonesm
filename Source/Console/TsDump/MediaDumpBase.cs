// -----------------------------------------------------------------------
//  <copyright file="MediaDumpBase.cs" company="Henric Jungheim">
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
using System.Text;
using System.Threading.Tasks;
using SM.Media.Buffering;
using SM.Media.MediaParser;
using SM.Media.Utility;
using SM.TsParser;
using SM.TsParser.Utility;

namespace TsDump
{
    class MediaDumpBase : IDisposable
    {
        protected readonly IBufferPool BufferPool;
        protected readonly ITsPesPacketPool PacketPool;
        readonly IBufferingManager _bufferingManager;
        readonly Action<IProgramStreams> _programStreamsHandler;
        readonly SignalTask _streamReader;
        protected IMediaParser Parser;
        HttpClient _httpClient;
        Stream _stream;

        protected MediaDumpBase(Action<IProgramStreams> programStreamsHandler)
        {
            _programStreamsHandler = programStreamsHandler;
            BufferPool = new BufferPool(new DefaultBufferPoolParameters
                                        {
                                            BaseSize = 5 * 64 * 1024,
                                            Pools = 2
                                        });

            PacketPool = new TsPesPacketPool(BufferPool);
            _bufferingManager = new NullBufferingManager(PacketPool);

            _streamReader = new SignalTask(ReadStreams);
        }

        #region IDisposable Members

        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        #endregion

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                using (_bufferingManager)
                using (BufferPool)
                using (PacketPool)
                using (Parser)
                using (_streamReader)
                using (_stream)
                using (_httpClient)
                { }
            }
        }

        void CheckForSample()
        {
            _streamReader.Fire();
        }

        Task ReadStreams()
        {
            for (; ; )
            {
                var noData = true;

                foreach (var stream in Parser.MediaStreams)
                {
                    if (!stream.ConfigurationSource.IsConfigured)
                        continue;

                    if (DumpStream(stream))
                        noData = false;
                }

                if (noData)
                    return TplTaskExtensions.CompletedTask;
            }
        }

        static bool DumpStream(IMediaParserMediaStream stream)
        {
            var streamSource = stream.StreamSource;

            if (null == streamSource)
                return false;

            Console.WriteLine("Stream " + stream.ConfigurationSource.Name);

            var sawData = false;

            var sb = new StringBuilder();

            for (; ; )
            {
                var packet = streamSource.GetNextSample();

                if (null == packet)
                {
                    if (streamSource.IsEof)
                        Console.WriteLine("EOF");

                    return sawData;
                }

                sawData = true;

                sb.AppendFormat("{0}/{1} {2} {3}", packet.PresentationTimestamp, packet.DecodeTimestamp, packet.Duration, packet.Length);
                sb.AppendLine();

                for (var i = 0; i < packet.Length; ++i)
                {
                    if (i > 0 && 0 == (i & 0x03))
                    {
                        if (0 == (i & 0x1f))
                            sb.AppendLine();
                        else
                            sb.Append(' ');
                    }

                    sb.Append(packet.Buffer[packet.Index + i].ToString("x2"));
                }

                Console.WriteLine(sb);
                sb.Clear();

                streamSource.FreeSample(packet);
            }
        }

        Task<Stream> OpenAsync(string path)
        {
            var uri = new Uri(path, UriKind.RelativeOrAbsolute);

            if (uri.IsAbsoluteUri)
            {
                _httpClient = new HttpClient();

                return _httpClient.GetStreamAsync(uri);
            }

            _stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 16384, true);

            return Task.FromResult(_stream);
        }

        public async Task ReadAsync(string source)
        {
            Parser.Initialize(_bufferingManager, _programStreamsHandler);

            var buffer = new byte[16 * 1024];

            using (var f = await OpenAsync(source).ConfigureAwait(false))
            {
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
                    {
                        Parser.ProcessData(buffer, 0, index);

                        CheckForSample();
                    }

                    index = 0;
                }

                Parser.ProcessEndOfData();

                CheckForSample();
            }
        }

        public async Task CloseAsync()
        {
            await _streamReader.WaitAsync().ConfigureAwait(false);

            Parser.FlushBuffers();
        }
    }
}

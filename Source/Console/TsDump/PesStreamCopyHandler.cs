// -----------------------------------------------------------------------
//  <copyright file="PesStreamCopyHandler.cs" company="Henric Jungheim">
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
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using SM.Media.Configuration;
using SM.Media.Pes;
using SM.TsParser;

namespace TsDump
{
    sealed class PesStreamCopyHandler : PesStreamHandler, IDisposable
    {
        static readonly byte[] FakeBuffer = new byte[0];
        readonly IConfigurationSource _configurator = new NullConfigurator();
        readonly HashAlgorithm _hash = SHA256.Create();
        readonly Action<TsPesPacket> _nextHandler;
        readonly Stream _stream;
        byte[] _streamHash;
        TimeSpan _timestamp;

        public PesStreamCopyHandler(uint pid, TsStreamType streamType, Action<TsPesPacket> nextHandler)
            : base(pid, streamType)
        {
            _nextHandler = nextHandler;

            var ext = streamType.FileExtension;

            if (string.IsNullOrWhiteSpace(ext))
                ext = "_" + streamType.StreamType.ToString("x2") + ".bin";

            _stream = File.Create(string.Format("TS_PID{0}{1}", pid, ext));
        }

        public override IConfigurationSource Configurator
        {
            get { return _configurator; }
        }

        #region IDisposable Members

        /// <summary>
        ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            using (_stream)
            { }

            using (_hash)
            { }
        }

        #endregion

        public override void PacketHandler(TsPesPacket packet)
        {
            if (null == packet)
            {
                if (null != _stream)
                    _stream.Close();

                _hash.TransformFinalBlock(FakeBuffer, 0, 0);

                _streamHash = _hash.Hash;

                Console.WriteLine("PID{0} {1}: {2}", Pid, StreamType.Contents, StreamType.Description);
                Console.WriteLine("Hash:");

                var count = 0;
                foreach (var b in _streamHash)
                {
                    Console.Write(b.ToString("x2"));
                    ++count;

                    if (0 == (count & 3))
                    {
                        if (0 == (count & 0x0f))
                            Console.WriteLine();
                        else
                            Console.Write(' ');
                    }
                }
            }
            else
            {
                Task t = null;

                if (null != _stream)
                    t = _stream.WriteAsync(packet.Buffer, packet.Index, packet.Length);

                _hash.TransformBlock(packet.Buffer, packet.Index, packet.Length, null, 0);

                if (packet.PresentationTimestamp < _timestamp)
                    Debug.WriteLine("Timestamp did not increase {0} -> {1}", _timestamp, packet.PresentationTimestamp);

                _timestamp = packet.PresentationTimestamp;

                if (null != t)
                    t.Wait();
            }

            var h = _nextHandler;

            if (null != h)
                h(packet);
        }
    }
}

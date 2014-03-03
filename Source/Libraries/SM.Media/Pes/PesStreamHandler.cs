// -----------------------------------------------------------------------
//  <copyright file="PesStreamHandler.cs" company="Henric Jungheim">
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
using SM.Media.Configuration;
using SM.TsParser;

namespace SM.Media.Pes
{
    public abstract class PesStreamHandler : IPesStreamHandler
    {
        protected readonly uint Pid;
        protected readonly TsStreamType StreamType;

        protected PesStreamHandler(uint pid, TsStreamType streamType)
        {
            StreamType = streamType;
            Pid = pid;
        }

        public abstract IConfigurationSource Configurator { get; }

        #region IPesStreamHandler Members

        public virtual void PacketHandler(TsPesPacket packet)
        {
            return;

#pragma warning disable 162
            if (null == packet)
                Debug.WriteLine("PES {0}/{1} End-of-stream", StreamType.Contents, Pid);
            else
            {
#if DEBUG
                Debug.WriteLine("PES({0}) {1}/{2} PTS {3} Length {4}",
                    packet.PacketId,
                    StreamType.Contents, Pid,
                    packet.PresentationTimestamp, packet.Length);
#endif
            }
#pragma warning restore 162
        }

        #endregion

        public virtual TimeSpan? GetDuration(TsPesPacket packet)
        {
            return packet.Duration;
        }
    }
}

// -----------------------------------------------------------------------
//  <copyright file="MediaParserBase.cs" company="Henric Jungheim">
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using SM.Media.Configuration;
using SM.TsParser;
using SM.TsParser.Utility;

namespace SM.Media.MediaParser
{
    public abstract class MediaParserBase<TConfigurator> : IMediaParser
        where TConfigurator : IConfigurationSource
    {
        readonly TConfigurator _configurator;
        readonly ITsPesPacketPool _pesPacketPool;
        readonly TsStreamType _streamType;
        int _isDisposed;
        ICollection<IMediaParserMediaStream> _mediaStreams;
        StreamBuffer _streamBuffer;

        protected MediaParserBase(ITsPesPacketPool pesPacketPool, TsStreamType streamType, TConfigurator configurator)
        {
            if (null == pesPacketPool)
                throw new ArgumentNullException("pesPacketPool");
            if (null == streamType)
                throw new ArgumentNullException("streamType");
            if (ReferenceEquals(default(TConfigurator), configurator))
                throw new ArgumentNullException("configurator");

            _pesPacketPool = pesPacketPool;
            _streamType = streamType;
            _configurator = configurator;

            _configurator.ConfigurationComplete += OnConfigurationComplete;
        }

        protected TConfigurator Configurator
        {
            get { return _configurator; }
        }

        #region IMediaParser Members

        public void Dispose()
        {
            if (0 != Interlocked.Exchange(ref _isDisposed, 1))
                return;

            Dispose(true);

            GC.SuppressFinalize(this);
        }

        public ICollection<IMediaParserMediaStream> MediaStreams
        {
            get { return _mediaStreams; }
        }

        public bool EnableProcessing { get; set; }
        public virtual TimeSpan StartPosition { get; set; }
        public event EventHandler ConfigurationComplete;

        public virtual void ProcessEndOfData()
        {
            FlushBuffers();

            SubmitPacket(null);
        }

        public abstract void ProcessData(byte[] buffer, int offset, int length);

        public abstract void FlushBuffers();

        public void Initialize(Func<TsStreamType, Action<TsPesPacket>, StreamBuffer> streamBufferFactory, Action<IProgramStreams> programStreamsHandler = null)
        {
            if (streamBufferFactory == null)
                throw new ArgumentNullException("streamBufferFactory");

            _streamBuffer = streamBufferFactory(_streamType, _pesPacketPool.FreePesPacket);

            _mediaStreams = new[] { new MediaStream(_configurator, _streamBuffer) };
        }

        #endregion

        void OnConfigurationComplete(object sender, EventArgs eventArgs)
        {
            _configurator.ConfigurationComplete -= OnConfigurationComplete;

            var occ = ConfigurationComplete;

            if (null == occ)
                return;

            occ(this, EventArgs.Empty);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            if (!Equals(default(TConfigurator), _configurator))
                _configurator.ConfigurationComplete -= OnConfigurationComplete;

            if (null != ConfigurationComplete)
            {
                Debug.WriteLine("MediaParserBase<>.Dispose(bool) ConfigurationComplete event is still subscribed");
                ConfigurationComplete = null;
            }

            using (_streamBuffer)
            { }
        }

        protected void SubmitPacket(TsPesPacket packet)
        {
            _streamBuffer.Enqueue(packet);
        }
    }
}

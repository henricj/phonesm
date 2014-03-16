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
using SM.Media.Buffering;
using SM.Media.Configuration;
using SM.TsParser;
using SM.TsParser.Utility;

namespace SM.Media.MediaParser
{
    public abstract class MediaParserBase<TConfigurator> : IMediaParser
        where TConfigurator : IConfigurationSource
    {
        readonly TConfigurator _configurator;
        readonly TsStreamType _streamType;
        readonly ITsPesPacketPool _tsPesPacketPool;
        IBufferingManager _bufferingManager;
        int _isDisposed;
        MediaStream _mediaStream;
        ICollection<IMediaParserMediaStream> _mediaStreams;
        IStreamBuffer _streamBuffer;

        protected MediaParserBase(TsStreamType streamType, TConfigurator configurator, ITsPesPacketPool tsPesPacketPool)
        {
            if (null == streamType)
                throw new ArgumentNullException("streamType");
            if (ReferenceEquals(default(TConfigurator), configurator))
                throw new ArgumentNullException("configurator");
            if (null == tsPesPacketPool)
                throw new ArgumentNullException("tsPesPacketPool");

            _streamType = streamType;
            _configurator = configurator;
            _tsPesPacketPool = tsPesPacketPool;

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

            _mediaStream.PushPackets();

            _bufferingManager.ReportEndOfData();
        }

        public abstract void ProcessData(byte[] buffer, int offset, int length);

        public virtual void FlushBuffers()
        {
            _mediaStream.Flush();
        }

        public void Initialize(IBufferingManager bufferingManager, Action<IProgramStreams> programStreamsHandler = null)
        {
            if (null == bufferingManager)
                throw new ArgumentNullException("bufferingManager");

            _bufferingManager = bufferingManager;

            _streamBuffer = bufferingManager.CreateStreamBuffer(_streamType);

            _mediaStream = new MediaStream(_configurator, _streamBuffer, _tsPesPacketPool.FreePesPacket);

            _mediaStreams = new[] { _mediaStream };
        }

        #endregion

        protected virtual bool PushStreams()
        {
            if (!_mediaStream.PushPackets())
                return false;

            _bufferingManager.Refresh();

            return true;
        }

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

            using (_mediaStream)
            { }

            _mediaStreams = null;
            _mediaStream = null;
            _bufferingManager = null;
            _streamBuffer = null;
        }

        protected void SubmitPacket(TsPesPacket packet)
        {
            _mediaStream.EnqueuePacket(packet);
        }
    }
}

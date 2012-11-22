//-----------------------------------------------------------------------
// <copyright file="MediaStream.cs" company="Henric Jungheim">
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
using SM.Media.Configuration;
using SM.TsParser;

namespace SM.Media
{
    public sealed class MediaStream : IMediaParserMediaStream, IStreamHandler
    {
        readonly ConfigurationEventArgs _configurationEventArgs;
        readonly IConfigurationSource _configurator;
        readonly Action<TsPesPacket> _packetHandler;
        readonly IStreamSource _streamBuffer;

        public MediaStream(IConfigurationSource configurator, IStreamSource streamBuffer, Action<TsPesPacket> packetHandler)
        {
            _streamBuffer = streamBuffer;
            _configurator = configurator;
            _packetHandler = packetHandler;

            configurator.ConfigurationComplete += ConfiguratorOnConfigurationComplete;

            _configurationEventArgs = new ConfigurationEventArgs(configurator, _streamBuffer);
        }

        #region IMediaParserMediaStream Members

        public event EventHandler<ConfigurationEventArgs> ConfigurationComplete;

        public void Dispose()
        {
            _configurator.ConfigurationComplete -= ConfiguratorOnConfigurationComplete;

            using (_streamBuffer as IDisposable)
            { }
        }

        #endregion

        #region IStreamHandler Members

        public IConfigurationSource ConfigurationSource
        {
            get { return _configurator; }
        }

        public Action<TsPesPacket> PacketHandler
        {
            get { return _packetHandler; }
        }

        #endregion

        void ConfiguratorOnConfigurationComplete(object sender, EventArgs eventArgs)
        {
            var cc = ConfigurationComplete;

            if (null == cc)
                return;

            cc(this, _configurationEventArgs);
        }
    }
}

//-----------------------------------------------------------------------
// <copyright file="PesHandlers.cs" company="Henric Jungheim">
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
using System.Collections.Generic;
using System.Diagnostics;
using SM.TsParser;

namespace SM.Media.Pes
{
    public sealed class PesHandlers : IDisposable
    {
        readonly Dictionary<uint, Action<TsPesPacket>> _handlers = new Dictionary<uint, Action<TsPesPacket>>();

        readonly Dictionary<byte, Func<uint, TsStreamType, Action<TsPesPacket>>> _pesStreamHandlerFactory =
            new Dictionary<byte, Func<uint, TsStreamType, Action<TsPesPacket>>>();

        readonly Func<uint, TsStreamType, Action<TsPesPacket>> _handlerFactory;

        public PesHandlers(Func<uint, TsStreamType, Action<TsPesPacket>> handlerFactory)
        {
            _handlerFactory = handlerFactory;
        }

        #region IDisposable Members

        /// <summary>
        ///   Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            CleanupHandlers();
        }

        #endregion

        public void Initialize()
        {
            CleanupHandlers();
        }

        void CleanupHandlers()
        {
            if (null == _handlers)
                return;

            _handlers.Clear();
        }

        public void RegisterHandlerFactory(byte streamType, Func<uint, TsStreamType, Action<TsPesPacket>> handlerFactory)
        {
            _pesStreamHandlerFactory[streamType] = handlerFactory;
        }

        public Action<TsPesPacket> GetPesHandler(uint pid, TsStreamType streamType)
        {
            Action<TsPesPacket> handler;

            if (_handlers.TryGetValue(pid, out handler))
            {
                Debug.WriteLine("Found PES {0} stream ({1}) with PID {2}", streamType.Contents, streamType.Description, pid);
                return handler;
            }

            Debug.WriteLine("Create PES {0} stream ({1}) with PID {2}", streamType.Contents, streamType.Description, pid);

            handler = _handlerFactory(pid, streamType);

            _handlers[pid] = handler;

            return handler;
        }
    }
}

// -----------------------------------------------------------------------
//  <copyright file="SocketWrapper.cs" company="Henric Jungheim">
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
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SM.Media.Web.HttpConnection
{
    public sealed class SocketWrapper : ISocket
    {
        Socket _socket;
        Uri _url;

        #region ISocket Members

        public void Dispose()
        {
            Close();
        }

        public Task ConnectAsync(Uri url, CancellationToken cancellationToken)
        {
            _url = url;

            var args = new SocketAsyncEventArgs
            {
                RemoteEndPoint = new DnsEndPoint(url.Host, url.Port)
            };

            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            if (null != Interlocked.CompareExchange(ref _socket, socket, null))
            {
                socket.Dispose();
                throw new InvalidOperationException("The socket is in use");
            }

            return DoAsync(_socket.ConnectAsync, args, cancellationToken);
        }

        public async Task<int> WriteAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
        {
            var args = new SocketAsyncEventArgs
            {
                BufferList = new[] { new ArraySegment<byte>(buffer, offset, length) }
            };

            await DoAsync(_socket.SendAsync, args, cancellationToken).ConfigureAwait(false);

            return args.BytesTransferred;
        }

        public async Task<int> ReadAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
        {
            var args = new SocketAsyncEventArgs
            {
                BufferList = new[] { new ArraySegment<byte>(buffer, offset, length) }
            };

            await DoAsync(_socket.ReceiveAsync, args, cancellationToken).ConfigureAwait(false);

            return args.BytesTransferred;
        }

        public void Close()
        {
            var socket = Interlocked.Exchange(ref _socket, null);

            if (null != socket)
                socket.Dispose();
        }

        #endregion

        Task DoAsync(Func<SocketAsyncEventArgs, bool> op, SocketAsyncEventArgs args, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<object>();

            var cancelRegistration = cancellationToken.Register(() => Socket.CancelConnectAsync(args));

            EventHandler<SocketAsyncEventArgs> completedHandler = (sender, eventArgs) =>
            {
                cancelRegistration.Dispose();

                if (SocketError.Success != args.SocketError)
                    tcs.TrySetException(new WebException("Socket to " + _url + " failed: " + args.SocketError));
                else
                    tcs.TrySetResult(null);
            };

            args.Completed += completedHandler;

            if (!op(args))
                completedHandler(_socket, args);

            return tcs.Task;
        }
    }
}

// -----------------------------------------------------------------------
//  <copyright file="StreamSocketWrapper.cs" company="Henric Jungheim">
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
using System.Globalization;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace SM.Media.Web.HttpConnection
{
    public sealed class StreamSocketWrapper : ISocket
    {
        StreamSocket _socket;

        #region ISocket Members

        public void Dispose()
        {
            Close();
        }

        public async Task ConnectAsync(Uri url, CancellationToken cancellationToken)
        {
            var host = url.Host;
            var serviceName = url.Port.ToString(CultureInfo.InvariantCulture);
            var hostName = new HostName(host);

            var useTls = string.Equals("HTTPS", url.Scheme, StringComparison.OrdinalIgnoreCase);

            if (!useTls && !string.Equals("HTTP", url.Scheme, StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("Scheme not supported: " + url.Scheme);

            var socket = new StreamSocket();

            if (null != Interlocked.CompareExchange(ref _socket, socket, null))
            {
                socket.Dispose();
                throw new InvalidOperationException("The socket is in use");
            }

            try
            {
                socket.Control.NoDelay = true;

#if WINDOWS_PHONE8
                var protectionLevel = useTls ? SocketProtectionLevel.Ssl : SocketProtectionLevel.PlainSocket;
#else
                var protectionLevel = useTls ? SocketProtectionLevel.Tls12 : SocketProtectionLevel.PlainSocket;
#endif

                await socket.ConnectAsync(hostName, serviceName, protectionLevel)
                    .AsTask(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception)
            {
                Close();

                throw;
            }

            //if (useTls)
            //{
            //    var cert = socket.Information.ServerCertificate;
            //    var certFingerprint = cert.GetHashValue("SHA256");
            //}
        }

        public async Task<int> WriteAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
        {
            var socket = _socket;

            if (null == socket)
                throw new InvalidOperationException("The socket is not open");

            var iBuffer = buffer.AsBuffer(offset, length);

            return (int)await socket.OutputStream.WriteAsync(iBuffer).AsTask(cancellationToken).ConfigureAwait(false);
        }

        public async Task<int> ReadAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
        {
            var socket = _socket;

            if (null == socket)
                throw new InvalidOperationException("The socket is not open");

            var iBuffer = buffer.AsBuffer(offset, 0, length);

            var iBuffer2 = await socket.InputStream.ReadAsync(iBuffer, (uint)length, InputStreamOptions.Partial).AsTask(cancellationToken).ConfigureAwait(false);

            var bytesRead = (int)iBuffer2.Length;

            //Debug.WriteLine("ReadSocketAsync() {0}/{1}", bytesRead, length);

            if (bytesRead <= 0)
                return 0;

            if (ReferenceEquals(iBuffer, iBuffer2))
                return bytesRead;

            Debug.Assert(bytesRead <= length, "Length out-of-range");

            iBuffer2.CopyTo(0, buffer, offset, bytesRead);

            return bytesRead;
        }

        public void Close()
        {
            var socket = Interlocked.Exchange(ref _socket, null);

            if (null != socket)
                socket.Dispose();
        }

        #endregion
    }
}

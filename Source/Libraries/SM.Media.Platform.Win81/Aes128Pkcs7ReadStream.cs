// -----------------------------------------------------------------------
//  <copyright file="Aes128Pkcs7ReadStream.cs" company="Henric Jungheim">
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
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Security.Cryptography.Core;
using Windows.Storage.Streams;

namespace SM.Media
{
    public class CbcReadBuffer
    {
        readonly string _algorithmName;
        byte[] _decryptedData;
        int _decryptedLength;
        int _decryptedOffset;
        byte[] _encryptedData;
        byte[] _iv;

        public CbcReadBuffer(string algorithmName, string algorithmLastName, byte[] iv = null)
        {
            if (algorithmName == null)
                throw new ArgumentNullException("algorithmName");
            if (algorithmLastName == null)
                throw new ArgumentNullException("algorithmLastName");

            _algorithmName = algorithmName;
            _iv = iv;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
            //if (null != _decryptedData) 
        }
    }

    public class Aes128Pkcs7ReadStream : Stream
    {
        readonly int _blockLength;
        readonly byte[] _encrypted = new byte[16 * 1024];
        readonly IBuffer _encryptedBuffer;
        readonly IBuffer _ivBuffer;
        readonly CryptographicKey _key;
        readonly CryptographicKey _lastKey;
        readonly Stream _parent;
        int _bytesRead;
        int _count;
        int _dataLength;
        int _dataOffset;
        byte[] _decryptedData;
        int _offset;

        public Aes128Pkcs7ReadStream(Stream parent, byte[] key, byte[] iv)
        {
            if (parent == null)
                throw new ArgumentNullException("parent");

            if (key == null)
                throw new ArgumentNullException("key");

            if (!parent.CanRead)
                throw new ArgumentException("parent must be a readable stream");

            _parent = parent;
            var keyBuffer = key.AsBuffer();
            _ivBuffer = iv.AsBuffer();

            var algorithm = SymmetricKeyAlgorithmProvider.OpenAlgorithm(SymmetricAlgorithmNames.AesCbc);
            var lastAlgorithm = SymmetricKeyAlgorithmProvider.OpenAlgorithm(SymmetricAlgorithmNames.AesCbcPkcs7);

            _blockLength = (int)lastAlgorithm.BlockLength;

            if (null != iv && iv.Length != _blockLength)
                throw new ArgumentOutOfRangeException("iv", "length must match the block length");

            _encryptedBuffer = _encrypted.AsBuffer();

            _key = algorithm.CreateSymmetricKey(keyBuffer);
            _lastKey = lastAlgorithm.CreateSymmetricKey(keyBuffer);
        }

        #region Cruft

        public override bool CanRead
        {
            get { return _parent.CanRead; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        #endregion

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        bool ReadFromDecryptedData(byte[] buffer)
        {
            if (null == _decryptedData)
                return false;

            Debug.Assert(_dataLength > 0);
            Debug.Assert(_decryptedData.Length >= _dataLength);
            Debug.Assert(_dataOffset >= 0 && _dataOffset <= _decryptedData.Length);

            var length = Math.Min(_dataLength, _count);

            Debug.Assert(length >= 0);

            if (length <= 0)
                return 0 == _count;

            Array.Copy(_decryptedData, _dataOffset, buffer, _offset, length);

            _dataLength -= length;
            _dataOffset += length;

            Debug.Assert(_dataLength >= 0);

            _count -= length;
            _offset += length;

            Debug.Assert(_count >= 0);

            if (0 == _dataLength)
                _decryptedData = null;

            _bytesRead += length;

            return 0 == _count;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset");

            if (offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException("buffer");

            if (count < 1)
                return 0;

            _count = count;
            _offset = offset;

            _bytesRead = 0;

            // First, use up what we can of any old data.
            if (ReadFromDecryptedData(buffer))
                return _bytesRead;

            for (; ; )
            {
                Debug.Assert(null == _decryptedData);

                // Now, we try to read more data from our parent.
                if (_parent.CanRead)
                {
                    if (_encryptedBuffer.Length > 0)
                    {
                        // Fill as much of the remaining buffer as we can.
                        var length = await _parent.ReadAsync(_encrypted, 0, _encrypted.Length - (int)_encryptedBuffer.Length, cancellationToken).ConfigureAwait(false);

                        _encryptedBuffer.Length += (uint)length;
                    }
                }

                var isLast = !_parent.CanRead;

                var blocks = (int)_encryptedBuffer.Length / _blockLength;

                if (isLast || blocks > 0)
                {
                    var length = isLast ? (int)_encryptedBuffer.Length : blocks * _blockLength;

                    _encryptedBuffer.Length = (uint)length;

                    var decrypted = await CryptographicEngine.DecryptAsync(isLast ? _lastKey : _key, _encryptedBuffer, _ivBuffer)
                                                             .AsTask(cancellationToken)
                                                             .ConfigureAwait(false);

                    if (!isLast)
                    {
                        Debug.Assert(decrypted.Length >= _blockLength);

                        // Preserve the new IV
                        decrypted.CopyTo((uint)(decrypted.Length - _blockLength), _ivBuffer, 0u, (uint)_blockLength);
                    }

                    _encryptedBuffer.Length -= (uint)length;

                    if (_encryptedBuffer.Length > 0)
                        Array.Copy(_encrypted, length, _encrypted, 0, (int)_encryptedBuffer.Length);

                    _decryptedData = decrypted.ToArray();

                    if (ReadFromDecryptedData(buffer))
                        return _bytesRead;
                }
            }
        }
    }
}

// -----------------------------------------------------------------------
//  <copyright file="Aes128Pkcs7ReadStream.cs" company="Henric Jungheim">
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
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Security.Cryptography.Core;
using Windows.Storage.Streams;
using SM.Media.Utility;

namespace SM.Media
{
    public class Aes128Pkcs7ReadStream : Stream
    {
        readonly int _blockLength;
        readonly byte[] _encrypted = new byte[16 * 1024];
        readonly IBuffer _encryptedBuffer;
        readonly IBuffer _ivBuffer;
        readonly CryptographicKey _key;
        readonly CryptographicKey _lastKey;
        readonly Stream _parent;
        byte[] _buffer;
        int _bytesRead;
        int _count;
        byte[] _decryptedData;
        int _decryptedLength;
        int _decryptedOffset;
        bool _eof;
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

            var algorithm = SymmetricKeyAlgorithmProvider.OpenAlgorithm(SymmetricAlgorithmNames.AesCbc);
            var lastAlgorithm = SymmetricKeyAlgorithmProvider.OpenAlgorithm(SymmetricAlgorithmNames.AesCbcPkcs7);

            _blockLength = (int)lastAlgorithm.BlockLength;

            if (null != iv && iv.Length != _blockLength)
                throw new ArgumentOutOfRangeException("iv", "length must match the block length");

            _encryptedBuffer = _encrypted.AsBuffer();
            _encryptedBuffer.Length = 0;

            var keyBuffer = key.AsBuffer();

            _key = algorithm.CreateSymmetricKey(keyBuffer);
            _lastKey = lastAlgorithm.CreateSymmetricKey(keyBuffer);

            _ivBuffer = CopyArray(iv).AsBuffer();
        }

        byte[] CopyArray(byte[] data)
        {
            var copy = new byte[data.Length];

            Array.Copy(data, copy, copy.Length);

            return copy;
        }

        void ClearBuffers()
        {
            _decryptedData = null;
            _decryptedLength = 0;
            _decryptedOffset = 0;

            _encryptedBuffer.Length = 0;
        }

        public override void Flush()
        {
            ClearBuffers();

            _parent.Flush();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            ClearBuffers();

            return _parent.FlushAsync(cancellationToken);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        bool ReadFromDecryptedData()
        {
            if (null == _decryptedData)
                return false;

            SmDebug.Assert(_decryptedLength > 0);
            SmDebug.Assert(_decryptedOffset >= 0 && _decryptedOffset + _decryptedLength <= _decryptedData.Length);

            var length = Math.Min(_decryptedLength, _count);

            SmDebug.Assert(length >= 0);

            if (length <= 0)
                return 0 == _count;

            Array.Copy(_decryptedData, _decryptedOffset, _buffer, _offset, length);

            _decryptedLength -= length;
            _decryptedOffset += length;

            SmDebug.Assert(_decryptedLength >= 0);
            SmDebug.Assert(_decryptedOffset > 0 && _decryptedOffset + _decryptedLength <= _decryptedData.Length);

            _count -= length;
            _offset += length;

            SmDebug.Assert(_count >= 0);
            SmDebug.Assert(_offset > 0 && _count + _offset <= _buffer.Length);

            if (0 == _decryptedLength)
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
            _buffer = buffer;

            _bytesRead = 0;

            // First, use up what we can of any old data.
            if (ReadFromDecryptedData())
                return _bytesRead;

            for (; ; )
            {
                SmDebug.Assert(null == _decryptedData);

                // Now, we try to read more data from our parent.
                if (_parent.CanRead)
                {
                    var remaining = _encryptedBuffer.Capacity - _encryptedBuffer.Length;

                    if (remaining > 0)
                    {
                        // Fill as much of the remaining buffer as we can.
                        var readLength = await _parent.ReadAsync(_encrypted, (int)_encryptedBuffer.Length, (int)remaining, cancellationToken).ConfigureAwait(false);

                        if (readLength < 1)
                            _eof = true;
                        else
                            _encryptedBuffer.Length += (uint)readLength;
                    }
                }

                var isLast = _eof || !_parent.CanRead;

                var blocks = (int)_encryptedBuffer.Length / _blockLength;

                var length = isLast ? (int)_encryptedBuffer.Length : blocks * _blockLength;

                if (!isLast && length == _encryptedBuffer.Length)
                {
                    // We can't be sure this isn't the last block until we read some more.
                    if (length >= _blockLength)
                        length -= _blockLength;
                    else
                        length = 0;
                }

                if (length > 0)
                {
                    var oldLength = _encryptedBuffer.Length;

                    _encryptedBuffer.Length = (uint)length;

                    var decrypted = CryptographicEngine.Decrypt(isLast ? _lastKey : _key, _encryptedBuffer, _ivBuffer);

                    if (!isLast)
                    {
                        SmDebug.Assert(_encryptedBuffer.Length >= _blockLength);

                        // Preserve the new IV
                        _encryptedBuffer.CopyTo((uint)(_encryptedBuffer.Length - _blockLength), _ivBuffer, 0u, (uint)_blockLength);
                    }

                    SmDebug.Assert(oldLength >= length);

                    _encryptedBuffer.Length = (uint)(oldLength - length);

                    if (_encryptedBuffer.Length > 0)
                        Array.Copy(_encrypted, length, _encrypted, 0, (int)_encryptedBuffer.Length);

                    _decryptedLength = (int)decrypted.Length;
                    _decryptedData = _decryptedLength > 0 ? decrypted.ToArray() : null;
                    _decryptedOffset = 0;
                }

                if (ReadFromDecryptedData() || isLast)
                    return _bytesRead;
            }
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
    }
}

// -----------------------------------------------------------------------
//  <copyright file="HttpReader.cs" company="Henric Jungheim">
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SM.Media.Web.HttpConnection
{
    public interface IHttpReader : IDisposable
    {
        bool HasData { get; }
        void Clear();
        Task<string> ReadLineAsync(CancellationToken cancellationToken);
        Task<int> ReadAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken);
    }
    
    public sealed class HttpReader : IHttpReader
    {
        #region Delegates

        public delegate Task<int> ReadAsyncDelegate(byte[] buffer, int offset, int length, CancellationToken cancellationToken);

        #endregion

        const int InitialCapacity = 16384;
        const int MaximumCapacity = 65536;
        const int ResizeRead = 1024;
        const int MinimumRead = 1024;
        const int MaximumRead = 1024;
        const int HasDataThreshold = 256;

        readonly Encoding _encoding;
        readonly ReadAsyncDelegate _readAsync;
        bool _badLine;
        int _begin;
        byte[] _buffer = new byte[InitialCapacity];
        int _end;
        bool _eof;

        public HttpReader(ReadAsyncDelegate readAsync, Encoding encoding)
        {
            if (null == readAsync)
                throw new ArgumentNullException("readAsync");

            _readAsync = readAsync;
            _encoding = encoding;
        }

        #region IHttpReader Members

        public bool HasData
        {
            get { return _end - _begin > HasDataThreshold || (_eof && _end > _begin); }
        }

        public void Dispose()
        {
            _buffer = null;
        }

        public void Clear()
        {
            _begin = 0;
            _end = 0;
        }

        public async Task<string> ReadLineAsync(CancellationToken cancellationToken)
        {
            _badLine = false;

            for (; ; )
            {
                var eolIndex = FindLine();

                if (eolIndex >= 0)
                {
                    var begin = _begin;

                    _begin = eolIndex;

                    if (_badLine)
                        _badLine = false;
                    else
                        return CreateString(begin, eolIndex);
                }

                if (_end - _begin > _buffer.Length - MinimumRead)
                {
                    if (!GrowBuffer())
                    {
                        // Throw it away.  What should we do with huge lines?   
                        Clear();

                        _badLine = true;
                    }
                }

                var length = await FillBufferAsync(cancellationToken);

                if (length < 1)
                {
                    if (_badLine)
                    {
                        Clear();

                        return null;
                    }

                    return CreateString(_begin, _end);
                }
            }
        }

        public async Task<int> ReadAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
        {
            if (length < 1)
                throw new ArgumentException("argument must be positive", "length");

            var size = _end - _begin;

            if (size < 1)
            {
                if (length >= _buffer.Length)
                    return await ReadBufferAsync(buffer, offset, length, cancellationToken).ConfigureAwait(false);

                var bytesRead = await FillBufferAsync(cancellationToken).ConfigureAwait(false);

                if (bytesRead <= 0)
                    return 0;

                size = _end - _begin;
            }

            if (length >= size)
            {
                Array.Copy(_buffer, _begin, buffer, offset, size);

                Clear();

                return size;
            }

            Array.Copy(_buffer, _begin, buffer, offset, length);

            _begin += length;

            return length;
        }

        #endregion

        async Task<int> FillBufferAsync(CancellationToken cancellationToken)
        {
            var remaining = _buffer.Length - _end;

            if (_begin > 0)
            {
                if (remaining < 128)
                {
                    var size = _end - _begin;

                    Array.Copy(_buffer, _begin, _buffer, 0, size);

                    _begin = 0;
                    _end = size;

                    remaining = _buffer.Length - _end;
                }
            }

            var readLength = remaining; //Math.Min(remaining, MaximumRead);

            var length = await ReadBufferAsync(_buffer, _end, readLength, cancellationToken).ConfigureAwait(false);

            if (length > 0)
                _end += length;

            return length;
        }

        bool GrowBuffer()
        {
            if (_buffer.Length >= MaximumCapacity)
                return false;

            var newBuffer = new byte[Math.Min(MaximumCapacity, 2 * _buffer.Length)];

            if (_end > _begin)
            {
                var size = _end - _begin;

                Array.Copy(_buffer, _begin, newBuffer, 0, size);

                _begin = 0;
                _end = size;
            }
            else
            {
                _begin = _end = 0;
            }

            _buffer = newBuffer;

            return true;
        }

        int FindLine()
        {
            for (var i = _begin; i < _end; ++i)
            {
                var ch = _buffer[i];

                if ('\n' == ch)
                    return i + 1;

                if ('\r' == ch)
                {
                    if (i + 1 < _end)
                    {
                        if ('\n' == _buffer[i + 1])
                            return i + 2;

                        return i + 1;
                    }
                }
            }

            return -1;
        }

        async Task<int> ReadBufferAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
        {
            if (_eof)
                return 0;

            var bytesRead = await _readAsync(buffer, offset, length, cancellationToken).ConfigureAwait(false);

            Debug.WriteLine("ReadBufferAsync() {0}/{1}", bytesRead, length);

            if (bytesRead <= 0)
            {
                _eof = true;
                return 0;
            }

            return bytesRead;
        }

        string CreateString(int begin, int end)
        {
            var length = end - begin;

            if (length < 1)
                return null;

            var lastCh = (char)_buffer[end - 1];

            switch (lastCh)
            {
                case '\n':
                    --end;
                    if (end > begin && '\r' == (char)_buffer[end - 1])
                        --end;
                    break;
                case '\r':
                    --end;
                    break;
                default:
                    return null;
            }

            var line = end > begin ? _encoding.GetString(_buffer, begin, end - begin) : string.Empty;

            return line;
        }
    }

    public static class HttpReaderExtensions
    {
        public static async Task<Tuple<string, string>> ReadHeaderAsync(this HttpReader httpReader, CancellationToken cancellationToken)
        {
            for (; ; )
            {
                var header = await httpReader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

                if (string.IsNullOrEmpty(header))
                    return null;

                var colon = header.IndexOf(':');

                if (colon < 1)
                {
                    Debug.WriteLine("Bad header: " + header);
                    continue;
                }

                var name = header.Substring(0, colon).Trim();
                var value = colon + 1 < header.Length ? header.Substring(colon + 1).Trim() : string.Empty;

                return Tuple.Create(name, value);
            }
        }
    }
}

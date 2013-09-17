// -----------------------------------------------------------------------
//  <copyright file="CallbackReader.cs" company="Henric Jungheim">
//  Copyright (c) 2012.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SM.Media.Segments;
using SM.Media.Utility;

namespace SM.Media
{
    public class CallbackReader : IDisposable
    {
        readonly IBlockingPool<WorkBuffer> _bufferPool;
        readonly Action<WorkBuffer> _enqueue;
        readonly object _readerLock = new object();
        readonly IAsyncEnumerable<ISegmentReader> _segmentReaders;
        bool _isClosed;
        CancellationTokenSource _readCancellationSource;
        int _readCount;
        bool _readerRunning;
        Task _readerTask;

        public CallbackReader(IAsyncEnumerable<ISegmentReader> segmentReaders, Action<WorkBuffer> enqueue, IBlockingPool<WorkBuffer> bufferPool)
        {
            if (null == segmentReaders)
                throw new ArgumentNullException("segmentReaders");

            if (null == enqueue)
                throw new ArgumentNullException("enqueue");

            if (null == bufferPool)
                throw new ArgumentNullException("bufferPool");

            _segmentReaders = segmentReaders;
            _enqueue = enqueue;
            _bufferPool = bufferPool;
        }

        #region IDisposable Members

        /// <summary>
        ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        #endregion

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            try
            {
                Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        void Close()
        {
            lock (_readerLock)
            {
                _isClosed = true;
            }

            StopAsync().Wait();
        }

        protected async Task ReadAsync(CancellationToken cancellationToken)
        {
            try
            {
                using (var segmentReaderEnumerator = _segmentReaders.GetEnumerator())
                {
                    while (await segmentReaderEnumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        var segmentReader = segmentReaderEnumerator.Current;

                        var start = DateTimeOffset.Now;

                        Debug.WriteLine("++++ Starting {0} at {1}.  Total memory: {2}", segmentReader, start, GC.GetTotalMemory(false));

                        await ReadSegment(segmentReader, cancellationToken).ConfigureAwait(false);

                        var complete = DateTimeOffset.Now;

                        Debug.WriteLine("---- Completed {0} at {1} ({2}).  Total memory: {3}", segmentReader, complete, complete - start, GC.GetTotalMemory(false));
                    }
                }

                _enqueue(null);
            }
            finally
            {
                lock (_readerLock)
                {
                    _readerRunning = false;
                }
            }
        }

        async Task ReadSegment(ISegmentReader segmentReader, CancellationToken cancellationToken)
        {
            WorkBuffer buffer = null;

            try
            {
                while (!segmentReader.IsEof)
                {
                    buffer = await _bufferPool.AllocateAsync(cancellationToken).ConfigureAwait(false);

                    Debug.Assert(null != buffer);

                    var length = await segmentReader.ReadAsync(buffer.Buffer, 0, buffer.Buffer.Length, cancellationToken).ConfigureAwait(false);

                    buffer.Length = length;

#if DEBUG
                    buffer.ReadCount = ++_readCount;
#endif

                    if (buffer.Length > 0)
                    {
                        _enqueue(buffer);

                        buffer = null;
                    }
                }
            }
            finally
            {
                if (null != buffer)
                    _bufferPool.Free(buffer);
            }
        }

        public Task StartAsync()
        {
            lock (_readerLock)
            {
                Debug.Assert(null == _readerTask || _readerTask.IsCompleted);

                if (_isClosed)
                    return TplTaskExtensions.CompletedTask;

                if (null == _readCancellationSource || _readCancellationSource.IsCancellationRequested)
                    _readCancellationSource = new CancellationTokenSource();

                var cancellationSource = _readCancellationSource;

                _readerRunning = true;
                _readerTask = TaskEx.Run(() => ReadAsync(cancellationSource.Token), cancellationSource.Token);
            }

            return _readerTask;
        }

        public async Task StopAsync()
        {
            Task oldReader;

            lock (_readerLock)
            {
                oldReader = _readerTask;

                if (null != oldReader)
                {
                    if (null != _readCancellationSource)
                        _readCancellationSource.Cancel();
                }
            }

            try
            {
                if (null != oldReader)
                    await oldReader.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // This is normal...
            }
        }
    }
}

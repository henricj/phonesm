// -----------------------------------------------------------------------
//  <copyright file="CallbackReader.cs" company="Henric Jungheim">
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
        int _isDisposed;
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
            if (0 != Interlocked.Exchange(ref _isDisposed, 1))
                return;

            Dispose(true);

            GC.SuppressFinalize(this);
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

            Task reader;
            CancellationTokenSource cancellationTokenSource;

            lock (_readerLock)
            {
                reader = _readerTask;
                _readerTask = TplTaskExtensions.CompletedTask;

                cancellationTokenSource = _readCancellationSource;
                _readCancellationSource = null;
            }

            if (null != reader)
                TaskCollector.Default.Add(reader, "CallbackReader.Close");

            if (null != cancellationTokenSource)
                cancellationTokenSource.Dispose();
        }

        void Close()
        {
            lock (_readerLock)
            {
                _isClosed = true;
            }

            StopAsync().Wait();
        }

        protected virtual async Task ReadAsync(CancellationToken cancellationToken)
        {
            try
            {
                using (var segmentReaderEnumerator = _segmentReaders.GetEnumerator())
                {
                    while (await segmentReaderEnumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        var segmentReader = segmentReaderEnumerator.Current;

                        var start = DateTimeOffset.Now;

                        Debug.WriteLine("++++ Starting {0} at {1}.  Total memory: {2:F} MiB", segmentReader, start, GC.GetTotalMemory(false).BytesToMiB());

                        await ReadSegmentAsync(segmentReader, cancellationToken).ConfigureAwait(false);

                        var complete = DateTimeOffset.Now;

                        Debug.WriteLine("---- Completed {0} at {1} ({2}).  Total memory: {3:F} MiB", segmentReader, complete, complete - start, GC.GetTotalMemory(false).BytesToMiB());
                    }
                }

                _enqueue(null);
            }
            catch (OperationCanceledException)
            {
                // Expected...
            }
            catch (Exception ex)
            {
                Debug.WriteLine("CallbackReader.ReadAsync() failed: " + ex.ExtendedMessage());
            }
            finally
            {
                lock (_readerLock)
                {
                    _readerRunning = false;
                }
            }
        }

        protected virtual async Task ReadSegmentAsync(ISegmentReader segmentReader, CancellationToken cancellationToken)
        {
            WorkBuffer buffer = null;

            try
            {
                while (!segmentReader.IsEof)
                {
                    if (null == buffer)
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

        public virtual Task StartAsync(CancellationToken cancellationToken)
        {
            CancellationTokenSource oldCancellationTokenSource = null;
            Task readerTask;

            lock (_readerLock)
            {
                Debug.Assert(null == _readerTask || _readerTask.IsCompleted);

                if (_isClosed)
                    return TplTaskExtensions.CompletedTask;

                if (null == _readCancellationSource || _readCancellationSource.IsCancellationRequested)
                {
                    oldCancellationTokenSource = _readCancellationSource;

                    // ReSharper disable once PossiblyMistakenUseOfParamsMethod
                    _readCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                }

                _readerRunning = true;

                var token = _readCancellationSource.Token;

                readerTask = _readerTask = TaskEx.Run(() => ReadAsync(token), token);
            }

            if (null != oldCancellationTokenSource)
            {
                try
                {
                    if (!oldCancellationTokenSource.IsCancellationRequested)
                        oldCancellationTokenSource.Cancel();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("CallbackReader.StartAsync() cancel failed: " + ex.Message);
                }

                oldCancellationTokenSource.Dispose();
            }

            return readerTask;
        }

        public virtual async Task StopAsync()
        {
            Task reader;
            CancellationTokenSource cancellationTokenSource;

            lock (_readerLock)
            {
                reader = _readerTask;
                cancellationTokenSource = _readCancellationSource;
            }

            if (null != cancellationTokenSource && !cancellationTokenSource.IsCancellationRequested)
                cancellationTokenSource.Cancel();

            try
            {
                if (null != reader)
                    await reader.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // This is normal...
            }
        }
    }
}

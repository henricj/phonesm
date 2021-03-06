// -----------------------------------------------------------------------
//  <copyright file="CallbackReader.cs" company="Henric Jungheim">
//  Copyright (c) 2012-2016.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012-2016 Henric Jungheim <software@henric.org>
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

namespace SM.Media.MediaManager
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
        TaskCompletionSource<long> _readResultTask = new TaskCompletionSource<long>();
        Task _readerTask;
        long _total;
#if DEBUG
        int _readCount;
#endif

        public CallbackReader(IAsyncEnumerable<ISegmentReader> segmentReaders, Action<WorkBuffer> enqueue, IBlockingPool<WorkBuffer> bufferPool)
        {
            if (null == segmentReaders)
                throw new ArgumentNullException(nameof(segmentReaders));

            if (null == enqueue)
                throw new ArgumentNullException(nameof(enqueue));

            if (null == bufferPool)
                throw new ArgumentNullException(nameof(bufferPool));

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
                cancellationTokenSource.CancelDisposeSafe();
        }

        void Close()
        {
            lock (_readerLock)
            {
                _isClosed = true;
            }

            try
            {
                StopAsync().Wait();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("CallbackReader.Close() failed: " + ex.ExtendedMessage());
            }
        }

        protected virtual async Task ReadSegmentsAsync(CancellationToken cancellationToken)
        {
            TaskCompletionSource<long> readResultTask;

            lock (_readerLock)
            {
                readResultTask = _readResultTask;
            }

            _total = 0L;

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

                readResultTask.TrySetCanceled();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("CallbackReader.ReadAsync() failed: " + ex.ExtendedMessage());

                readResultTask.TrySetException(ex);
            }
            finally
            {
                if (!readResultTask.Task.IsCompleted)
                    readResultTask.TrySetResult(_total);
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

                    var localBuffer = buffer;

                    var length = await segmentReader.ReadAsync(buffer.Buffer, 0, buffer.Buffer.Length, metadata => localBuffer.Metadata = metadata, cancellationToken).ConfigureAwait(false);

                    buffer.Length = length;

#if DEBUG
                    buffer.ReadCount = ++_readCount;
#endif

                    if (buffer.Length > 0)
                    {
                        _total += buffer.Length;

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

        public virtual Task<long> ReadAsync(CancellationToken cancellationToken)
        {
            CancellationTokenSource oldCancellationTokenSource = null;
            TaskCompletionSource<long> oldReadResultTask = null;
            TaskCompletionSource<long> readResultTask;

            lock (_readerLock)
            {
                Debug.Assert(null == _readerTask || _readerTask.IsCompleted);

                if (_isClosed)
                    return Task.FromResult(0L);

                if (null == _readCancellationSource || _readCancellationSource.IsCancellationRequested)
                {
                    oldCancellationTokenSource = _readCancellationSource;

                    // ReSharper disable once PossiblyMistakenUseOfParamsMethod
                    _readCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                }

                if (_readResultTask.Task.IsCompleted)
                {
                    oldReadResultTask = _readResultTask;
                    _readResultTask = new TaskCompletionSource<long>();
                }

                readResultTask = _readResultTask;

                var token = _readCancellationSource.Token;

                _readerTask = Task.Run(() => ReadSegmentsAsync(token), token);
            }

            if (null != oldReadResultTask)
                oldReadResultTask.TrySetCanceled();

            if (null != oldCancellationTokenSource)
                oldCancellationTokenSource.CancelDisposeSafe();

            return readResultTask.Task;
        }

        public virtual async Task StopAsync()
        {
            Task reader;
            CancellationTokenSource cancellationTokenSource;
            TaskCompletionSource<long> readResultTask;

            lock (_readerLock)
            {
                reader = _readerTask;
                readResultTask = _readResultTask;
                cancellationTokenSource = _readCancellationSource;
            }

            if (null != cancellationTokenSource && !cancellationTokenSource.IsCancellationRequested)
                cancellationTokenSource.Cancel();

            try
            {
                if (null != reader)
                    await reader.ConfigureAwait(false);

                // It should be done, but we want to propagate any exceptions.
                if (null != readResultTask)
                    await readResultTask.Task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // This is normal...
            }
        }
    }
}

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

namespace SM.Media
{
    class CallbackReader : IDisposable
    {
        const int BufferSize = 174 * 188; // Almost 32768 and saves some cycles having to rebuffer partial packets
        const int MaxBuffers = 8;
        readonly BlockingPool<WorkBuffer> _bufferPool = new BlockingPool<WorkBuffer>(MaxBuffers);
        readonly CommandWorker _commandWorker = new CommandWorker();
        readonly Action<WorkBuffer> _enqueue;
        readonly object _readerLock = new object();
        readonly ISegmentReaderManager _segmentReaderManager;
        bool _isClosed;
        CancellationTokenSource _readCancellationSource;
        int _readCount;
        bool _readerRunning;
        Task _readerTask;

        public CallbackReader(ISegmentReaderManager segmentReaderManager, Action<WorkBuffer> enqueue)
        {
            _segmentReaderManager = segmentReaderManager;
            _enqueue = enqueue;
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

        public void FreeBuffer(WorkBuffer buffer)
        {
            _bufferPool.Free(buffer);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            using (_commandWorker)
            { }

            using (_bufferPool)
            { }
        }

        protected async Task ReadAsync(TimeSpan startTime, TaskCompletionSource<TimeSpan> seekDone, CancellationToken cancellationToken)
        {
            var sw = new Stopwatch();

            try
            {
                var actualPosition = await _segmentReaderManager.Seek(startTime, cancellationToken);

                seekDone.SetResult(actualPosition);

                while (await _segmentReaderManager.MoveNextAsync())
                {
                    var segmentReader = _segmentReaderManager.Current;

                    var url = segmentReader.Url;

                    Debug.WriteLine("++++ Starting {0} at {1}.  Total memory: {2}", url, DateTimeOffset.Now, GC.GetTotalMemory(false));

                    sw.Reset();
                    sw.Start();

                    await ReadSegment(segmentReader, cancellationToken);

                    sw.Stop();

                    Debug.WriteLine("---- Completed {0} at {1} ({2} elapsed).  Total memory: {3}", url, DateTimeOffset.Now, sw.Elapsed, GC.GetTotalMemory(false));
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
                    buffer = await _bufferPool.AllocateAsync(cancellationToken);

                    var length = await segmentReader.ReadAsync(buffer.Buffer, 0, buffer.Buffer.Length, cancellationToken);

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

        #region Nested type: WorkBuffer

        public class WorkBuffer
        {
            public readonly byte[] Buffer = new byte[BufferSize];
            public int Length;

#if DEBUG
            static int _sequenceCounter;

            public readonly int Sequence = Interlocked.Increment(ref _sequenceCounter);
            public int ReadCount;
#endif
        }

        #endregion

        #region IBufferingReader members

        public void Start(Action<byte[], int> callback)
        {
            _commandWorker.SendCommand(new CommandWorker.Command(() => StartAsync(TimeSpan.Zero)));
        }

        public virtual void Seek(TimeSpan position, Action<TimeSpan> seekCompleted)
        {
            _commandWorker.SendCommand(new CommandWorker.Command(() => SeekAsync(position)
                                                                           .ContinueWith(t => seekCompleted(t.Result))));
        }

        public void Stop(Action stopCallback)
        {
            _commandWorker.SendCommand(new CommandWorker.Command(StopAsync, b => stopCallback()));
        }

        public void Close(Action closeCallback)
        {
            lock (_readerLock)
            {
                _isClosed = true;

                if (null != _readCancellationSource && !_readCancellationSource.IsCancellationRequested)
                    _readCancellationSource.Cancel();
            }

            _commandWorker.SendCommand(new CommandWorker.Command(CloseAsync, b => closeCallback()));

            //_commandWorker.CloseAsync();
        }

        public async Task<TimeSpan> StartAsync(TimeSpan startPosition)
        {
            lock (_readerLock)
            {
                Debug.Assert(null == _readerTask || _readerTask.IsCompleted);

                if (_isClosed)
                    return TimeSpan.Zero;
            }

            return await SeekAsync(startPosition);
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
                    await oldReader;
            }
            catch (OperationCanceledException)
            {
                // This is normal...
            }
        }

        async Task CloseAsync()
        {
            await StopAsync();
        }

        async Task<TimeSpan> SeekAsync(TimeSpan position)
        {
            await StopAsync();

            var seekDone = new TaskCompletionSource<TimeSpan>();

            lock (_readerLock)
            {
                if (_isClosed)
                    return TimeSpan.Zero;

                if (null == _readCancellationSource || _readCancellationSource.IsCancellationRequested)
                    _readCancellationSource = new CancellationTokenSource();

                _readerRunning = true;
                _readerTask = Task.Factory.StartNew(() => ReadAsync(position, seekDone, _readCancellationSource.Token)).Unwrap();
            }

            return await seekDone.Task;
        }

        #endregion
    }
}
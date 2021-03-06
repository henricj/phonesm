// -----------------------------------------------------------------------
//  <copyright file="SegmentReader.cs" company="Henric Jungheim">
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
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using SM.Media.Metadata;
using SM.Media.Utility;
using SM.Media.Web;

namespace SM.Media.Segments
{
    sealed class SegmentReader : ISegmentReader
    {
        readonly CancellationTokenSource _disposedCancellationTokenSource = new CancellationTokenSource();
        readonly IPlatformServices _platformServices;
        readonly IRetryManager _retryManager;
        readonly ISegment _segment;
        readonly IWebMetadataFactory _webMetadataFactory;
        readonly IWebReader _webReader;
        Uri _actualUrl;
        long? _endOffset;
        long? _expectedBytes;
        int _isDisposed;
        Stream _readStream;
        IWebStreamResponse _response;
        Stream _responseStream;
        long? _startOffset;

        public SegmentReader(ISegment segment, IWebReader webReader, IWebMetadataFactory webMetadataFactory, IRetryManager retryManager, IPlatformServices platformServices)
        {
            if (null == segment)
                throw new ArgumentNullException(nameof(segment));
            if (null == webReader)
                throw new ArgumentNullException(nameof(webReader));
            if (null == webMetadataFactory)
                throw new ArgumentNullException(nameof(webMetadataFactory));
            if (null == retryManager)
                throw new ArgumentNullException(nameof(retryManager));
            if (null == platformServices)
                throw new ArgumentNullException(nameof(platformServices));

            _segment = segment;
            _webReader = webReader;
            _webMetadataFactory = webMetadataFactory;
            _retryManager = retryManager;
            _platformServices = platformServices;

            if ((segment.Offset >= 0) && (segment.Length > 0))
            {
                _startOffset = segment.Offset;
                _endOffset = segment.Offset + segment.Length - 1;
            }
        }

        #region ISegmentReader Members

        public Uri Url => _segment.Url;

        public bool IsEof { get; private set; }

        public void Dispose()
        {
            if (0 != Interlocked.Exchange(ref _isDisposed, 1))
                return;

            try
            {
                _disposedCancellationTokenSource.Cancel();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SegmentReader.Dispose() Cancellation failed: " + ex.Message);
            }

            Close();
        }

        public async Task<int> ReadAsync(byte[] buffer, int offset, int length, Action<ISegmentMetadata> setMetadata, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            var index = 0;
            var thresholdSize = length - length / 4;
            var retryCount = 3;
            var delay = 200;

            using (var linkedCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(_disposedCancellationTokenSource.Token, cancellationToken))
            {
                do
                {
                    if (null == _readStream)
                        await OpenStream(setMetadata, cancellationToken).ConfigureAwait(false);

                    Debug.Assert(null != _readStream);

                    var retry = false;

                    try
                    {
                        var count = await _readStream.ReadAsync(buffer, offset + index, length - index, linkedCancellationToken.Token).ConfigureAwait(false);

                        if (count < 1)
                        {
                            var validLength = IsLengthValid();

                            if (!validLength)
                                throw new StatusCodeWebException(HttpStatusCode.InternalServerError, $"Read length mismatch mismatch ({_expectedBytes} expected)");

                            IsEof = true;

                            Close();

                            return index;
                        }

                        retryCount = 3;

                        if (!_startOffset.HasValue)
                            _startOffset = count;
                        else
                            _startOffset += count;

                        index += count;
                    }
                    catch (OperationCanceledException)
                    {
                        Close();

                        throw;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Read of {0} failed at {1}: {2}", _segment.Url, _startOffset, ex.Message);

                        Close();

                        if (--retryCount <= 0)
                            throw;

                        retry = true;
                    }

                    if (retry)
                    {
                        var actualDelay = (int)(delay * (0.5 + _platformServices.GetRandomNumber()));

                        delay += delay;

                        await Task.Delay(actualDelay, linkedCancellationToken.Token).ConfigureAwait(false);
                    }
                } while (index < thresholdSize);
            }

            return index;
        }

        public void Close()
        {
            var oneStream = ReferenceEquals(_readStream, _responseStream);

            try
            {
                var readStream = _readStream;

                if (null != readStream)
                {
                    _readStream = null;

                    using (readStream)
                    { }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SegmentReader.Close() readStream cleanup failed: " + ex.Message);
            }

            if (!oneStream)
                try
                {
                    var responseStream = _responseStream;

                    if (null != responseStream)
                    {
                        _responseStream = null;

                        using (responseStream)
                        { }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("SegmentReader.Close() responseStream cleanup failed: " + ex.Message);
                }

            try
            {
                var response = _response;

                if (null != response)
                {
                    _response = null;

                    using (response)
                    { }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SegmentReader.Close() response cleanup failed: " + ex.Message);
            }
        }

        #endregion

        void ThrowIfDisposed()
        {
            if (0 == _isDisposed)
                return;

            throw new ObjectDisposedException(GetType().Name);
        }

        bool IsLengthValid()
        {
            try
            {
                var actualBytesRead = _responseStream.Position;

                var badLength = _expectedBytes.HasValue && (_expectedBytes != actualBytesRead);

                return !badLength;
            }
            catch (NotSupportedException)
            {
                return true;
            }
        }

        Task OpenStream(Action<ISegmentMetadata> setMetadata, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            var webResponse = new WebResponse();

            var retry = _retryManager.CreateRetry(2, 200, RetryPolicy.IsWebExceptionRetryable);
            return retry.CallAsync(
                async () =>
                {
                    for (;;)
                    {
                        if (_startOffset.HasValue && _endOffset.HasValue)
                            _expectedBytes = _endOffset - _startOffset + 1;
                        else
                            _expectedBytes = null;

                        _response = await _webReader.GetWebStreamAsync(_actualUrl ?? _segment.Url, false, cancellationToken,
                                _segment.ParentUrl, _startOffset, _endOffset, webResponse)
                            .ConfigureAwait(false);

                        if (_response.IsSuccessStatusCode)
                        {
                            _actualUrl = _response.ActualUrl;

                            var contentLength = _response.ContentLength;

                            if (!_endOffset.HasValue)
                                _endOffset = contentLength - 1;

                            if (!_expectedBytes.HasValue)
                                _expectedBytes = contentLength;

                            _responseStream = new PositionStream(await _response.GetStreamAsync(cancellationToken).ConfigureAwait(false));

                            var filterStreamTask = _segment.CreateFilterAsync(_responseStream, cancellationToken);

                            if (null != filterStreamTask)
                                _readStream = await filterStreamTask.ConfigureAwait(false);
                            else
                                _readStream = _responseStream;

                            var segmentMetadata = _webMetadataFactory.CreateSegmentMetadata(webResponse);

                            setMetadata(segmentMetadata);

                            return;
                        }

                        // Special-case 404s.
                        var statusCode = (HttpStatusCode)_response.HttpStatusCode;
                        if ((HttpStatusCode.NotFound != statusCode) && !RetryPolicy.IsRetryable(statusCode))
                            _response.EnsureSuccessStatusCode();

                        var canRetry = await retry.CanRetryAfterDelayAsync(cancellationToken)
                            .ConfigureAwait(false);

                        if (!canRetry)
                            if ((null != _actualUrl) && (_actualUrl != _segment.Url))
                                _actualUrl = null;
                            else
                                _response.EnsureSuccessStatusCode();

                        _response.Dispose();
                        _response = null;
                    }
                }, cancellationToken);
        }

        public override string ToString()
        {
            if ((_segment.Offset > 0) || (_segment.Length > 0))
                return $"{Url} [{_segment.Offset}-{_segment.Offset + _segment.Length}]";

            return Url.ToString();
        }
    }
}

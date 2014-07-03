// -----------------------------------------------------------------------
//  <copyright file="SegmentReader.cs" company="Henric Jungheim">
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
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SM.Media.Utility;
using SM.Media.Web;

namespace SM.Media.Segments
{
    sealed class SegmentReader : ISegmentReader
    {
        readonly IPlatformServices _platformServices;
        readonly IRetryManager _retryManager;
        readonly ISegment _segment;
        readonly IWebReader _webReader;
        Uri _actualUrl;
        long? _endOffset;
        long? _expectedBytes;
        Stream _readStream;
        IWebStreamResponse _response;
        Stream _responseStream;
        long? _startOffset;

        public SegmentReader(ISegment segment, IWebReader webReader, IRetryManager retryManager, IPlatformServices platformServices)
        {
            if (null == segment)
                throw new ArgumentNullException("segment");
            if (null == webReader)
                throw new ArgumentNullException("webReader");
            if (null == retryManager)
                throw new ArgumentNullException("retryManager");
            if (null == platformServices)
                throw new ArgumentNullException("platformServices");

            _segment = segment;
            _webReader = webReader;
            _retryManager = retryManager;
            _platformServices = platformServices;

            if (segment.Offset >= 0 && segment.Length > 0)
            {
                _startOffset = segment.Offset;
                _endOffset = segment.Offset + segment.Length - 1;
            }
        }

        #region ISegmentReader Members

        public Uri Url
        {
            get { return _segment.Url; }
        }

        public bool IsEof { get; private set; }

        public void Dispose()
        {
            Close();
        }

        public async Task<int> ReadAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
        {
            var index = 0;
            var thresholdSize = length - length / 4;
            var retryCount = 3;
            var delay = 200;

            do
            {
                if (null == _readStream)
                    await OpenStream(cancellationToken).ConfigureAwait(false);

                Debug.Assert(null != _readStream);

                var retry = false;

                try
                {
                    var count = await _readStream.ReadAsync(buffer, offset + index, length - index, cancellationToken).ConfigureAwait(false);

                    if (count < 1)
                    {
                        var validLength = IsLengthValid();

                        if (!validLength)
                            throw new HttpRequestException(string.Format("Read length mismatch mismatch ({0} expected)", _expectedBytes));

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

                    await TaskEx.Delay(actualDelay, cancellationToken).ConfigureAwait(false);
                }
            } while (index < thresholdSize);

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
            {
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

        bool IsLengthValid()
        {
            try
            {
                var actualBytesRead = _responseStream.Position;

                var badLength = _expectedBytes.HasValue && _expectedBytes != actualBytesRead;

                return !badLength;
            }
            catch (NotSupportedException)
            {
                return true;
            }
        }

        Task OpenStream(CancellationToken cancellationToken)
        {
            var retry = _retryManager.CreateRetry(2, 200, RetryPolicy.IsWebExceptionRetryable);

            return retry.CallAsync(
                async () =>
                {
                    for (; ; )
                    {
                        if (_startOffset.HasValue && _endOffset.HasValue)
                            _expectedBytes = _endOffset - _startOffset + 1;
                        else
                            _expectedBytes = null;

                        _response = await _webReader.GetWebStreamAsync(_actualUrl ?? _segment.Url, false, cancellationToken,
                            _segment.ParentUrl, _startOffset, _endOffset)
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

                            var filterStreamTask = _segment.CreateFilterAsync(_responseStream);

                            if (null != filterStreamTask)
                                _readStream = await filterStreamTask.ConfigureAwait(false);
                            else
                                _readStream = _responseStream;

                            return;
                        }

                        // Special-case 404s.
                        var statusCode = (HttpStatusCode)_response.HttpStatusCode;
                        if (HttpStatusCode.NotFound != statusCode && !RetryPolicy.IsRetryable(statusCode))
                            _response.EnsureSuccessStatusCode();

                        var canRetry = await retry.CanRetryAfterDelayAsync(cancellationToken)
                                                  .ConfigureAwait(false);

                        if (!canRetry)
                        {
                            if (null != _actualUrl && _actualUrl != _segment.Url)
                                _actualUrl = null;
                            else
                                _response.EnsureSuccessStatusCode();
                        }

                        _response.Dispose();
                        _response = null;
                    }
                }, cancellationToken);
        }

        public override string ToString()
        {
            if (_segment.Offset > 0 || _segment.Length > 0)
                return string.Format("{0} [{1}-{2}]", Url, _segment.Offset, _segment.Offset + _segment.Length);

            return Url.ToString();
        }
    }
}

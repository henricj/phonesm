// -----------------------------------------------------------------------
//  <copyright file="SegmentReader.cs" company="Henric Jungheim">
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
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using SM.Media.Utility;

namespace SM.Media.Segments
{
    sealed class SegmentReader : ISegmentReader
    {
        static readonly TimeSpan DefaultNotFoundDelay = TimeSpan.FromSeconds(5);
        readonly HttpClient _httpClient;
        readonly ISegment _segment;
        readonly Func<Stream, Stream> _streamFilter;
        long _endOffset;
        HttpResponseMessage _response;
        Stream _responseStream;
        long _startOffset;

        public SegmentReader(ISegment segment, HttpClient httpClient, Func<Stream, Stream> streamFilter = null)
        {
            if (null == segment)
                throw new ArgumentNullException("segment");

            if (httpClient == null)
                throw new ArgumentNullException("httpClient");

            _streamFilter = streamFilter;

            _segment = segment;
            _httpClient = httpClient;
            _startOffset = segment.Offset;
            _endOffset = _startOffset + segment.Length - 1;
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
                if (null == _responseStream)
                    _responseStream = await OpenStream(cancellationToken).ConfigureAwait(false);

                Debug.Assert(null != _responseStream);

                var retry = false;

                try
                {
                    var count = await _responseStream.ReadAsync(buffer, offset + index, length - index, cancellationToken).ConfigureAwait(false);

                    if (count < 1)
                    {
                        IsEof = true;

                        _responseStream.Dispose();
                        _responseStream = null;

                        return index;
                    }

                    _startOffset += count;

                    index += count;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Read of {0} failed at {1}: {2}", _segment.Url, _startOffset, ex.Message);

                    if (--retryCount <= 0)
                        throw;

                    retry = true;
                }

                if (retry)
                {
                    Close();

                    var actualDelay = (int)(delay * (0.5 + GlobalPlatformServices.Default.GetRandomNumber()));

                    delay += delay;

                    await TaskEx.Delay(actualDelay, cancellationToken).ConfigureAwait(false);
                }
            } while (index < thresholdSize);

            return index;
        }

        public void Close()
        {
            using (_responseStream)
            { }
            _responseStream = null;

            using (_response)
            { }
            _response = null;
        }

        #endregion

        async Task<Stream> OpenStream(CancellationToken cancellationToken)
        {
            var notFoundRetry = 2;

            for (; ; )
            {
                try
                {
                    _response = await new Retry(3, 150, RetryPolicy.IsWebExceptionRetryable)
                        .CallAsync(() =>
                                   {
                                       var msg = new HttpRequestMessage(HttpMethod.Get, _segment.Url);

                                       if (_startOffset >= 0 && _endOffset > 0)
                                           msg.Headers.Range = new RangeHeaderValue(_startOffset, _endOffset);

                                       return _httpClient.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                                   })
                        .ConfigureAwait(false);

                    _response.EnsureSuccessStatusCode();

                    break;
                }
                catch (AggregateException aggregateException)
                {
                    aggregateException.Handle(ex =>
                                              {
                                                  var webException = ex as WebException;

                                                  if (null == webException)
                                                      return false;

                                                  var response = webException.Response as HttpWebResponse;

                                                  return null != response && response.StatusCode == HttpStatusCode.NotFound && 0 > notFoundRetry--;
                                              });
                }
                catch (WebException webException)
                {
                    var response = webException.Response as HttpWebResponse;

                    if (null == response || response.StatusCode != HttpStatusCode.NotFound || 0 > notFoundRetry--)
                        throw;
                }

                var delay = DefaultNotFoundDelay;

                var duration = _segment.Duration;

                if (duration.HasValue)
                    delay = TimeSpan.FromTicks(duration.Value.Ticks / 2);

                Debug.WriteLine("SegmentReader.OpenStream: not found delay for {0} of {1}", _segment.Url, delay);

                await TaskEx.Delay(delay, cancellationToken).ConfigureAwait(false);
            }

            if (_endOffset <= 0)
                _endOffset = _response.Content.Headers.ContentLength ?? 0;

            var stream = await _response.Content.ReadAsStreamAsync();

            if (null != _streamFilter)
                stream = _streamFilter(stream);

            return stream;
        }

        public override string ToString()
        {
            if (_segment.Offset > 0 || _segment.Length > 0)
                return string.Format("{0} [{1}-{2}]", Url, _segment.Offset, _segment.Offset + _segment.Length);

            return Url.ToString();
        }
    }
}

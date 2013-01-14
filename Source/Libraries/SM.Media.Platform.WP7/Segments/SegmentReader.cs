// -----------------------------------------------------------------------
//  <copyright file="SegmentReader.cs" company="Henric Jungheim">
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
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using SM.Media.Utility;

namespace SM.Media.Segments
{
    sealed class SegmentReader : ISegmentReader
    {
        readonly Func<Stream, Stream> _streamFilter;
        readonly Uri _url;
        readonly Func<Uri, HttpWebRequest> _webRequestFactory;
        long _endOffset;
        WebResponse _response;
        Stream _responseStream;
        long _startOffset;

        public SegmentReader(ISegment segment, Func<Uri, HttpWebRequest> webRequestFactory, Func<Stream, Stream> streamFilter = null)
        {
            if (null == segment)
                throw new ArgumentNullException("segment");

            if (null == webRequestFactory)
                throw new ArgumentNullException("webRequestFactory");

            _webRequestFactory = webRequestFactory;
            _streamFilter = streamFilter;

            _startOffset = segment.Offset;
            _endOffset = _startOffset + segment.Length - 1;
            _url = segment.Url;
        }

        #region ISegmentReader Members

        public Uri Url
        {
            get { return _url; }
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

            do
            {
                if (null == _responseStream)
                    _responseStream = await OpenStream(cancellationToken);

                Debug.Assert(null != _responseStream);

                var retryCount = 3;
                var delay = 200;
                var retry = false;

                try
                {
                    var count = await _responseStream.ReadAsync(buffer, offset + index, length - index, cancellationToken);

                    if (count < 1)
                    {
                        IsEof = true;

                        _responseStream.Close();
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
                    Debug.WriteLine("Read of {0} failed at {1}: {2}", _url, _startOffset, ex.Message);

                    if (--retryCount <= 0)
                        throw;

                    retry = true;
                }

                if (retry)
                {
                    Close();

                    var actualDelay = (int)(delay * (0.5 + GlobalPlatformServices.Default.GetRandomNumber()));

                    delay += delay;

#if WINDOWS_PHONE7
                    await TaskEx.Delay(actualDelay, cancellationToken);
#else
                    await Task.Delay(actualDelay, cancellationToken);
#endif
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

        WebRequest CreateWebRequest()
        {
            var webRequest = _webRequestFactory(_url);

            if (null != webRequest)
            {
                webRequest.AllowReadStreamBuffering = false;

                if (_startOffset >= 0 && _endOffset > 0)
                {
#if WINDOWS_PHONE
                    webRequest.Headers["Range"] = "bytes=" + _startOffset.ToString(CultureInfo.InvariantCulture) + "-"
                                                  + _endOffset.ToString(CultureInfo.InvariantCulture);
#else
                    webRequest.AddRange(_startOffset, _endOffset);
#endif
                }
            }

            return webRequest;
        }

        async Task<Stream> OpenStream(CancellationToken cancellationToken)
        {
            _response = await new Retry(3, 150, RetryPolicy.IsWebExceptionRetryable)
                                  .CallAsync(async () =>
                                                   {
                                                       var webRequest = CreateWebRequest();

                                                       return await webRequest.GetResponseAsync();
                                                   })
                                  .WithCancellation(cancellationToken);

            if (_endOffset <= 0)
                _endOffset = _response.ContentLength;

            var stream = _response.GetResponseStream();

            if (null != _streamFilter)
                stream = _streamFilter(stream);

            return stream;
        }

        public override string ToString()
        {
            if (_startOffset > 0 || _endOffset > 0)
                return string.Format("{0} [{1}-{2}]", Url, _startOffset, _endOffset);

            return Url.ToString();
        }
    }
}

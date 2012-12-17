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
        WebResponse _response;
        Stream _responseStream;
        readonly Segment _segment;

        public SegmentReader(Segment segment)
        {
            _segment = segment;
        }

        #region ISegmentReader Members

        public Uri Url
        {
            get
            {
                if (null == _segment)
                    return null;

                return _segment.Url;
            }
        }

        public bool IsEof
        {
            get
            {
                if (null == _segment)
                    return false;

                return _segment.Eof;
            }
        }

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
                        _segment.Eof = true;

                        _responseStream.Close();
                        _responseStream = null;

                        return index;
                    }

                    _segment.Offset += count;

                    index += count;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Read of {0} failed at {1}: {2}", _segment.Url, _segment.Offset, ex.Message);

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

        WebRequest CreateWebRequest(Segment segment)
        {
            var webRequest = WebRequest.Create(segment.Url);

            var httpWebRequest = webRequest as HttpWebRequest;

            if (null != httpWebRequest)
            {
                httpWebRequest.AllowReadStreamBuffering = false;

                if (segment.Offset > 0)
                    httpWebRequest.Headers["Range"] = "bytes=" + segment.Offset.ToString(CultureInfo.InvariantCulture) + "-";
            }

            return webRequest;
        }

        async Task<Stream> OpenStream(CancellationToken cancellationToken)
        {
            _response = await new Retry(3, 150, e => !(e is OperationCanceledException))
                                  .CallAsync(async () =>
                                                   {
                                                       var webRequest = CreateWebRequest(_segment);

                                                       return await Task<WebResponse>.Factory.FromAsync(webRequest.BeginGetResponse, webRequest.EndGetResponse, null);
                                                   })
                                  .WithCancellation(cancellationToken);

            return _response.GetResponseStream();
        }
    }
}
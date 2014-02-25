// -----------------------------------------------------------------------
//  <copyright file="StreamSegments.cs" company="Henric Jungheim">
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using SM.Media.M3U8;
using SM.Media.M3U8.AttributeSupport;
using SM.Media.M3U8.TagSupport;
using SM.Media.Segments;
using SM.Media.Utility;
using SM.Media.Web;

namespace SM.Media.Playlists
{
    public interface IStreamSegments
    {
        ISegment CreateStreamSegment(M3U8Parser.M3U8Uri uri);
        ICollection<ISegment> GetPlaylist();
    }

    public class StreamSegments : IStreamSegments
    {
        const string MethodAes = "AES-128";
        const string MethodNone = "NONE";
        const string MethodSampleAes = "SAMPLE-AES";
        readonly IHttpClients _httpClients;

        readonly Dictionary<Uri, Task<byte[]>> _keyCache = new Dictionary<Uri, Task<byte[]>>();
        readonly M3U8Parser _parser;
        long _byteRangeOffset;
        long? _mediaSequence;
        ISegment[] _playlist;
        int _segmentIndex;

        public StreamSegments(M3U8Parser parser, IHttpClients httpClients)
        {
            if (parser == null)
                throw new ArgumentNullException("parser");

            if (httpClients == null)
                throw new ArgumentNullException("httpClients");

            _parser = parser;
            _httpClients = httpClients;

            _mediaSequence = M3U8Tags.ExtXMediaSequence.GetValue<long>(parser.GlobalTags);
        }

        #region IStreamSegments Members

        public virtual ISegment CreateStreamSegment(M3U8Parser.M3U8Uri uri)
        {
            var url = _parser.ResolveUrl(uri.Uri);

            var segment = new SubStreamSegment(url);

            if (_mediaSequence.HasValue)
                segment.MediaSequence = _mediaSequence + _segmentIndex;

            ++_segmentIndex;

            var tags = uri.Tags;

            if (null == tags || 0 == tags.Length)
                return segment;

            var info = M3U8Tags.ExtXInf.Find(tags);

            if (null != info)
                segment.Duration = TimeSpan.FromSeconds((double)info.Duration);

            var byteRange = M3U8Tags.ExtXByteRange.Find(tags);
            if (null != byteRange)
                HandleByteRange(segment, byteRange);

            var extKeys = M3U8Tags.ExtXKey.FindAll(tags);

            if (null != extKeys)
                HandleKey(segment, extKeys);

            return segment;
        }

        public ICollection<ISegment> GetPlaylist()
        {
            if (null == _playlist)
            {
                _playlist = _parser.Playlist.Select(CreateStreamSegment)
                                   .ToArray();
            }

            return _playlist;
        }

        #endregion

        void HandleByteRange(SubStreamSegment segment, ByterangeTagInstance byteRange)
        {
            if (byteRange.Offset.HasValue)
            {
                segment.Offset = byteRange.Offset.Value;
                _byteRangeOffset = byteRange.Offset.Value;
            }
            else
                segment.Offset = _byteRangeOffset;

            segment.Length = byteRange.Length;
            _byteRangeOffset += byteRange.Length;
        }

        void HandleKey(SubStreamSegment segment, IEnumerable<ExtKeyTagInstance> extKeys)
        {
            var keys = extKeys.ToArray();

            if (keys.Length < 1)
                return;

            string keyUri = null;
            byte[] iv = null;

            foreach (var key in keys)
            {
                var method = key.AttributeObject(ExtKeySupport.AttrMethod);

                if (String.Equals(MethodNone, method, StringComparison.OrdinalIgnoreCase))
                {
                    keyUri = null;
                    continue;
                }

                if (!String.Equals(MethodAes, method, StringComparison.OrdinalIgnoreCase))
                {
                    if (String.Equals(MethodSampleAes, method, StringComparison.OrdinalIgnoreCase))
                        throw new NotImplementedException("Method SAMPLE-AES decryption is not implemented");

                    throw new NotSupportedException("Unknown decryption method type: " + method);
                }

                var newUri = key.AttributeObject(ExtKeySupport.AttrUri);
                if (null != newUri)
                    keyUri = newUri;

                var newIv = key.AttributeObject(ExtKeySupport.AttrIv);
                if (null != newIv)
                    iv = newIv;
            }

            if (null == keyUri)
                return;

            if (null == iv)
            {
                iv = new byte[16];

                var ms = segment.MediaSequence ?? (_segmentIndex - 1);

                iv[15] = (byte)(ms & 0xff);
                iv[14] = (byte)((ms >> 8) & 0xff);
                iv[13] = (byte)((ms >> 16) & 0xff);
                iv[12] = (byte)((ms >> 24) & 0xff);
            }

            var filter = segment.AsyncStreamFilter;

            var uri = _parser.ResolveUrl(keyUri);

            HttpClient binaryClient = null;

            Task<byte[]> keyTask;

            if (!_keyCache.TryGetValue(uri, out keyTask))
            {
                binaryClient = _httpClients.CreateBinaryClient(_parser.BaseUrl);

                keyTask = binaryClient.GetByteArrayAsync(uri);

                _keyCache[uri] = keyTask;
            }

            segment.AsyncStreamFilter =
                async stream =>
                {
                    using (binaryClient)
                    {
                        if (null != filter)
                            stream = await filter(stream).ConfigureAwait(false);

                        var key = await keyTask.ConfigureAwait(false);

                        Debug.WriteLine("Segment AES-128: key {0} iv {1}", BitConverter.ToString(key), BitConverter.ToString(iv));

                        return GlobalPlatformServices.Default.Aes128DecryptionFilter(stream, key, iv);
                    }
                };
        }
    }
}

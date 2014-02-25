// -----------------------------------------------------------------------
//  <copyright file="M3U8Tags.cs" company="Henric Jungheim">
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

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SM.Media.M3U8.AttributeSupport;
using SM.Media.M3U8.TagSupport;

namespace SM.Media.M3U8
{
    public class M3U8Tags
    {
        #region Tags

        public static readonly M3U8ExtInfTag ExtXInf = new M3U8ExtInfTag("#EXTINF", M3U8TagScope.Segment);
        public static readonly M3U8ByterangeTag ExtXByteRange = new M3U8ByterangeTag("#EXT-X-BYTERANGE", M3U8TagScope.Segment);
        public static readonly M3U8ValueTag ExtXTargetDuration = new M3U8ValueTag("#EXT-X-TARGETDURATION", M3U8TagScope.Global, ValueTagInstance.CreateLong);
        public static readonly M3U8ValueTag ExtXMediaSequence = new M3U8ValueTag("#EXT-X-MEDIA-SEQUENCE", M3U8TagScope.Global, ValueTagInstance.CreateLong);
        public static readonly M3U8ExtKeyTag ExtXKey = new M3U8ExtKeyTag("#EXT-X-KEY", M3U8TagScope.Shared);
        public static readonly M3U8Tag ExtXProgramDateTime = new M3U8DateTimeTag("#EXT-X-PROGRAM-DATE-TIME", M3U8TagScope.Segment);
        public static readonly M3U8Tag ExtXAllowCache = new M3U8Tag("#EXT-X-ALLOW-CACHE", M3U8TagScope.Global, M3U8AttributeSupport.CreateInstance);
        public static readonly M3U8ValueTag ExtXPlaylistType = new M3U8ValueTag("#EXT-X-PLAYLIST-TYPE", M3U8TagScope.Global, (tag, value) => ValueTagInstance.Create(tag, value, v => v));
        public static readonly M3U8Tag ExtXEndList = new M3U8Tag("#EXT-X-ENDLIST", M3U8TagScope.Global, M3U8AttributeSupport.CreateInstance);
        public static readonly M3U8Tag ExtXMedia = new M3U8AttributeTag("#EXT-X-MEDIA", M3U8TagScope.Global, ExtMediaSupport.Attributes, (tag, value) => AttributesTagInstance.Create(tag, value, ExtMediaSupport.Attributes));
        public static readonly M3U8ExtStreamInfTag ExtXStreamInf = new M3U8ExtStreamInfTag("#EXT-X-STREAM-INF", M3U8TagScope.Segment);
        public static readonly M3U8Tag ExtXDiscontinuity = new M3U8Tag("#EXT-X-DISCONTINUITY", M3U8TagScope.Segment, M3U8AttributeSupport.CreateInstance);
        public static readonly M3U8Tag ExtXIFramesOnly = new M3U8Tag("#EXT-X-I-FRAMES-ONLY", M3U8TagScope.Global, M3U8AttributeSupport.CreateInstance);
        public static readonly M3U8Tag ExtXMap = new M3U8Tag("#EXT-X-MAP", M3U8TagScope.Shared, MapTagInstance.Create);
        public static readonly M3U8Tag ExtXIFrameStreamInf = new M3U8AttributeTag("#EXT-X-I-FRAME-STREAM-INF", M3U8TagScope.Global, ExtIFrameStreamInfSupport.Attributes, (tag, value) => AttributesTagInstance.Create(tag, value, ExtIFrameStreamInfSupport.Attributes));
        public static readonly M3U8ValueTag ExtXVersion = new M3U8ValueTag("#EXT-X-VERSION", M3U8TagScope.Global, ValueTagInstance.CreateLong);

        #endregion

        public static readonly M3U8Tags Default = new M3U8Tags();

        volatile Dictionary<string, M3U8Tag> _tags
            = (new[]
               {
                   ExtXInf,
                   ExtXByteRange,
                   ExtXTargetDuration,
                   ExtXMediaSequence,
                   ExtXKey,
                   ExtXProgramDateTime,
                   ExtXAllowCache,
                   ExtXPlaylistType,
                   ExtXEndList,
                   ExtXMedia,
                   ExtXStreamInf,
                   ExtXDiscontinuity,
                   ExtXIFramesOnly,
                   ExtXMap,
                   ExtXIFrameStreamInf,
                   ExtXVersion
               }).ToDictionary(t => t.Name);

        public void RegisterTag(IEnumerable<M3U8Tag> tags)
        {
            // "Register" calls are rare and will likely only happen during startup.
            // If we never modify an externally visible dictionary, then we do
            // not need to worry about locking during "Create()".
            for (; ; )
            {
                var currentTags = _tags;

                var combinedTags = new Dictionary<string, M3U8Tag>(currentTags);

                foreach (var tag in tags)
                    combinedTags[tag.Name] = tag;

#pragma warning disable 0420
                var previousTags = Interlocked.CompareExchange(ref _tags, combinedTags, currentTags);
#pragma warning restore 0420

                if (ReferenceEquals(previousTags, currentTags))
                    return;
            }
        }

        public M3U8TagInstance Create(string tagName, string value)
        {
            M3U8Tag tag;
            if (!_tags.TryGetValue(tagName, out tag))
                return null;

            return tag.CreateInstance(tag, value);
        }
    }
}

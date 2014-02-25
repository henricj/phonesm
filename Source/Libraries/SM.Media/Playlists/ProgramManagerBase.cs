// -----------------------------------------------------------------------
//  <copyright file="ProgramManagerBase.cs" company="Henric Jungheim">
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
using SM.Media.M3U8;
using SM.Media.M3U8.AttributeSupport;
using SM.Media.Web;

namespace SM.Media.Playlists
{
    public class ProgramManagerBase
    {
        readonly Func<M3U8Parser, IStreamSegments> _segmentsFactory;
        readonly IWebCacheFactory _webCacheFactory;
        readonly IWebContentTypeDetector _webContentTypeDetector;

        protected ProgramManagerBase(Func<M3U8Parser, IStreamSegments> segmentsFactory, IWebCacheFactory webCacheFactory, IWebContentTypeDetector webContentTypeDetector)
        {
            if (null == segmentsFactory)
                throw new ArgumentNullException("segmentsFactory");
            if (null == webCacheFactory)
                throw new ArgumentNullException("webCacheFactory");
            if (null == webContentTypeDetector)
                throw new ArgumentNullException("webContentTypeDetector");

            _segmentsFactory = segmentsFactory;
            _webCacheFactory = webCacheFactory;
            _webContentTypeDetector = webContentTypeDetector;
        }

        protected IDictionary<long, Program> Load(Uri playlist, M3U8Parser parser)
        {
            var audioStreams = new Dictionary<string, MediaGroup>();

            foreach (var gt in parser.GlobalTags)
            {
                if (M3U8Tags.ExtXMedia == gt.Tag)
                {
                    try
                    {
                        var audioAttribute = gt.Attribute(ExtMediaSupport.AttrType, "AUDIO");

                        if (null != audioAttribute)
                            AddMedia(playlist, gt, audioStreams);
                    }
                    catch (NullReferenceException)
                    {
                        // DynamicObject isn't welcome on the phone or this would be a binding exception.
                    }
                }
            }

            var programs = new Dictionary<long, Program>();
            var hasSegments = false;

            foreach (var p in parser.Playlist)
            {
                if (null == p.Tags || p.Tags.Length < 1)
                    continue;

                var streamInf = M3U8Tags.ExtXStreamInf.Find(p.Tags);

                var programId = long.MinValue;
                MediaGroup audioGroup = null;

                if (null != streamInf)
                {
                    var programIdAttribute = streamInf.Attribute(ExtStreamInfSupport.AttrProgramId);

                    if (null != programIdAttribute)
                        programId = programIdAttribute.Value;

                    var audioAttribute = streamInf.AttributeObject(ExtStreamInfSupport.AttrAudio);

                    if (null != audioAttribute)
                        audioStreams.TryGetValue(audioAttribute, out audioGroup);

                    var playlistUrl = parser.ResolveUrl(p.Uri);

                    var bandwidth = streamInf.Attribute(ExtStreamInfSupport.AttrBandwidth);

                    var programUrl = parser.BaseUrl;

                    var program = GetProgram(programs, programId, programUrl);

                    var subProgram = new PlaylistSubProgram(program, new ProgramStream(_segmentsFactory, _webCacheFactory, _webContentTypeDetector)
                                                                     {
                                                                         Urls = new[] { playlistUrl }
                                                                     })
                                     {
                                         Bandwidth = null == bandwidth ? 0 : bandwidth.Value,
                                         Playlist = playlistUrl,
                                         AudioGroup = audioGroup
                                     };

                    program.SubPrograms.Add(subProgram);
                }
                else
                    hasSegments = true;
            }

            if (hasSegments)
            {
                var program = GetProgram(programs, long.MinValue, parser.BaseUrl);

                var subProgram = new PlaylistSubProgram(program, new ProgramStream(_segmentsFactory, _webCacheFactory, _webContentTypeDetector, parser)
                                                                 {
                                                                     Urls = new[] { playlist }
                                                                 });

                program.SubPrograms.Add(subProgram);
            }

            return programs;
        }

        static Program GetProgram(IDictionary<long, Program> programs, long programId, Uri programUrl)
        {
            Program program;

            if (!programs.TryGetValue(programId, out program))
            {
                program = new Program
                          {
                              PlaylistUrl = programUrl,
                              ProgramId = programId
                          };

                programs[programId] = program;
            }

            return program;
        }

        public void Dispose()
        {
            Dispose(true);

            GC.KeepAlive(this);
        }

        protected virtual void Dispose(bool disposing)
        { }

        static void AddMedia(Uri playlist, M3U8TagInstance gt, Dictionary<string, MediaGroup> audioStreams)
        {
            var groupId = gt.Attribute(ExtMediaSupport.AttrGroupId).Value;

            var urlAttribute = gt.AttributeObject(ExtMediaSupport.AttrUri);

            Uri playlistUrl = null;

            if (null != urlAttribute)
                playlistUrl = new Uri(playlist, new Uri(urlAttribute, UriKind.RelativeOrAbsolute));

            var language = gt.AttributeObject(ExtMediaSupport.AttrLanguage);

            var audioStream = new PlaylistSubStream
                              {
                                  Type = gt.AttributeObject(ExtMediaSupport.AttrType),
                                  Name = groupId,
                                  Playlist = playlistUrl,
                                  IsAutoselect = IsYesNo(gt, ExtMediaSupport.AttrAutoselect),
                                  Language = null == language ? null : language.Trim().ToLower()
                              };

            MediaGroup mediaGroup;
            if (!audioStreams.TryGetValue(groupId, out mediaGroup))
            {
                mediaGroup = new MediaGroup
                             {
                                 Default = audioStream
                             };

                audioStreams[groupId] = mediaGroup;
            }

            var isDefault = IsYesNo(gt, ExtMediaSupport.AttrDefault);

            if (isDefault)
                mediaGroup.Default = audioStream;

            var name = gt.Attribute(ExtMediaSupport.AttrName).Value;

            mediaGroup.Streams[name] = audioStream;
        }

        static bool IsYesNo(M3U8TagInstance tag, M3U8ValueAttribute<string> attribute, bool defaultValue = false)
        {
            var attr = tag.Attribute(attribute);

            if (null == attr || string.IsNullOrWhiteSpace(attr.Value))
                return defaultValue;

            return 0 == string.CompareOrdinal("YES", attr.Value.ToUpperInvariant());
        }

        #region Nested type: MediaGroup

        public class MediaGroup
        {
            public readonly IDictionary<string, SubStream> Streams = new Dictionary<string, SubStream>();
            public SubStream Default { get; set; }
        }

        #endregion
    }
}

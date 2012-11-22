//-----------------------------------------------------------------------
// <copyright file="ProgramManagerBase.cs" company="Henric Jungheim">
// Copyright (c) 2012.
// <author>Henric Jungheim</author>
// </copyright>
//-----------------------------------------------------------------------
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
using System.Collections.Generic;
using System.Linq;
using SM.Media.M3U8;
using SM.Media.M3U8.M38UAttributes;
using SM.Media.M3U8.M38UTags;

namespace SM.Media.Playlists
{
    public class ProgramManagerBase : IDisposable
    {
        public IEnumerable<Program> Programs
        {
            get { return null; }
        }

        #region IDisposable Members

        /// <summary>
        ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        { }

        #endregion

        public IDictionary<long, Program> Load(Uri playlist, M3U8Parser parser)
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
                        {
                            var group = gt.Attribute(ExtMediaSupport.AttrGroupId).Value;

                            var urlAttribute = gt.AttributeObject(ExtMediaSupport.AttrUri);

                            Uri playlistUrl = null;

                            if (null != urlAttribute)
                                playlistUrl = new Uri(playlist, new Uri(urlAttribute, UriKind.RelativeOrAbsolute));

                            var audioStream = new PlaylistSubStream
                                              {
                                                  Name = @group,
                                                  Playlist = playlistUrl
                                              };

                            MediaGroup mediaGroup;
                            if (!audioStreams.TryGetValue(@group, out mediaGroup))
                            {
                                mediaGroup = new MediaGroup { Default = audioStream };

                                audioStreams[@group] = mediaGroup;
                            }

                            var isDefault = 0 == string.CompareOrdinal("YES", gt.Attribute(ExtMediaSupport.AttrDefault).Value);

                            if (isDefault)
                                mediaGroup.Default = audioStream;

                            var name = gt.Attribute(ExtMediaSupport.AttrName).Value;

                            mediaGroup.Streams[name] = audioStream;
                        }
                    }
                    catch (NullReferenceException)
                    {
                        // DynamicObject isn't welcome on the phone or this would be a binding exception.
                    }
                }
            }

            var programs = new Dictionary<long, Program>();
            SimpleSubProgram simpleSubProgram = null;

            foreach (var p in parser.Playlist)
            {
                var streamInf = p.Tags.FirstOrDefault(t => M3U8Tags.ExtXStreamInf == t.Tag);

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

                    var subProgram = new PlaylistSubProgramBase
                                     {
                                         Bandwidth = streamInf.Attribute(ExtStreamInfSupport.AttrBandwidth).Value,
                                         Playlist = new Uri(playlist, new Uri(p.Uri, UriKind.RelativeOrAbsolute)),
                                         Audio = audioGroup
                                     };

                    Program program;

                    if (!programs.TryGetValue(programId, out program))
                    {
                        program = new Program { ProgramId = programId };

                        programs[programId] = program;
                    }

                    program.SubPrograms.Add(subProgram);
                }
                else
                {
                    var extInf = (ExtinfTagInstance)p.Tags.FirstOrDefault(t => M3U8Tags.ExtXInf == t.Tag);

                    if (null != extInf)
                    {
                        if (null == simpleSubProgram)
                        {
                            simpleSubProgram = new SimpleSubProgram();

                            var program = new Program { ProgramId = long.MinValue };

                            program.SubPrograms.Add(simpleSubProgram);

                            programs[program.ProgramId] = program;
                        }

                        simpleSubProgram.Segments.Add(new SubStreamSegment(new Uri(playlist, new Uri(p.Uri, UriKind.RelativeOrAbsolute)))
                                                      {
                                                          Duration = TimeSpan.FromSeconds((double)extInf.Duration)
                                                      });
                    }
                }
            }

            return programs;
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

// -----------------------------------------------------------------------
//  <copyright file="MediaParserMediaStreamExtensions.cs" company="Henric Jungheim">
//  Copyright (c) 2012-2015.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012-2015 Henric Jungheim <software@henric.org>
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
using SM.Media.Configuration;

namespace SM.Media.MediaParser
{
    public static class MediaParserMediaStreamExtensions
    {
        public static IMediaConfiguration CreateMediaConfiguration(this IEnumerable<IMediaParserMediaStream> mediaParserMediaStreams, TimeSpan? duration)
        {
            var configuration = new MediaConfiguration
            {
                Duration = duration
            };

            List<IMediaParserMediaStream> alternateStreams = null;

            foreach (var mediaStream in mediaParserMediaStreams)
            {
                var configurationSource = mediaStream.ConfigurationSource;

                var video = configurationSource as IVideoConfigurationSource;

                if (null != video && null == configuration.Video)
                {
                    configuration.Video = mediaStream;

                    continue;
                }

                var audio = configurationSource as IAudioConfigurationSource;

                if (null != audio && null == configuration.Audio)
                {
                    configuration.Audio = mediaStream;

                    continue;
                }

                if (null == alternateStreams)
                    alternateStreams = new List<IMediaParserMediaStream>();

                alternateStreams.Add(mediaStream);
            }

            configuration.AlternateStreams = alternateStreams;

            return configuration;
        }
    }
}

// -----------------------------------------------------------------------
//  <copyright file="TrackManager.cs" company="Henric Jungheim">
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

namespace HlsView
{
    class MediaTrack
    {
        public Uri Url { get; set; }
        public string Title { get; set; }
        public bool UseNativePlayer { get; set; }
    }

    static class TrackManager
    {
        static readonly MediaTrack[] Sources =
        {
            new MediaTrack
            {
                Title = "NASA TV",
                Url = new Uri("http://www.nasa.gov/multimedia/nasatv/NTV-Public-IPS.m3u8")
            },
            new MediaTrack
            {
                Title = "NPR",
                Url = new Uri("http://www.npr.org/streams/mp3/nprlive24.pls")
            },
            new MediaTrack
            {
                Title = "Bjarne Stroustrup - The Essence of C++",
                Url = new Uri("http://media.ch9.ms/ch9/ca9a/66ac2da7-efca-4e13-a494-62843281ca9a/GN13BjarneStroustrup.mp3"),
                UseNativePlayer = true
            },
            new MediaTrack
            {
                Title = "Apple",
                Url = new Uri("http://devimages.apple.com/iphone/samples/bipbop/bipbopall.m3u8")
            },
            null,
            new MediaTrack
            {
                Title = "Apple 16x9",
                Url = new Uri("https://devimages.apple.com.edgekey.net/streaming/examples/bipbop_16x9/bipbop_16x9_variant.m3u8")
            }
        };

        public static IList<MediaTrack> Tracks
        {
            get { return Sources; }
        }
    }
}

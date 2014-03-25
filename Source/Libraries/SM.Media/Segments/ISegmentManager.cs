// -----------------------------------------------------------------------
//  <copyright file="ISegmentManager.cs" company="Henric Jungheim">
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
using System.Threading.Tasks;
using SM.Media.Content;
using SM.Media.Utility;
using SM.Media.Web;

namespace SM.Media.Segments
{
    public interface ISegmentManager : IStopClose
    {
        IWebReader WebReader { get; }
        TimeSpan StartPosition { get; }
        TimeSpan? Duration { get; }

        ContentType ContentType { get; }
        IAsyncEnumerable<ISegment> Playlist { get; }

        Task<TimeSpan> SeekAsync(TimeSpan timestamp);
        Task StartAsync();
    }

    public static class SegmentManagerAsyncExtensions
    {
        public static Task<TimeSpan> Start(this ISegmentManager segmentManager)
        {
            return segmentManager.SeekAsync(TimeSpan.Zero);
        }
    }
}

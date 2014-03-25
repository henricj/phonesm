// -----------------------------------------------------------------------
//  <copyright file="ISegment.cs" company="Henric Jungheim">
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
using System.IO;
using System.Threading.Tasks;

namespace SM.Media.Segments
{
    public interface ISegment
    {
        Uri Url { get; }

        Uri ParentUrl { get; }

        long Offset { get; }

        long Length { get; }

        TimeSpan? Duration { get; }

        long? MediaSequence { get; }

        /// <summary>
        ///     Create a new stream to modify the data returned from the web server (e.g.,
        ///     stripping headers from .mp3 or decrypting #EXT-X-KEY AES-128 segments).
        /// </summary>
        /// <param name="stream"></param>
        /// <returns>null if no filter is required</returns>
        Task<Stream> CreateFilterAsync(Stream stream);
    }
}

// -----------------------------------------------------------------------
//  <copyright file="MediaStreamFacadeParameters.cs" company="Henric Jungheim">
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

namespace SM.Media
{
    public class MediaStreamFacadeParameters
    {
        public static TimeSpan DefaultStartTimeout = TimeSpan.FromSeconds(10);

        public MediaStreamFacadeParameters()
        {
            CreateTimeout = DefaultStartTimeout;
        }

        public Func<IMediaStreamFacadeBase> Factory { get; set; }

        /// <summary>
        ///     Use the socket-based <see cref="SM.Media.Web.HttpConnection.HttpConnection" /> instead of the
        ///     platform's default HTTP client (usually HttpClient).
        /// </summary>
        public bool UseHttpConnection { get; set; }

        public bool UseSingleStreamMediaManager { get; set; }

        /// <summary>
        ///     Cancel playback if it takes longer than this to create the media stream source.
        /// </summary>
        public TimeSpan CreateTimeout { get; set; }
    }
}

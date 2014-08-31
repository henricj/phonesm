// -----------------------------------------------------------------------
//  <copyright file="UriExtensions.cs" company="Henric Jungheim">
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

namespace SM.Media.Web
{
    public static class UriExtensions
    {
        static readonly char[] Slashes = { '/', '\\' };

        /// <summary>
        ///     Check if the URL contains a file extension that matches the argument.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="extension"></param>
        /// <returns></returns>
        public static bool HasExtension(this Uri url, string extension)
        {
            if (!url.IsAbsoluteUri)
                return false;

            return url.LocalPath.EndsWith(extension, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        ///     Get the file extension, including the period, of the URL's local path.
        /// </summary>
        /// <param name="url"></param>
        /// <returns>Extension or null</returns>
        public static string GetExtension(this Uri url)
        {
            if (!url.IsAbsoluteUri)
                return null;

            var path = url.LocalPath;

            var lastPeriod = path.LastIndexOf('.');

            if (lastPeriod <= 0 || lastPeriod + 1 == path.Length)
                return null;

            var lastSlash = path.LastIndexOfAny(Slashes);

            if (lastSlash >= lastPeriod)
                return null;

            return path.Substring(lastPeriod);
        }

        /// <summary>
        ///     Get the file extension, including the period, of the URL's local path.
        /// </summary>
        /// <param name="path"></param>
        /// <returns>Extension or null</returns>
        public static string GetExtension(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

#if SM_MEDIA_LEGACY
            var lastPeriod = path.LastIndexOf('.');

            if (lastPeriod <= 0 || lastPeriod + 1 == path.Length)
                return null;

            var lastSlash = path.LastIndexOfAny(Slashes);

            if (lastSlash >= lastPeriod)
                return null;

            return path.Substring(lastPeriod);
#else
            return Path.GetExtension(path);
#endif
        }
    }
}

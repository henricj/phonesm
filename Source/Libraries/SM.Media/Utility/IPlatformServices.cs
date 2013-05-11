// -----------------------------------------------------------------------
//  <copyright file="IPlatformServices.cs" company="Henric Jungheim">
//  Copyright (c) 2012.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
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
using System.Threading;

namespace SM.Media.Utility
{
    public interface IPlatformServices
    {
        // Only thin wrappers around platform services belong here.  Any application/library
        // services belong elsewhere (in some DI/IoC).

        /// <summary>
        ///     Returns a random number between 0.0 and 1.0.  It does not suffer from Random's slowly
        ///     changing default seed.
        /// </summary>
        /// <returns></returns>
        double GetRandomNumber();

        /// <summary>
        /// Create and start a thread.
        /// </summary>
        /// <remarks>Use the TPL when possible.</remarks>
        /// <param name="name"></param>
        /// <param name="run"></param>
        /// <returns></returns>
        Thread RunThread(string name, Action run);

        /// <summary>
        /// Join a thread.
        /// </summary>
        /// <remarks>Use the TPL when possible.</remarks>
        /// <param name="thread"></param>
        /// <returns></returns>
        void JoinThread(Thread thread);
    }
}

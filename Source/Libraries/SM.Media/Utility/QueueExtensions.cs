// -----------------------------------------------------------------------
//  <copyright file="QueueExtensions.cs" company="Henric Jungheim">
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
using System.Diagnostics;

namespace SM.Media.Utility
{
    public static class QueueExtensions
    {
        /// <summary>
        ///     Remove an item from a queue.  This is expensive, since it copies the queue, clears it, then re-enqueue
        ///     everything but the requested item.  Think about finding a more suitable data structure if this needs
        ///     to happen often or if the queue is large.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="queue"></param>
        /// <param name="item"></param>
        /// <returns></returns>
        public static bool Remove<T>(this Queue<T> queue, T item)
            where T : class
        {
            if (!queue.Contains(item))
                return false;

            var items = queue.ToArray();

            queue.Clear();

            var foundItem = false;

            foreach (var x in items)
            {
                if (ReferenceEquals(x, item))
                {
                    if (foundItem)
                        Debug.WriteLine("RemoveQueue.Remove() multiple matches");

                    foundItem = true;

                    continue;
                }

                queue.Enqueue(x);
            }

            return foundItem;
        }
    }
}

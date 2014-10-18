// -----------------------------------------------------------------------
//  <copyright file="EnumerableExtensions.cs" company="Henric Jungheim">
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
using System.Linq;

namespace SM.Media.Utility
{
    public static class EnumerableExtensions
    {
        /// <summary>
        ///     Return the first item if there is exactly one item in the enumerable.  Unlike .SingleOrDefault(), exceptions will
        ///     not be thrown if there is more than one item or if the enumerable itself is null.  Exceptions may still result
        ///     from enumerating the enumerable.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="items"></param>
        /// <returns></returns>
        public static T SingleOrDefaultSafe<T>(this IEnumerable<T> items)
        {
            if (null == items)
                return default(T);

            var list = items as IList<T>;

            if (null != list)
                return 1 == list.Count ? list[0] : default(T);

            using (var itemEnum = items.GetEnumerator())
            {
                if (!itemEnum.MoveNext())
                    return default(T);

                var item = itemEnum.Current;

                return itemEnum.MoveNext() ? default(T) : item;
            }
        }

        /// <summary>
        /// Check if two sequences are the same.  Unlike SequenceEqual(), two nulls
        /// are considered equivalent.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool SequencesAreEquivalent<T>(this IEnumerable<T> a, IEnumerable<T> b)
        {
            if (ReferenceEquals(a, b))
                return true;

            if (null == a || null == b)
                return false;

            return a.SequenceEqual(b);
        }
    }
}

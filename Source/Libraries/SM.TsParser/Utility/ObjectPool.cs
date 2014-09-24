// -----------------------------------------------------------------------
//  <copyright file="ObjectPool.cs" company="Henric Jungheim">
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
using System.Diagnostics;

namespace SM.TsParser.Utility
{
    sealed class ObjectPool<T> : IDisposable
        where T : new()
    {
        readonly Stack<T> _pool = new Stack<T>();
#if OBJECT_POOL_STATISTICS
        int _allocations;
        int _deallocations;
        int _objectsCreated;
#if DEBUG
        readonly List<T> _allocationTracker = new List<T>(); 
#endif
#endif

        public T Allocate()
        {
            lock (_pool)
            {
#if OBJECT_POOL_STATISTICS
                ++_allocations;
#endif

                if (_pool.Count > 0)
                {
                    var poolObject = _pool.Pop();

                    return poolObject;
                }

#if OBJECT_POOL_STATISTICS
                ++_objectsCreated;
#endif
            }

            var t = new T();

#if OBJECT_POOL_STATISTICS && DEBUG
            lock (_pool)
            {
                _allocationTracker.Add(t);
            }
#endif

            return t;
        }

        public void Free(T poolObject)
        {
            lock (_pool)
            {
                _pool.Push(poolObject);

#if OBJECT_POOL_STATISTICS
                ++_deallocations;
                Debug.Assert(_deallocations <= _allocations,
                    string.Format("ObjectPool.Free() more deallocations than allocations ({0} > {1})", _deallocations, _allocations));
#endif
            }
        }

        public void Dispose()
        {
            Clear();
        }

        public void Clear()
        {
            lock (_pool)
            {
#if OBJECT_POOL_STATISTICS
                Debug.Assert(_allocations == _deallocations && _pool.Count == _objectsCreated,
                    string.Format("ObjectPool.Clear(): allocations {0} == deallocations {1} && _pool.Count {2} == _objectsCreated {3}",
                        _allocations, _deallocations, _pool.Count, _objectsCreated));
#endif

                _pool.Clear();

#if OBJECT_POOL_STATISTICS
                _allocations = 0;
                _deallocations = 0;
                _objectsCreated = 0;
#endif
            }
        }
    }
}

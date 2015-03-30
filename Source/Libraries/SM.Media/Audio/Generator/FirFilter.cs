// -----------------------------------------------------------------------
//  <copyright file="FirFilter.cs" company="Henric Jungheim">
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

namespace SM.Media.Audio.Generator
{
    public class FirFilter
    {
        readonly float[] _filterCoefficients;
        readonly float[] _history;
        int _historyIndex;

        public FirFilter(float[] filterCoefficients)
        {
            if (null == filterCoefficients)
                throw new ArgumentNullException("filterCoefficients");

            _filterCoefficients = filterCoefficients;
            _history = new float[_filterCoefficients.Length];
        }

        public float Filter(float x)
        {
            if (--_historyIndex < 0)
                _historyIndex = _history.Length - 1;

            _history[_historyIndex] = x;

            var sum = 0f;

            var historyIndex = _historyIndex;

            foreach (var fc in _filterCoefficients)
            {
                sum += fc * _history[historyIndex];

                if (++historyIndex >= _history.Length)
                    historyIndex = 0;
            }

            return sum;
        }
    }
}

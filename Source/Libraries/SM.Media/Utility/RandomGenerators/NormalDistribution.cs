// -----------------------------------------------------------------------
//  <copyright file="NormalDistribution.cs" company="Henric Jungheim">
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

namespace SM.Media.Utility.RandomGenerators
{
    public class NormalDistribution
    {
        // http://en.wikipedia.org/wiki/Marsaglia_polar_method

        readonly float _mean;
        readonly IRandomGenerator _randomGenerator;
        readonly float _standardDeviation;
        float? _value;

        public NormalDistribution(IRandomGenerator randomGenerator, float mean, float standardDeviation)
        {
            if (randomGenerator == null)
                throw new ArgumentNullException("randomGenerator");
            if (standardDeviation <= 0)
                throw new ArgumentOutOfRangeException("standardDeviation");

            _randomGenerator = randomGenerator;
            _mean = mean;
            _standardDeviation = standardDeviation;
        }

        public float Next()
        {
            float result;

            if (_value.HasValue)
            {
                result = _value.Value;
                _value = null;
            }
            else
            {
                float d2;
                float u;
                float v;

                for (; ; )
                {
                    u = 2.0f * _randomGenerator.NextFloat() - 1.0f;
                    v = 2.0f * _randomGenerator.NextFloat() - 1.0f;

                    d2 = u * u + v * v;

                    if (d2 > 0.0f && d2 < 1.0f)
                        break;
                }

                var s = (float)Math.Sqrt(-2.0f * (float)Math.Log(d2) / d2);

                _value = v * s;
                result = u * s;
            }

            return result * _standardDeviation + _mean;
        }
    }
}

// -----------------------------------------------------------------------
//  <copyright file="PinkNoise.cs" company="Henric Jungheim">
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
using SM.Media.Utility.RandomGenerators;

namespace SM.Media.Audio.Generator
{
    public class PinkNoise
    {
        /// <summary>
        ///     Experimentally determined scale factor to get
        ///     output at the requested RMS amplitude.
        /// </summary>
        const float RmsScale = 0.3277f;

        readonly float[] _b = new float[7];
        readonly NormalDistribution _whiteGenerator;

        public PinkNoise(IRandomGenerator randomGenerator, float rmsAmplitude)
        {
            if (null == randomGenerator)
                throw new ArgumentNullException("randomGenerator");

            _whiteGenerator = new NormalDistribution(randomGenerator, 0, RmsScale * rmsAmplitude);
        }

        public float Next()
        {
            // http://www.firstpr.com.au/dsp/pink-noise/

            var white = _whiteGenerator.Next();

            _b[0] = 0.99886f * _b[0] + 0.0555179f * white;
            _b[1] = 0.99332f * _b[1] + 0.0750759f * white;
            _b[2] = 0.96900f * _b[2] + 0.1538520f * white;
            _b[3] = 0.86650f * _b[3] + 0.3104856f * white;
            _b[4] = 0.55000f * _b[4] + 0.5329522f * white;
            _b[5] = -0.7616f * _b[5] - 0.0168980f * white;

            var v = _b[0] + _b[1] + _b[2] + _b[3] + _b[4] + _b[5] + _b[6] + white * 0.5362f;

            _b[6] = white * 0.115926f;

            return v;
        }
    }
}

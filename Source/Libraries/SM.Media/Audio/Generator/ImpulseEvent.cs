// -----------------------------------------------------------------------
//  <copyright file="ImpulseEvent.cs" company="Henric Jungheim">
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
    class ImpulseEvent
    {
        static readonly float[] LpInput = { 1, 4, 6, 4, 1 };
        static readonly float[] LpOutput = { -0.9146797258f, 3.6978912481f, -5.6494276865f, 3.8659134219f };
        static readonly float LpGain = 5.285023688e+04f;
        readonly float _amplitude;
        readonly NormalDistribution _amplitudeModulation;
        readonly float _amplitudeRatio;
        readonly int _frequency;
        readonly int _frequency2;
        readonly int _length;
        readonly IirFilter _lpFilter;
        readonly int _peak;
        readonly IRandomGenerator _randomGenerator;
        int _phase;
        int _phase2;
        int _position;
        // http://www-users.cs.york.ac.uk/~fisher/mkfilter/

        public ImpulseEvent(IRandomGenerator randomGenerator, float amplitude, int length, int frequency, int frequency2, float amplitudeRatio)
        {
            if (null == randomGenerator)
                throw new ArgumentNullException("randomGenerator");

            _randomGenerator = randomGenerator;
            _amplitude = amplitude;
            _length = length;
            _frequency = frequency;
            _peak = _length / 3;
            _phase = _randomGenerator.NextInt();

            _frequency2 = frequency2;
            _amplitudeRatio = amplitudeRatio;
            _phase2 = _randomGenerator.NextInt();

            _amplitudeModulation = new NormalDistribution(randomGenerator, 0, Math.Abs(amplitude));
            _lpFilter = new IirFilter(LpGain, LpInput, LpOutput);
        }

        public bool IsDone
        {
            get { return _position >= _length; }
        }

        float Scale()
        {
            if (_position <= _peak)
                return _position / (float)_peak;
            return (_length - _position) / (float)(_length - _peak);
        }

        float Triangle(int phase)
        {
            if (phase > 0)
                return phase * (2f / Int32.MaxValue) - 1f;

            return 1f - phase * (2f / Int32.MinValue);
        }

        float Square(int phase)
        {
            if (phase > 0)
                return 1f;

            return -1f;
        }

        public float Next()
        {
            if (IsDone)
                return 0f;

            var scale = 1f; //Scale();

            var phase = _phase; // + (_randomGenerator.NextFloat() - 0.5f) * _frequency / 8;
            //var radians = NoiseSource.PhaseToRadian * phase;

            //var f = Square(phase); // + 0.2f * _randomGenerator.NextFloat();
            var f = Triangle(phase);

            //f += _amplitudeRatio * Triangle(_phase2);

            var noise = _amplitudeModulation.Next();
            //var lpNoise = _lpFilter.Next(f + noise);

            //var v = _amplitude * scale * scale * f * (1 + lpNoise);
            var v = noise * f;

            _phase += _frequency; //(int)(_frequency * (1f + (_randomGenerator.NextFloat() - 0.5f) * (1 / 8f)));
            _phase2 += _frequency2;

            //_phase += _frequency;
            ++_position;

            return v;
        }
    }
}

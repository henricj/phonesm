// -----------------------------------------------------------------------
//  <copyright file="ImpulseNoise.cs" company="Henric Jungheim">
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
using System.Collections.Generic;
using System.Diagnostics;
using SM.Media.Utility.RandomGenerators;

namespace SM.Media.Audio.Generator
{
    public class ImpulseNoise
    {
        const int MaxEvents = 4;
        readonly NormalDistribution _eventAmplitudeDistribution;
        readonly NormalDistribution _eventAmplitudeRatio;
        readonly NormalDistribution _eventFrequency2Distribution;
        readonly NormalDistribution _eventFrequencyDistribution;
        readonly double _eventLambda;
        readonly NormalDistribution _eventLengthDistribution;
        readonly List<ImpulseEvent> _impulseEvents = new List<ImpulseEvent>();
        readonly IRandomGenerator _randomGenerator;
        ulong _nextEvent;

        public ImpulseNoise(IRandomGenerator randomGenerator, double eventsPerSecond)
        {
            _randomGenerator = randomGenerator;

            var sampleRate = 44100;

            _eventLambda = eventsPerSecond / sampleRate;
            _eventFrequencyDistribution = new NormalDistribution(_randomGenerator, GetFrequency(300, sampleRate), GetFrequency(30, sampleRate));
            _eventFrequency2Distribution = new NormalDistribution(_randomGenerator, GetFrequency(4000, sampleRate), GetFrequency(350, sampleRate));
            _eventAmplitudeDistribution = new NormalDistribution(_randomGenerator, 0.6f, 0.3f);
            _eventAmplitudeRatio = new NormalDistribution(_randomGenerator, 0.2f, 0.02f);
            _eventLengthDistribution = new NormalDistribution(_randomGenerator, 9 * sampleRate / 1000f, 3f * sampleRate / 1000f);
        }

        static int GetFrequency(float hz, int sampleRate)
        {
            return (int)Math.Round((hz * (double)(1L << 32)) / sampleRate);
        }

        public float Next(ulong position)
        {
            var eventCount = 0;

            var sum = 0f;

            for (var j = 0; j < _impulseEvents.Count; ++j)
            {
                var impulse = _impulseEvents[j];

                if (null == impulse)
                    continue;

                sum += impulse.Next();

                if (impulse.IsDone)
                    _impulseEvents[j] = null;
                else
                    ++eventCount;
            }

            if (eventCount < MaxEvents && position > _nextEvent)
                AddEvent();

            return sum;
        }

        void AddEvent()
        {
            _nextEvent += (ulong)_randomGenerator.NextExponential(_eventLambda);

            var impulse = new ImpulseEvent(_randomGenerator, _eventAmplitudeDistribution.Next(),
                (int)Math.Round(_eventLengthDistribution.Next()),
                (int)Math.Round(_eventFrequencyDistribution.Next()),
                (int)Math.Round(_eventFrequency2Distribution.Next()), _eventAmplitudeRatio.Next());

            for (var j = 0; j < _impulseEvents.Count; ++j)
            {
                if (null != _impulseEvents[j])
                    continue;

                _impulseEvents[j] = impulse;
                impulse = null;

                break;
            }

            if (null != impulse)
            {
                _impulseEvents.Add(impulse);
                Debug.Assert(_impulseEvents.Count <= MaxEvents, "Too many impulse events");
            }
        }
    }
}

// -----------------------------------------------------------------------
//  <copyright file="IirFilter.cs" company="Henric Jungheim">
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

namespace SM.Media.Audio.Generator
{
    public class IirFilter
    {
        readonly float _gainScale;
        readonly float[] _input;
        readonly float[] _inputHistory;
        readonly float[] _output;
        readonly float[] _outputHistory;
        int _inputIndex;
        int _outputIndex;

        public IirFilter(float gain, float[] input, float[] output)
        {
            _gainScale = 1 / gain;
            _input = input;
            _output = output;

            _inputHistory = new float[_input.Length];
            _outputHistory = new float[_output.Length];
        }

        public float Next(float x)
        {
            _inputHistory[_inputIndex] = _gainScale * x;

            var sum = 0f;

            var index = _inputIndex;

            foreach (var i in _input)
            {
                sum += i * _inputHistory[index];

                if (++index >= _inputHistory.Length)
                    index = 0;
            }

            index = _outputIndex;

            foreach (var o in _output)
            {
                sum += o * _outputHistory[index];

                if (++index >= _outputHistory.Length)
                    index = 0;
            }

            _output[_outputIndex] = sum;

            if (--_outputIndex < 0)
                _outputIndex = _outputHistory.Length - 1;

            if (--_inputIndex < 0)
                _inputIndex = _inputHistory.Length - 1;

            return sum;
        }
    }
}

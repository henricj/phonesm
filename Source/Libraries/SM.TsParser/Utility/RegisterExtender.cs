// -----------------------------------------------------------------------
//  <copyright file="RegisterExtender.cs" company="Henric Jungheim">
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
using System.Diagnostics;

namespace SM.TsParser.Utility
{
    /// <summary>
    ///     Extend a slow-changing integer register from n bits (n &lt; 64) to 64 bits.  The
    ///     register must change by less than 2 ^ (n - 1) per iteration.
    /// </summary>
    sealed class RegisterExtender
    {
        readonly int _width;
        ulong _value;

        public RegisterExtender(ulong initialValue, int actualWidth)
        {
            if (actualWidth < 2 || actualWidth > 63)
                throw new ArgumentOutOfRangeException("actualWidth", "actualWidth must be between 2 and 63, inclusive.");

            _value = initialValue;
            _width = actualWidth;
        }

        public ulong Extend(ulong value)
        {
            var wrapThreshold = (1L << (_width - 1));
            var period = (1UL << _width);
            var mask = ~(period - 1);

            Debug.Assert(0 == (value & mask));

            value |= _value & mask;

            // Check to see if we have wrapped (either up or down).
            if (Math.Abs((long)(value - _value)) > wrapThreshold)
            {
                var adjustedPts = value;

                if (adjustedPts > _value)
                    adjustedPts = value - period;
                else
                    adjustedPts = value + period;

                var adjustedPtsError = Math.Abs((long)(adjustedPts - _value));
                var ptsError = Math.Abs((long)(value - _value));

                // Pick the value closest to the old one.
                if (adjustedPtsError <= ptsError)
                    value = adjustedPts;

                Debug.Assert(Math.Abs((long)(value - _value)) <= wrapThreshold);
            }

            return _value = value;
        }
    }
}

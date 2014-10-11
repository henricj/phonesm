// -----------------------------------------------------------------------
//  <copyright file="NalUnitType.cs" company="Henric Jungheim">
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

namespace SM.Media.H264
{
    // See ITU-T H.264 (04/2013) Table 7-1
    public enum NalUnitType
    {
        Uns = 0,
        Slice = 1,
        Dpa = 2,
        Dpb = 3,
        Dpc = 4,
        Idr = 5,
        Sei = 6,
        Sps = 7,
        Pps = 8,
        Aud = 9,
        EoSeq = 10,
        EoStream = 11,
        Fill = 12,
        SpsExt = 13,
        Prefix = 14,
        SubSps = 15,
        Rsv16 = 16,
        Rsv17 = 17,
        Rsv18 = 18,
        SlcAux = 19,
        SlcExt = 20,
        SlcDv = 21,
        Rsv22 = 22,
        Rsv23 = 23,
        Vdrd = 24
    }
}

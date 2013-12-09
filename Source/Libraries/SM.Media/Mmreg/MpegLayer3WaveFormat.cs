// -----------------------------------------------------------------------
//  <copyright file="MpegLayer3WaveFormat.cs" company="Henric Jungheim">
//  Copyright (c) 2012, 2013.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012, 2013 Henric Jungheim <software@henric.org>
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

namespace SM.Media.Mmreg
{
    // // See MPEGLAYER3WAVEFORMAT in Microsoft's mmreg.h
    class MpegLayer3WaveFormat : WaveFormatEx
    {
        #region Flags enum

        [Flags]
        public enum Flags : uint
        {
            PaddingIso = 0x00000000,
            PaddingOn = 0x00000001,
            PaddingOff = 0x00000002,
        }

        #endregion

        #region Id enum

        public enum Id : ushort
        {
            Unkown = 0,
            Mpeg = 1,
            ConstantFrameSize = 2
        }

        #endregion

        const int MpegLayer3WfxExtraBytes = 12;

        public uint fdwFlags;
        public ushort nBlockSize;
        public ushort nCodecDelay;
        public ushort nFramesPerBlock = 1;
        public ushort wID = (ushort)Id.Mpeg;

        public MpegLayer3WaveFormat()
        {
            wFormatTag = (ushort)WaveFormatTag.MpegLayer3;
        }

        public override ushort cbSize
        {
            get { return (ushort)(base.cbSize + MpegLayer3WfxExtraBytes); }
        }

        public override void ToBytes(IList<byte> buffer)
        {
            base.ToBytes(buffer);

            buffer.AddLe(wID);
            buffer.AddLe(fdwFlags);
            buffer.AddLe(nBlockSize);
            buffer.AddLe(nFramesPerBlock);
            buffer.AddLe(nCodecDelay);
        }
    }
}

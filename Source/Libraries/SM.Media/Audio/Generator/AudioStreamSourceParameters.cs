// -----------------------------------------------------------------------
//  <copyright file="AudioStreamSourceParameters.cs" company="Henric Jungheim">
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
    public interface IAudioStreamSourceParameters
    {
        uint SampleRate { get; set; }
        uint Channels { get; set; }
        bool Is16Bit { get; set; }
    }

    public class AudioStreamSourceParameters : IAudioStreamSourceParameters
    {
        public AudioStreamSourceParameters(uint sampleRate = 16000, uint channels = 2, bool is16Bit = false)
        {
            SampleRate = sampleRate;
            Channels = channels;
            Is16Bit = is16Bit;
        }

        #region IAudioStreamSourceParameters Members

        public uint SampleRate { get; set; }

        public uint Channels { get; set; }

        public bool Is16Bit { get; set; }

        #endregion
    }
}

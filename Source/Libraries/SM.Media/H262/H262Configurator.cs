// -----------------------------------------------------------------------
//  <copyright file="H262Configurator.cs" company="Henric Jungheim">
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

using System.Diagnostics;
using SM.Media.Configuration;

namespace SM.Media.H262
{
    public sealed class H262Configurator : ConfiguratorBase, IVideoConfigurationSource, IFrameParser
    {
        public H262Configurator(string streamDescription = null)
        {
            StreamDescription = streamDescription;
        }

        #region IFrameParser Members

        public int FrameLength
        {
            get { return 0; }
        }

        public bool Parse(byte[] buffer, int index, int length)
        {
            Configure();

            return true;
        }

        #endregion

        #region IVideoConfigurationSource Members

        public string VideoFourCc
        {
            get { return "MP2V"; }
        }

        public int? Height { get; private set; }
        public int? Width { get; private set; }

        #endregion

        public void Configure()
        {
            Name = "H.262";

#if DEBUG
            Debug.WriteLine("Configuration " + Name);
#endif
            SetConfigured();
        }
    }
}

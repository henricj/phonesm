// -----------------------------------------------------------------------
//  <copyright file="ConfiguratorBase.cs" company="Henric Jungheim">
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

using System;
using System.Threading;

namespace SM.Media.Configuration
{
    public class ConfiguratorBase : IConfigurationSource
    {
        int _isConfigured;

        #region IConfigurationSource Members

        public virtual string CodecPrivateData { get; protected set; }

        public string Name { get; protected set; }
        public string StreamDescription { get; protected set; }

        public int? Bitrate { get; protected set; }

        public bool IsConfigured
        {
            get { return 0 != _isConfigured; }
        }

        public event EventHandler ConfigurationComplete;

        #endregion

        protected void SetConfigured()
        {
            // Does ARM need memory barriers ("interlocked" is what's available in a PCL)?
            Interlocked.Exchange(ref _isConfigured, 1);

            var configurationComplete = ConfigurationComplete;

            if (null == configurationComplete)
                return;

            ConfigurationComplete = null;

            configurationComplete(this, EventArgs.Empty);
        }
    }
}

// -----------------------------------------------------------------------
//  <copyright file="H264StreamHandlerFactory.cs" company="Henric Jungheim">
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

using System.Collections.Generic;
using SM.Media.Pes;
using SM.TsParser;

namespace SM.Media.H264
{
    public class H264StreamHandlerFactory : IPesStreamFactoryInstance
    {
        static readonly byte[] Types = { TsStreamType.H264StreamType };

        #region IPesStreamFactoryInstance Members

        public ICollection<byte> SupportedStreamTypes
        {
            get { return Types; }
        }

        public PesStreamHandler Create(PesStreamParameters parameter)
        {
            return new H264StreamHandler(parameter.PesPacketPool, parameter.Pid, parameter.StreamType, parameter.NextHandler);
        }

        #endregion
    }
}

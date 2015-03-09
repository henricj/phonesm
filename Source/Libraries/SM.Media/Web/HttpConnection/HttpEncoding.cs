// -----------------------------------------------------------------------
//  <copyright file="HttpEncoding.cs" company="Henric Jungheim">
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

using System.Text;
using SM.Media.Utility.TextEncodings;

namespace SM.Media.Web.HttpConnection
{
    public interface IHttpEncoding
    {
        Encoding HeaderDecoding { get; }
        Encoding HeaderEncoding { get; }
    }

    public sealed class HttpEncoding : IHttpEncoding
    {
        readonly Encoding _decoding;
        readonly Encoding _encoding;

        public HttpEncoding(ISmEncodings encodings)
        {
            _encoding = encodings.AsciiEncoding;
            _decoding = encodings.Latin1Encoding;
        }

        #region IHttpEncoding Members

        public Encoding HeaderDecoding
        {
            get { return _decoding; }
        }

        public Encoding HeaderEncoding
        {
            get { return _encoding; }
        }

        #endregion
    }
}

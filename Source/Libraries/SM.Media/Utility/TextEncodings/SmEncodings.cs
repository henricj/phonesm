// -----------------------------------------------------------------------
//  <copyright file="SmEncodings.cs" company="Henric Jungheim">
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
using System.Diagnostics;
using System.Text;

namespace SM.Media.Utility.TextEncodings
{
    public interface ISmEncodings
    {
        Encoding AsciiEncoding { get; }
        Encoding Latin1Encoding { get; }
    }

    public class SmEncodings : ISmEncodings
    {
        internal static readonly Encoding Latin1;
        internal static readonly Encoding Ascii;

        static SmEncodings()
        {
            Ascii = GetAsciiEncoding();
            Latin1 = GetLatin1Encoding();
        }

        #region ISmEncodings Members

        public Encoding Latin1Encoding
        {
            get { return Latin1; }
        }

        public Encoding AsciiEncoding
        {
            get { return Ascii; }
        }

        #endregion

        static Encoding GetLatin1Encoding()
        {
            var decoding = GetEncoding("Windows-1252");

            if (null != decoding)
                return decoding;

            decoding = GetEncoding("iso-8859-1");
            if (null != decoding)
                return decoding;

            return new Windows1252Encoding();
        }

        static Encoding GetAsciiEncoding()
        {
            var encoding = GetEncoding("us-ascii");

            if (null != encoding)
                return encoding;

            return new AsciiEncoding();
        }

        static Encoding GetEncoding(string name)
        {
            try
            {
                return Encoding.GetEncoding(name);
            }
            catch (Exception)
            {
                Debug.WriteLine("Unable to get " + name + " encoding");
            }

            return null;
        }
    }
}

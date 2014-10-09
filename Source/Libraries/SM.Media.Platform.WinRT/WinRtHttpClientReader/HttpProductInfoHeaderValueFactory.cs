// -----------------------------------------------------------------------
//  <copyright file="HttpProductInfoHeaderValueFactory.cs" company="Henric Jungheim">
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
using System.Diagnostics;
using Windows.Web.Http.Headers;
using SM.Media.Web;

namespace SM.Media.WinRtHttpClientReader
{
    public interface IHttpProductInfoHeaderValueFactory
    {
        HttpProductInfoHeaderValue Create();
    }

    public class HttpProductInfoHeaderValueFactory : IHttpProductInfoHeaderValueFactory
    {
        readonly IUserAgent _userAgent;

        public HttpProductInfoHeaderValueFactory(IUserAgent userAgent)
        {
            if (null == userAgent)
                throw new ArgumentNullException("userAgent");

            _userAgent = userAgent;
        }

        #region IHttpProductInfoHeaderValueFactory Members

        public HttpProductInfoHeaderValue Create()
        {
            try
            {
                // What should we do?  App names often have non-ASCII characters.
                // These characters are turned into question marks by the
                // time the header hits the wire.  We do RFC 2047 encoding
                // so that the server's log winds up with something decipherable.
                // This can easily be changed by providing an alternate factory
                // or an alternate IUserAgent implementation.
                // (Note that only strings that need encoding are modified by
                // ".Rfc2047Encode()".)
                var productName = _userAgent.Name.Trim().Replace(' ', '-').Rfc2047Encode();

                var userAgent = new HttpProductInfoHeaderValue(productName, _userAgent.Version);

                return userAgent;
            }
            catch (FormatException ex)
            {
                Debug.WriteLine("HttpDefaults.DefaultUserAgentFactory({0}, {1}) unable to construct HttpProductInfoHeaderValue: {2}",
                    _userAgent.Name, _userAgent.Version, ex.Message);

                return null;
            }
        }

        #endregion
    }
}

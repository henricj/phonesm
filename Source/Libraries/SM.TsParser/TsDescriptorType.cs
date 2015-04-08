// -----------------------------------------------------------------------
//  <copyright file="TsDescriptorType.cs" company="Henric Jungheim">
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

namespace SM.TsParser
{
    public class TsDescriptorType : IEquatable<TsDescriptorType>
    {
        #region Delegates

        public delegate TsDescriptor TsDescriptorFactory(TsDescriptorType type, byte[] buffer, int offset, int length);

        #endregion

        readonly byte _code;
        readonly string _description;
        readonly TsDescriptorFactory _factory;

        public TsDescriptorType(byte code, string description, TsDescriptorFactory factory = null)
        {
            _code = code;
            _description = description;
            _factory = factory ?? DefaultFactory;
        }

        public byte Code
        {
            get { return _code; }
        }

        public string Description
        {
            get { return _description; }
        }

        #region IEquatable<TsDescriptorType> Members

        public bool Equals(TsDescriptorType other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;

            return _code == other._code;
        }

        #endregion

        public TsDescriptor Create(byte[] buffer, int offset, int length)
        {
            return _factory(this, buffer, offset, length);
        }

        TsDescriptor DefaultFactory(TsDescriptorType type, byte[] buffer, int offset, int length)
        {
            return new TsDescriptor(type, length);
        }

        public override int GetHashCode()
        {
            return _code.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as TsDescriptorType);
        }

        public override string ToString()
        {
            return _code + ":" + _description;
        }
    }
}

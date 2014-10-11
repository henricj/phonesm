// -----------------------------------------------------------------------
//  <copyright file="NalUnitTypeDescriptor.cs" company="Henric Jungheim">
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

namespace SM.Media.H264
{
    public sealed class NalUnitTypeDescriptor : IEquatable<NalUnitTypeDescriptor>
    {
        readonly string _description;
        readonly string _name;
        readonly NalUnitType _type;

        public NalUnitTypeDescriptor(NalUnitType type, string name, string description)
        {
            _type = type;
            _name = name;
            _description = description;
        }

        public NalUnitType Type
        {
            get { return _type; }
        }

        public string Name
        {
            get { return _name; }
        }

        public string Description
        {
            get { return _description; }
        }

        #region IEquatable<NalUnitTypeDescriptor> Members

        public bool Equals(NalUnitTypeDescriptor other)
        {
            if (ReferenceEquals(this, other))
                return true;

            if (ReferenceEquals(null, other))
                return false;

            return _type == other._type;
        }

        #endregion

        public override int GetHashCode()
        {
            return (int)_type;
        }

        public override bool Equals(object obj)
        {
            var other = obj as NalUnitTypeDescriptor;

            if (ReferenceEquals(null, other))
                return false;

            return Equals(other);
        }

        public static bool operator ==(NalUnitTypeDescriptor a, NalUnitTypeDescriptor b)
        {
            if (ReferenceEquals(a, b))
                return true;

            if (ReferenceEquals(a, null))
                return false;

            return a.Equals(b);
        }

        public static bool operator !=(NalUnitTypeDescriptor a, NalUnitTypeDescriptor b)
        {
            return !(a == b);
        }

        public override string ToString()
        {
            return string.Format("{0}/{1}", _type, _name);
        }
    }
}

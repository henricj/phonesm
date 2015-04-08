// -----------------------------------------------------------------------
//  <copyright file="TsDescriptors.cs" company="Henric Jungheim">
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

using System.Diagnostics;
using System.IO;

namespace SM.TsParser
{
    public static class TsDescriptors
    {
        public static void WriteDescriptors(TextWriter writer, byte[] buffer, int offset, int length)
        {
            while (length > 0)
            {
                if (length < 2)
                {
                    writer.WriteLine("Unused buffer " + length);
                    break;
                }

                var code = buffer[offset];
                var descriptorLength = buffer[offset + 1];

                offset += 2;
                length -= 2;

                var type = TsDescriptorTypes.GetDescriptorType(code);

                if (null == type)
                    writer.Write(code + ":Unknown");
                else
                    writer.Write(type);

                if (length < descriptorLength)
                {
                    writer.WriteLine(" " + descriptorLength + " exceeds buffer (" + length + " remaining)");
                    break;
                }

                length -= descriptorLength;
                offset += descriptorLength;
            }
        }

        [Conditional("DEBUG")]
        public static void DebugWrite(byte[] buffer, int offset, int length)
        {
            using (var sw = new StringWriter())
            {
                WriteDescriptors(sw, buffer, offset, length);

                Debug.WriteLine(sw.ToString());
            }
        }
    }
}

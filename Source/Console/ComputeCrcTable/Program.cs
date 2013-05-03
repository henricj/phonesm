// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Henric Jungheim">
//  Copyright (c) 2012, 2013.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012, 2013 Henric Jungheim <software@henric.org>
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
using System.Collections.Generic;

namespace ComputeCrcTable
{
    class Program
    {
        static IEnumerable<uint> GenerateCrc32(uint polynomial)
        {
            var crcTable = new uint[256];

            for (var i = 0u; i < crcTable.Length; ++i)
            {
                var crc = i << 24;

                for (var j = 0; j < 8; ++j)
                {
                    var wasSet = 0 != (crc & (1 << 31));

                    crc <<= 1;

                    if (wasSet)
                        crc ^= polynomial;
                }

                crcTable[i] = crc;
            }

            return crcTable;
        }

        static IEnumerable<ushort> GenerateCrc16(ushort polynomial)
        {
            var crcTable = new ushort[256];

            for (var i = 0; i < crcTable.Length; ++i)
            {
                var crc = (ushort)(i << 8);

                for (var j = 0; j < 8; ++j)
                {
                    var wasSet = 0 != (crc & (1 << 15));

                    crc <<= 1;

                    if (wasSet)
                        crc ^= polynomial;
                }

                crcTable[i] = crc;
            }

            return crcTable;
        }

        static void Main(string[] args)
        {
            //DumpCrc32Table(0x04C11DB7u);
            //DumpCrc32Table(0xEDB88320u);

            DumpCrc16Table(0x8005);
            DumpCrc16Table(0x1021);
        }

        static void DumpCrc32Table(uint poly)
        {
            var table = GenerateCrc32(poly);

            Console.WriteLine("// CRC32 Lookup for 0x{0:x8}", poly);
            Console.WriteLine();
            Console.WriteLine("static readonly uint[] Crc32Table_0x{0:x8} =", poly);
            Console.WriteLine("{");
            Console.Write("    ");
            var count = 0;
            foreach (var c in table)
            {
                Console.Write("0x{0:x8}, ", c);

                ++count;

                if (0 == (count & 0x03))
                {
                    count = 0;
                    Console.WriteLine();
                    Console.Write("    ");
                }
            }

            Console.WriteLine("};");
        }

        static void DumpCrc16Table(ushort poly)
        {
            var table = GenerateCrc16(poly);

            Console.WriteLine("// CRC16 Lookup for 0x{0:x4}", poly);
            Console.WriteLine();
            Console.WriteLine("static readonly ushort[] Crc16Table_0x{0:x4} =", poly);
            Console.WriteLine("{");
            Console.Write("    ");
            var count = 0;
            foreach (var c in table)
            {
                Console.Write("0x{0:x4}, ", c);

                ++count;

                if (0 == (count & 0x07))
                {
                    count = 0;
                    Console.WriteLine();
                    Console.Write("    ");
                }
            }

            Console.WriteLine("};");
        }
    }
}
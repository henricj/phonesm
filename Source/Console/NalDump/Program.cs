﻿// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Henric Jungheim">
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SM.Media.H264;

namespace NalDump
{
    class Program
    {
        const int MAXNALUSIZE = 64000; // This is straight from the H.264.2 sample code's nalucommon.h 
        const int MinRemainingSize = MAXNALUSIZE * 2; // We make sure we have twice as much available before starting a new NAL unit.

        static readonly Encoding NoBomUtf8 = new UTF8Encoding(false, true);

        static async Task Parse(string filename)
        {
            var buffer = new byte[MAXNALUSIZE * 4];
            var offset = 0;
            var length = 0;
            var isEof = false;

            var logFilename = Path.ChangeExtension(filename, ".log");

            if (string.Equals(filename, logFilename, StringComparison.InvariantCultureIgnoreCase))
                return;

            using (var outputFile = new FileStream(logFilename, FileMode.Create, FileAccess.Write, FileShare.Read, 1024 * 1024, FileOptions.SequentialScan))
            using (var output = new StreamWriter(outputFile, NoBomUtf8, 512 * 1024))
            {
                var localOutput = output;

                var rbspDecoder = new RbspDecoder();

                rbspDecoder.CompletionHandler += b => PrintNalUnit(localOutput, b.ToArray());

                var parser = new NalUnitParser(n => (b, o, l, e) => rbspDecoder.Parse(b, o, l, e));

                using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 512 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    for (; ; )
                    {
                        var bytesRead = await stream.ReadAsync(buffer, length, buffer.Length - length).ConfigureAwait(false);

                        if (bytesRead < 1)
                        {
                            isEof = true;

                            if (length < 1)
                                break;
                        }
                        else
                            length += bytesRead;

                        while ((isEof && length > 0) || length >= MinRemainingSize)
                        {
                            var completedLength = parser.Parse(buffer, offset, length, isEof);

                            if (completedLength < 1)
                            {
                                if (isEof)
                                    return;

                                break;
                            }

                            if (completedLength >= length)
                            {
                                offset = 0;
                                length = 0;

                                break;
                            }

                            offset += completedLength;
                            length -= completedLength;
                        }

                        if (length < 1)
                        {
                            offset = 0;
                            continue;
                        }

                        if (offset > 0)
                        {
                            Array.Copy(buffer, offset, buffer, 0, length);
                            offset = 0;
                        }
                    }
                }
            }
        }

        static bool PrintNalUnit(TextWriter writer, byte[] nalUnit)
        {
            var type = nalUnit[0] & 0x1f;

            var nalUnitType = NalUnitTypes.GetNalUnitType((NalUnitType)type);

            var typeName = null == nalUnitType ? type.ToString(NumberFormatInfo.InvariantInfo) : nalUnitType.Name;

            writer.WriteLine("NALU({0}/{1}/{2}): {3}", nalUnit.Length, type, typeName, BitConverter.ToString(nalUnit, 0, nalUnit.Length));

            return true;
        }

        static void Main(string[] args)
        {
            if (args.Length < 1)
                return;

            foreach (var arg in args)
            {
                try
                {
                    var sw = Stopwatch.StartNew();

                    Parse(arg).Wait();

                    sw.Stop();

                    Console.WriteLine("{0} processed in {1}", arg, sw.Elapsed);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Parsing of {0} failed: {1}", arg, ex.Message);
                }
            }
        }
    }
}

// -----------------------------------------------------------------------
//  <copyright file="M3U8ParserExtensions.cs" company="Henric Jungheim">
//  Copyright (c) 2012.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012 Henric Jungheim <software@henric.org>
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
using System.IO;
using System.Text;

namespace SM.Media.M3U8
{
    public static class M3U8ParserExtensions
    {
        /// <summary>
        /// </summary>
        /// <param name="parser"> </param>
        /// <param name="stream"> </param>
        /// <param name="encoding"> </param>
        public static void Parse(this M3U8Parser parser, Stream stream, Encoding encoding = null)
        {
            if (null == encoding)
                encoding = Encoding.UTF8;

            using (var sr = new StreamReader(stream, encoding))
            {
                Parse(parser, sr);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="parser"> </param>
        /// <param name="textReader"> </param>
        public static void Parse(this M3U8Parser parser, TextReader textReader)
        {
            parser.Parse(GetExtendedLines(textReader));
        }

        /// <summary>
        ///     Read a text file a line at a time, combining lines ending with "\".  Leading and trailing
        ///     whitespace is trimmed.
        /// </summary>
        /// <param name="textReader"> </param>
        /// <returns> </returns>
        static IEnumerable<string> GetExtendedLines(TextReader textReader)
        {
            var eof = false;
            var sb = new StringBuilder();

            while (!eof)
            {
                sb.Length = 0;

                string line = null;

                for (;;)
                {
                    line = textReader.ReadLine();

                    if (null == line)
                    {
                        eof = true;
                        break;
                    }

                    line = line.Trim();

                    if (!line.EndsWith(@"\"))
                        break;

                    if (line.Length < 1)
                        continue;

                    if (sb.Length > 0)
                        sb.Append(' ');

                    sb.Append(line.Substring(0, line.Length - 1));
                }

                if (sb.Length > 0)
                {
                    sb.Append(' ');

                    sb.Append(line);

                    line = sb.ToString();

                    sb.Length = 0;
                }

                if (null != line)
                    yield return line;
            }
        }
    }
}

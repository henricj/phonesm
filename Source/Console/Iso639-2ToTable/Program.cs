// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Henric Jungheim">
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
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Iso639_2ToTable
{
    class Program
    {
        static readonly Uri LocIso639_2 = new Uri("http://www.loc.gov/standards/iso639-2/ISO-639-2_utf-8.txt");

        static async Task ConvertAsync(Uri url)
        {
            using (var client = new HttpClient())
            {
                using (var stream = await client.GetStreamAsync(url).ConfigureAwait(false))
                using (var sr = new StreamReader(stream))
                {
                    for (; ; )
                    {
                        var line = await sr.ReadLineAsync().ConfigureAwait(false);

                        if (null == line)
                            break;

                        var split = line.Split('|');

                        if (5 != split.Length)
                            continue;

                        var alpha3bib = split[0];
                        var alpha3term = split[1];
                        var alpha2 = split[2];
                        var englishName = split[3];

                        if (string.IsNullOrEmpty(alpha2))
                            continue;

                        Debug.WriteLine("{0} {1} {2} {3}", alpha3bib, alpha3term, alpha2, englishName);

                        if (!string.IsNullOrWhiteSpace(alpha3bib))
                            OutputMapping(alpha3bib, alpha2, englishName);

                        if (!string.IsNullOrWhiteSpace(alpha3term))
                            OutputMapping(alpha3term, alpha2, englishName);
                    }
                }
            }
        }

        static void OutputMapping(string alpha3, string alpha2, string englishName)
        {
            Console.WriteLine("   {{ \"{0}\", \"{1}\" }}, // {2}", alpha3, alpha2, englishName);
        }

        static void Main(string[] args)
        {
            try
            {
                Uri url;

                if (args.Length < 1)
                    url = LocIso639_2;
                else
                    url = new Uri(args[0]);

                ConvertAsync(url).Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}

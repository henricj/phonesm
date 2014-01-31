// -----------------------------------------------------------------------
//  <copyright file="PlsParser.cs" company="Henric Jungheim">
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SM.Media.Pls
{
    public class PlsParser
    {
        readonly Dictionary<int, PlsTrack> _tracks = new Dictionary<int, PlsTrack>();
        bool _foundPlaylist;
        int? _numberOfEntries;
        PlsTrack[] _orderedTracks;
        int? _version;

        public int? Version
        {
            get { return _version; }
        }

        public ICollection<PlsTrack> Tracks
        {
            get { return _orderedTracks; }
        }

        public async Task<bool> Parse(TextReader tr)
        {
            _tracks.Clear();
            _numberOfEntries = null;
            _version = null;
            _foundPlaylist = false;
            _orderedTracks = null;

            for (; ; )
            {
                var line = await tr.ReadLineAsync().ConfigureAwait(false);

                if (null == line)
                {
                    _orderedTracks = _tracks.OrderBy(kv => kv.Key).Select(kv => kv.Value).ToArray();

                    if (_numberOfEntries.HasValue && _numberOfEntries.Value != _orderedTracks.Length)
                        Debug.WriteLine("PlsParser.Parse() entries mismatch ({0} != {1})", _numberOfEntries, _orderedTracks.Length);

                    return _foundPlaylist;
                }

                // Skip blank lines
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                line = line.Trim();

                if (line.Length < 1)
                    continue;

                var firstChar = line[0];

                // Skip comments
                if (';' == firstChar || '#' == firstChar)
                    continue;

                if (!_foundPlaylist)
                {
                    if (!string.Equals("[playlist]", line, StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.WriteLine("PlsParser.Parser() invalid line while expecting parser: " + line);
                        continue;
                    }

                    _foundPlaylist = true;
                }
                else
                {
                    var index = line.IndexOf('=');

                    if (index <= 0 || index + 1 >= line.Length)
                    {
                        Debug.WriteLine("PlsParser.Parser() invalid line: " + line);
                        continue;
                    }

                    var key = line.Substring(0, index).Trim();
                    var value = line.Substring(index + 1, line.Length - (index + 1)).Trim();

                    if (string.Equals("NumberOfEntries", key, StringComparison.OrdinalIgnoreCase))
                        HandleNumberOfEntries(value, line);
                    else if (string.Equals("Version", key, StringComparison.OrdinalIgnoreCase))
                        HandleVersion(value, line);
                    else
                        HandleTrack(line, key, value);
                }
            }
        }

        static int FindStartOfTrackNumber(string key)
        {
            for (var i = key.Length - 1; i >= 0; --i)
            {
                if (!char.IsDigit(key[i]))
                {
                    if (i < key.Length - 1)
                        return i;

                    break;
                }
            }

            return -1;
        }

        void HandleTrack(string line, string key, string value)
        {
            var index = FindStartOfTrackNumber(key);

            if (index < 0)
            {
                Debug.WriteLine("PlsParser.HandleTrack() unable to find track number: " + line);
                return;
            }

            var name = key.Substring(0, index + 1).Trim();
            var track = key.Substring(index + 1, key.Length - (index + 1));

            int number;
            if (!int.TryParse(track, NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
            {
                Debug.WriteLine("PlsParser.HandleTrack() invalid track number: " + line);
                return;
            }

            PlsTrack plsTrack;
            if (!_tracks.TryGetValue(number, out plsTrack))
            {
                plsTrack = new PlsTrack();
                _tracks[number] = plsTrack;
            }

            if (string.Equals("File", name))
            {
                if (null != plsTrack.File)
                {
                    Debug.WriteLine("PlsParser.Parser() duplicate file property for entry {0}: {1}", number, line);
                    return;
                }

                plsTrack.File = value;
            }
            else if (string.Equals("Title", name))
            {
                if (null != plsTrack.Title)
                {
                    Debug.WriteLine("PlsParser.Parser() duplicate title property for entry {0}: {1}", number, line);
                    return;
                }

                plsTrack.Title = value;
            }
            else if (string.Equals("Length", name))
            {
                if (plsTrack.Length.HasValue)
                {
                    Debug.WriteLine("PlsParser.Parser() duplicate length property for entry {0}: {1}", number, line);
                    return;
                }

                decimal length;

                if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out length))
                {
                    Debug.WriteLine("PlsParser.Parser() invalid length property for entry {0}: {1}", number, line);
                    return;
                }

                try
                {
                    plsTrack.Length = TimeSpan.FromSeconds((double)length);
                }
                catch (InvalidCastException)
                {
                    Debug.WriteLine("PlsParser.Parser() invalid numeric length property for entry {0}: {1}", number, line);
                }
            }
            else
                Debug.WriteLine("PlsParser.Parse() unknown property: " + line);
        }

        void HandleVersion(string value, string line)
        {
            int number;

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
            {
                if (_version.HasValue)
                {
                    Debug.WriteLine("PlsParser.Parser() repeated version: " + line);
                    return;
                }

                _version = number;
            }
            else
                Debug.WriteLine("PlsParser.Parser() invalid NumberOfEntries: " + line);
        }

        void HandleNumberOfEntries(string value, string line)
        {
            int number;

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
            {
                if (_numberOfEntries.HasValue)
                {
                    Debug.WriteLine("PlsParser.Parser() repeated NumberOfEntries: " + line);
                    return;
                }

                _numberOfEntries = number;
            }
            else
                Debug.WriteLine("PlsParser.Parser() invalid NumberOfEntries: " + line);
        }
    }
}

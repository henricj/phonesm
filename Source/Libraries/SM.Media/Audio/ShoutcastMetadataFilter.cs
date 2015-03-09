// -----------------------------------------------------------------------
//  <copyright file="ShoutcastMetadataFilter.cs" company="Henric Jungheim">
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
using SM.Media.Metadata;
using SM.Media.Utility;

namespace SM.Media.Audio
{
    public class ShoutcastMetadataFilter : IAudioParser
    {
        public static bool UseParseByPairs = true;
        readonly IAudioParser _audioParser;
        readonly MemoryStream _buffer = new MemoryStream();
        readonly int _interval;
        readonly Action<ITrackMetadata> _reportMetadata;
        int _metadataLength;
        int _remainingData;
        State _state = State.Data;

        public ShoutcastMetadataFilter(IAudioParser audioParser, Action<ITrackMetadata> reportMetadata, int interval)
        {
            if (null == audioParser)
                throw new ArgumentNullException("audioParser");
            if (null == reportMetadata)
                throw new ArgumentNullException("reportMetadata");
            if (interval < 1)
                throw new ArgumentOutOfRangeException("interval", "must be positive");

            _audioParser = audioParser;
            _reportMetadata = reportMetadata;
            _interval = interval;

            _remainingData = _interval;
        }

        #region IAudioParser Members

        public TimeSpan StartPosition
        {
            get { return _audioParser.StartPosition; }
            set { _audioParser.StartPosition = value; }
        }

        public TimeSpan? Position
        {
            get { return _audioParser.Position; }
            set { _audioParser.Position = value; }
        }

        public void FlushBuffers()
        {
            if (_buffer.Length > 0)
                ProcessMetadata(_buffer);

            _audioParser.FlushBuffers();

            _state = State.Data;

            _buffer.SetLength(0);
            _remainingData = _interval;
        }

        public void Dispose()
        {
            _audioParser.Dispose();
        }

        public void ProcessData(byte[] buffer, int offset, int length)
        {
            while (length > 0)
            {
                switch (_state)
                {
                    case State.Data:
                        if (_remainingData > length)
                        {
                            _audioParser.ProcessData(buffer, offset, length);

                            _remainingData -= length;

                            return;
                        }

                        if (_remainingData > 0)
                        {
                            _audioParser.ProcessData(buffer, offset, _remainingData);

                            length -= _remainingData;
                            offset += _remainingData;

                            _remainingData = _interval;
                        }

                        _state = State.MetadataLength;

                        break;
                    case State.MetadataLength:
                        _metadataLength = buffer[offset] * 16;

                        --length;
                        ++offset;

                        _state = _metadataLength > 0 ? State.Metadata : State.Data;

                        break;
                    case State.Metadata:
                        if (_metadataLength > length)
                        {
                            _buffer.Write(buffer, offset, length);

                            _metadataLength -= length;

                            return;
                        }

                        if (_metadataLength > 0)
                        {
                            _buffer.Write(buffer, offset, _metadataLength);

                            length -= _metadataLength;
                            offset += _metadataLength;

                            _metadataLength = 0;
                        }

                        if (_buffer.Length > 0)
                            ProcessMetadata(_buffer);

                        _state = State.Data;

                        break;
                }
            }
        }

        #endregion

        protected virtual void ProcessMetadata(Stream stream)
        {
            Debug.WriteLine("ShoutcastMetadataFilter.ProcessMetadata() length " + stream.Position);

            if (stream.Length < 1)
                return;

            try
            {
                stream.Seek(0, SeekOrigin.Begin);

                var reader = new StreamReader(stream);

                var value = reader.ReadToEnd();

                if (!string.IsNullOrWhiteSpace(value))
                    ParseStringMetadata(value.TrimEnd('\0'));
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ShoutcastMetadataFilter.ProcessMetadata() failed: " + ex.ExtendedMessage());
            }
            finally
            {
                stream.SetLength(0);
            }
        }

        protected virtual void ParseStringMetadata(string metadata)
        {
            Debug.WriteLine("ShoutcastMetadataFilter.ParseStringMetadata(): " + metadata);

            var trackMetadata = new TrackMetadata { TimeStamp = _audioParser.Position };

            if (UseParseByPairs)
                ParseByPairs(metadata, trackMetadata);
            else
                ParseByQuotes(metadata, trackMetadata);

            _reportMetadata(trackMetadata);
        }

        protected virtual void ParseByQuotes(string metadata, TrackMetadata trackMetadata)
        {
            var index = 0;

            for (; ; )
            {
                var equalSign = metadata.IndexOf('=', index);

                if (equalSign <= index)
                    break;

                var name = metadata.Substring(index, equalSign - index).Trim();

                var firstQuote = metadata.IndexOf('\'', equalSign);

                if (firstQuote < 0 || firstQuote + 1 >= metadata.Length)
                    break;

                var lastQuote = metadata.IndexOf('\'', firstQuote + 1);

                if (lastQuote < 0)
                    break;

                var value = metadata.Substring(firstQuote + 1, lastQuote - firstQuote - 1);

                AddNameValueProperty(trackMetadata, name, value);

                var semicolon = metadata.IndexOf(';', lastQuote);

                if (semicolon < 0)
                    break;

                index = semicolon + 1;

                if (index >= metadata.Length)
                    break;
            }
        }

        protected virtual void ParseByPairs(string metadata, TrackMetadata trackMetadata)
        {
            var index = 0;

            for (; ; )
            {
                var equalQuote = metadata.IndexOf("='", index, StringComparison.Ordinal);

                if (equalQuote <= index)
                    break;

                var name = metadata.Substring(index, equalQuote - index).Trim();

                var quoteSemi = metadata.IndexOf("';", equalQuote, StringComparison.Ordinal);

                var isLast = false;

                if (quoteSemi < 0)
                {
                    quoteSemi = metadata.LastIndexOf('\'');

                    if (quoteSemi < 0 || quoteSemi <= equalQuote + 1)
                        break;

                    isLast = true;
                }

                var valueStart = equalQuote + 2;

                var value = metadata.Substring(valueStart, quoteSemi - valueStart);

                AddNameValueProperty(trackMetadata, name, value);

                if (isLast)
                    break;

                index = quoteSemi + 2;

                if (index >= metadata.Length)
                    break;
            }
        }

        protected virtual void AddNameValueProperty(TrackMetadata trackMetadata, string name, string value)
        {
            Debug.WriteLine("ShoutcastMetadataFilter.AddNameValueProperty(): " + name + "=" + value);

            value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();

            switch (name.ToLowerInvariant())
            {
                case "streamtitle":
                    trackMetadata.Title = value;
                    break;
                case "streamurl":
                    break;
            }
        }

        #region Nested type: State

        enum State
        {
            Data,
            MetadataLength,
            Metadata
        }

        #endregion
    }
}

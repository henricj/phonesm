﻿// -----------------------------------------------------------------------
//  <copyright file="PersistentSettings.cs" company="Henric Jungheim">
//  Copyright (c) 2012-2016.
//  <author>Henric Jungheim</author>
//  </copyright>
// -----------------------------------------------------------------------
// Copyright (c) 2012-2016 Henric Jungheim <software@henric.org>
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
using System.IO.IsolatedStorage;

namespace NasaTv
{
    public sealed class PersistentSettings
    {
        const string UrlKey = "Url";
        static readonly Uri DefaultUrl = new Uri("http://iphone-streaming.ustream.tv/uhls/6540154/streams/live/iphone/playlist.m3u8");
        readonly IsolatedStorageSettings _settings = IsolatedStorageSettings.ApplicationSettings;

        public Uri DefaultPlaylistUrl
        {
            get { return DefaultUrl; }
        }

        public Uri PlaylistUrl
        {
            get
            {
                object value;
                if (_settings.TryGetValue(UrlKey, out value))
                {
                    var url = value as Uri;
                    if (null != url)
                        return url;
                }

                return DefaultUrl;
            }

            set
            {
                if (value == DefaultUrl)
                    _settings.Remove(UrlKey);
                else
                    _settings[UrlKey] = value;
            }
        }

        public void ResetToDefaults()
        {
            _settings.Remove(UrlKey);
        }
    }
}

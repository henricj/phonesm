// -----------------------------------------------------------------------
//  <copyright file="AudioStreamFactory.cs" company="Henric Jungheim">
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
using System.Globalization;
using Microsoft.PlayerFramework;
using SM.Media.Configuration;

namespace SM.Media.MediaPlayer
{
    public static class AudioStreamFactory
    {
        public static AudioStream CreateAudioStream(this IAudioConfigurationSource audioConfigurationSource)
        {
            var languageTag = audioConfigurationSource.GetLanguage();

            if (string.IsNullOrWhiteSpace(languageTag))
                return new AudioStream();

            var languageName = languageTag;

            try
            {
                var ci = new CultureInfo(languageTag);

                var nativeName = ci.NativeName;
                var displayName = ci.DisplayName;

                Debug.WriteLine("Language: " + languageTag + " -> " + languageName + '/' + displayName);

                if (nativeName == displayName)
                    languageName = nativeName;
                else
                    languageName = nativeName + '/' + displayName;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("AudioStreamFactory.CreateAudioStream() failed: " + ex.Message);
            }

            return new AudioStream(languageName, languageTag);
        }
    }
}

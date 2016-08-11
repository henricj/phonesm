// -----------------------------------------------------------------------
//  <copyright file="TsIso639LanguageDescriptor.cs" company="Henric Jungheim">
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
using System.Text;
using SM.Media.Utility.TextEncodings;

namespace SM.Media.TransportStream.TsParser.Descriptor
{
    public class TsIso639LanguageDescriptor : TsDescriptor
    {
        public static readonly TsDescriptorType DescriptorType = new TsDescriptorType(10, "ISO 639 language");
        readonly Language[] _languages;

        public TsIso639LanguageDescriptor(Language[] languages)
            : base(DescriptorType)
        {
            if (null == languages)
                throw new ArgumentNullException(nameof(languages));

            _languages = languages;
        }

        public Language[] Languages
        {
            get { return _languages; }
        }

        #region Nested type: AudioType

        enum AudioType
        {
            // ISO/IEC 13818-1:2007 Table 2-60
            Undefined = 0,
            Clean_effects = 1,
            Hearing_impaired = 2,
            Visual_impaired_commentary = 3
        }

        #endregion

        #region Nested type: Language

        public class Language
        {
            readonly byte _audioType;
            readonly string _iso639;

            public Language(string iso639, byte audioType)
            {
                if (iso639 == null)
                    throw new ArgumentNullException(nameof(iso639));

                _iso639 = iso639;
                _audioType = audioType;
            }

            public string Iso639_2
            {
                get { return _iso639; }
            }

            public byte AudioType
            {
                get { return _audioType; }
            }
        }

        #endregion
    }

    public class TsIso639LanguageDescriptorFactory : TsDescriptorFactoryInstanceBase
    {
        const int BlockLength = 4;
        readonly Encoding _latin1;

        public TsIso639LanguageDescriptorFactory(ISmEncodings smEncodings)
            : base(TsIso639LanguageDescriptor.DescriptorType)
        {
            if (null == smEncodings)
                throw new ArgumentNullException(nameof(smEncodings));

            _latin1 = smEncodings.Latin1Encoding;
        }

        public override TsDescriptor Create(byte[] buffer, int offset, int length)
        {
            if (length < BlockLength)
                return null;

            var i = offset;
            var languages = new TsIso639LanguageDescriptor.Language[length / BlockLength];
            var languageIndex = 0;

            while (length >= BlockLength)
            {
                var language = _latin1.GetString(buffer, i, 3);

                var audio_type = buffer[3];

                languages[languageIndex++] = new TsIso639LanguageDescriptor.Language(language, audio_type);

                i += BlockLength;
                length -= BlockLength;
            }

            return new TsIso639LanguageDescriptor(languages);
        }
    }
}

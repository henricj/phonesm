// -----------------------------------------------------------------------
//  <copyright file="Iso639_2Normalization.cs" company="Henric Jungheim">
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
using System.Collections.Generic;

namespace SM.Media.Utility
{
    public static class Iso639_2Normalization
    {
        // Generated from http://www.loc.gov/standards/iso639-2/ISO-639-2_utf-8.txt
        static readonly Dictionary<string, string> ThreeToTwo
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "aar", "aa" }, // Afar
                { "abk", "ab" }, // Abkhazian
                { "afr", "af" }, // Afrikaans
                { "aka", "ak" }, // Akan
                { "alb", "sq" }, // Albanian
                { "sqi", "sq" }, // Albanian
                { "amh", "am" }, // Amharic
                { "ara", "ar" }, // Arabic
                { "arg", "an" }, // Aragonese
                { "arm", "hy" }, // Armenian
                { "hye", "hy" }, // Armenian
                { "asm", "as" }, // Assamese
                { "ava", "av" }, // Avaric
                { "ave", "ae" }, // Avestan
                { "aym", "ay" }, // Aymara
                { "aze", "az" }, // Azerbaijani
                { "bak", "ba" }, // Bashkir
                { "bam", "bm" }, // Bambara
                { "baq", "eu" }, // Basque
                { "eus", "eu" }, // Basque
                { "bel", "be" }, // Belarusian
                { "ben", "bn" }, // Bengali
                { "bih", "bh" }, // Bihari languages
                { "bis", "bi" }, // Bislama
                { "bos", "bs" }, // Bosnian
                { "bre", "br" }, // Breton
                { "bul", "bg" }, // Bulgarian
                { "bur", "my" }, // Burmese
                { "mya", "my" }, // Burmese
                { "cat", "ca" }, // Catalan; Valencian
                { "cha", "ch" }, // Chamorro
                { "che", "ce" }, // Chechen
                { "chi", "zh" }, // Chinese
                { "zho", "zh" }, // Chinese
                { "chu", "cu" }, // Church Slavic; Old Slavonic; Church Slavonic; Old Bulgarian; Old Church Slavonic
                { "chv", "cv" }, // Chuvash
                { "cor", "kw" }, // Cornish
                { "cos", "co" }, // Corsican
                { "cre", "cr" }, // Cree
                { "cze", "cs" }, // Czech
                { "ces", "cs" }, // Czech
                { "dan", "da" }, // Danish
                { "div", "dv" }, // Divehi; Dhivehi; Maldivian
                { "dut", "nl" }, // Dutch; Flemish
                { "nld", "nl" }, // Dutch; Flemish
                { "dzo", "dz" }, // Dzongkha
                { "eng", "en" }, // English
                { "epo", "eo" }, // Esperanto
                { "est", "et" }, // Estonian
                { "ewe", "ee" }, // Ewe
                { "fao", "fo" }, // Faroese
                { "fij", "fj" }, // Fijian
                { "fin", "fi" }, // Finnish
                { "fre", "fr" }, // French
                { "fra", "fr" }, // French
                { "fry", "fy" }, // Western Frisian
                { "ful", "ff" }, // Fulah
                { "geo", "ka" }, // Georgian
                { "kat", "ka" }, // Georgian
                { "ger", "de" }, // German
                { "deu", "de" }, // German
                { "gla", "gd" }, // Gaelic; Scottish Gaelic
                { "gle", "ga" }, // Irish
                { "glg", "gl" }, // Galician
                { "glv", "gv" }, // Manx
                { "gre", "el" }, // Greek, Modern (1453-)
                { "ell", "el" }, // Greek, Modern (1453-)
                { "grn", "gn" }, // Guarani
                { "guj", "gu" }, // Gujarati
                { "hat", "ht" }, // Haitian; Haitian Creole
                { "hau", "ha" }, // Hausa
                { "heb", "he" }, // Hebrew
                { "her", "hz" }, // Herero
                { "hin", "hi" }, // Hindi
                { "hmo", "ho" }, // Hiri Motu
                { "hrv", "hr" }, // Croatian
                { "hun", "hu" }, // Hungarian
                { "ibo", "ig" }, // Igbo
                { "ice", "is" }, // Icelandic
                { "isl", "is" }, // Icelandic
                { "ido", "io" }, // Ido
                { "iii", "ii" }, // Sichuan Yi; Nuosu
                { "iku", "iu" }, // Inuktitut
                { "ile", "ie" }, // Interlingue; Occidental
                { "ina", "ia" }, // Interlingua (International Auxiliary Language Association)
                { "ind", "id" }, // Indonesian
                { "ipk", "ik" }, // Inupiaq
                { "ita", "it" }, // Italian
                { "jav", "jv" }, // Javanese
                { "jpn", "ja" }, // Japanese
                { "kal", "kl" }, // Kalaallisut; Greenlandic
                { "kan", "kn" }, // Kannada
                { "kas", "ks" }, // Kashmiri
                { "kau", "kr" }, // Kanuri
                { "kaz", "kk" }, // Kazakh
                { "khm", "km" }, // Central Khmer
                { "kik", "ki" }, // Kikuyu; Gikuyu
                { "kin", "rw" }, // Kinyarwanda
                { "kir", "ky" }, // Kirghiz; Kyrgyz
                { "kom", "kv" }, // Komi
                { "kon", "kg" }, // Kongo
                { "kor", "ko" }, // Korean
                { "kua", "kj" }, // Kuanyama; Kwanyama
                { "kur", "ku" }, // Kurdish
                { "lao", "lo" }, // Lao
                { "lat", "la" }, // Latin
                { "lav", "lv" }, // Latvian
                { "lim", "li" }, // Limburgan; Limburger; Limburgish
                { "lin", "ln" }, // Lingala
                { "lit", "lt" }, // Lithuanian
                { "ltz", "lb" }, // Luxembourgish; Letzeburgesch
                { "lub", "lu" }, // Luba-Katanga
                { "lug", "lg" }, // Ganda
                { "mac", "mk" }, // Macedonian
                { "mkd", "mk" }, // Macedonian
                { "mah", "mh" }, // Marshallese
                { "mal", "ml" }, // Malayalam
                { "mao", "mi" }, // Maori
                { "mri", "mi" }, // Maori
                { "mar", "mr" }, // Marathi
                { "may", "ms" }, // Malay
                { "msa", "ms" }, // Malay
                { "mlg", "mg" }, // Malagasy
                { "mlt", "mt" }, // Maltese
                { "mon", "mn" }, // Mongolian
                { "nau", "na" }, // Nauru
                { "nav", "nv" }, // Navajo; Navaho
                { "nbl", "nr" }, // Ndebele, South; South Ndebele
                { "nde", "nd" }, // Ndebele, North; North Ndebele
                { "ndo", "ng" }, // Ndonga
                { "nep", "ne" }, // Nepali
                { "nno", "nn" }, // Norwegian Nynorsk; Nynorsk, Norwegian
                { "nob", "nb" }, // Bokmål, Norwegian; Norwegian Bokmål
                { "nor", "no" }, // Norwegian
                { "nya", "ny" }, // Chichewa; Chewa; Nyanja
                { "oci", "oc" }, // Occitan (post 1500); Provençal
                { "oji", "oj" }, // Ojibwa
                { "ori", "or" }, // Oriya
                { "orm", "om" }, // Oromo
                { "oss", "os" }, // Ossetian; Ossetic
                { "pan", "pa" }, // Panjabi; Punjabi
                { "per", "fa" }, // Persian
                { "fas", "fa" }, // Persian
                { "pli", "pi" }, // Pali
                { "pol", "pl" }, // Polish
                { "por", "pt" }, // Portuguese
                { "pus", "ps" }, // Pushto; Pashto
                { "que", "qu" }, // Quechua
                { "roh", "rm" }, // Romansh
                { "rum", "ro" }, // Romanian; Moldavian; Moldovan
                { "ron", "ro" }, // Romanian; Moldavian; Moldovan
                { "run", "rn" }, // Rundi
                { "rus", "ru" }, // Russian
                { "sag", "sg" }, // Sango
                { "san", "sa" }, // Sanskrit
                { "sin", "si" }, // Sinhala; Sinhalese
                { "slo", "sk" }, // Slovak
                { "slk", "sk" }, // Slovak
                { "slv", "sl" }, // Slovenian
                { "sme", "se" }, // Northern Sami
                { "smo", "sm" }, // Samoan
                { "sna", "sn" }, // Shona
                { "snd", "sd" }, // Sindhi
                { "som", "so" }, // Somali
                { "sot", "st" }, // Sotho, Southern
                { "spa", "es" }, // Spanish; Castilian
                { "srd", "sc" }, // Sardinian
                { "srp", "sr" }, // Serbian
                { "ssw", "ss" }, // Swati
                { "sun", "su" }, // Sundanese
                { "swa", "sw" }, // Swahili
                { "swe", "sv" }, // Swedish
                { "tah", "ty" }, // Tahitian
                { "tam", "ta" }, // Tamil
                { "tat", "tt" }, // Tatar
                { "tel", "te" }, // Telugu
                { "tgk", "tg" }, // Tajik
                { "tgl", "tl" }, // Tagalog
                { "tha", "th" }, // Thai
                { "tib", "bo" }, // Tibetan
                { "bod", "bo" }, // Tibetan
                { "tir", "ti" }, // Tigrinya
                { "ton", "to" }, // Tonga (Tonga Islands)
                { "tsn", "tn" }, // Tswana
                { "tso", "ts" }, // Tsonga
                { "tuk", "tk" }, // Turkmen
                { "tur", "tr" }, // Turkish
                { "twi", "tw" }, // Twi
                { "uig", "ug" }, // Uighur; Uyghur
                { "ukr", "uk" }, // Ukrainian
                { "urd", "ur" }, // Urdu
                { "uzb", "uz" }, // Uzbek
                { "ven", "ve" }, // Venda
                { "vie", "vi" }, // Vietnamese
                { "vol", "vo" }, // Volapük
                { "wel", "cy" }, // Welsh
                { "cym", "cy" }, // Welsh
                { "wln", "wa" }, // Walloon
                { "wol", "wo" }, // Wolof
                { "xho", "xh" }, // Xhosa
                { "yid", "yi" }, // Yiddish
                { "yor", "yo" }, // Yoruba
                { "zha", "za" }, // Zhuang; Chuang
                { "zul", "zu" }, // Zulu
            };

        public static string Normalize(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;

            code = code.Trim();

            if (3 != code.Length)
                return code;

            string code2;

            return !ThreeToTwo.TryGetValue(code, out code2) ? code : code2;
        }
    }
}

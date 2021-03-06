// -----------------------------------------------------------------------
//  <copyright file="PesHandlerFactory.cs" company="Henric Jungheim">
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
using System.Linq;
using SM.Media.Content;
using SM.Media.TransportStream.TsParser;

namespace SM.Media.Pes
{
    public interface IPesHandlerFactory
    {
        PesStreamHandler CreateHandler(PesStreamParameters parameters);
    }

    public sealed class PesHandlerFactory : IPesHandlerFactory
    {
        //    Table 2-34 Stream type assignments
        //    ISO/IEC 13818-1:2007/Amd.3:2009 (E)
        //    Rec. ITU-T H.222.0 (2006)/Amd.3 (03/2009)
        // TODO: TsStreamTypeContentTypes isn't actually used anywhere...
        static readonly IDictionary<byte, ContentType> TsStreamTypeContentTypes =
            new Dictionary<byte, ContentType>
            {
                { TsStreamType.H262StreamType, ContentTypes.H262 },
                { TsStreamType.Mp3Iso11172, ContentTypes.Mp3 },
                { TsStreamType.Mp3Iso13818, ContentTypes.Mp3 },
                { TsStreamType.H264StreamType, ContentTypes.H264 },
                { TsStreamType.AacStreamType, ContentTypes.Aac },
                { TsStreamType.Ac3StreamType, ContentTypes.Ac3 }
            };

        readonly Dictionary<byte, IPesStreamFactoryInstance> _factories;

        public PesHandlerFactory(IEnumerable<IPesStreamFactoryInstance> factoryInstances)
        {
            if (factoryInstances == null)
                throw new ArgumentNullException(nameof(factoryInstances));

            _factories = factoryInstances
                .SelectMany(fi => fi.SupportedStreamTypes,
                    (fi, contentType) => new
                    {
                        ContentType = contentType,
                        Instance = fi
                    })
                .ToDictionary(v => v.ContentType, v => v.Instance);
        }

        #region IPesHandlerFactory Members

        public PesStreamHandler CreateHandler(PesStreamParameters parameters)
        {
            IPesStreamFactoryInstance factory;
            if (!_factories.TryGetValue(parameters.StreamType.StreamType, out factory))
                return new DefaultPesStreamHandler(parameters);

            return factory.Create(parameters);
        }

        #endregion
    }
}

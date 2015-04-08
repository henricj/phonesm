// -----------------------------------------------------------------------
//  <copyright file="H264Configurator.cs" company="Henric Jungheim">
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

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using SM.Media.Configuration;
using SM.Media.Content;
using SM.Media.Utility;

namespace SM.Media.H264
{
    sealed class H264Configurator : VideoConfigurator
    {
        readonly StringBuilder _codecPrivateData = new StringBuilder();
        readonly IH264Reader _h264Reader = new H264Reader();
        IEnumerable<byte> _ppsBytes;
        IEnumerable<byte> _spsBytes;

        public H264Configurator(string streamDescription = null)
            : base("H264", ContentTypes.H264)
        {
            StreamDescription = streamDescription;
        }

        public override string CodecPrivateData
        {
            get
            {
                if (_codecPrivateData.Length > 0)
                    return _codecPrivateData.ToString();

                _codecPrivateData.Append("00000001");

                foreach (var b in RbspEscape(_spsBytes))
                    _codecPrivateData.Append(b.ToString("X2"));

                _codecPrivateData.Append("00000001");

                foreach (var b in RbspEscape(_ppsBytes))
                    _codecPrivateData.Append(b.ToString("X2"));

                return _codecPrivateData.ToString();
            }
        }

        public void ParseSpsBytes(ICollection<byte> value)
        {
            if (value.SequencesAreEquivalent(_spsBytes))
                return;

            _spsBytes = value.ToArray();

            // Get the height/width
            using (var r = new H264Bitstream(_spsBytes))
            {
                _h264Reader.ReadSps(r);
            }

            CheckConfigure();
        }

        public void ParsePpsBytes(ICollection<byte> value)
        {
            if (value.SequencesAreEquivalent(_ppsBytes))
                return;

            _ppsBytes = value.ToArray();

            using (var r = new H264Bitstream(_ppsBytes))
            {
                _h264Reader.ReadPps(r);
            }

            CheckConfigure();
        }

        IEnumerable<byte> RbspEscape(IEnumerable<byte> sequence)
        {
            var zeroCount = 0;

            foreach (var v in sequence)
            {
                var previousZeroCount = zeroCount;

                if (0 == v)
                    ++zeroCount;
                else
                    zeroCount = 0;

                if (0 == (v & ~0x03) && previousZeroCount == 2)
                {
                    zeroCount = 0;

                    yield return 0x03;
                }

                yield return v;
            }
        }

        void CheckConfigure()
        {
            if (IsConfigured)
                return;

            _codecPrivateData.Length = 0;

            if (null == _spsBytes || null == _ppsBytes)
                return;

            if (!_h264Reader.ReaderCheckConfigure(this))
                return;

            Name = _h264Reader.Name;

            Height = _h264Reader.Height;
            Width = _h264Reader.Width;

            FrameRateNumerator = _h264Reader.FrameRateNumerator;
            FrameRateDenominator = _h264Reader.FrameRateDenominator;

            if (FrameRateDenominator.HasValue && FrameRateNumerator.HasValue)
                Debug.WriteLine("H264Configurator.ComputeFrameRate() {0}/{1} -> {2:F4} fps", FrameRateNumerator, FrameRateDenominator, FrameRateNumerator / (double)FrameRateDenominator);


#if DEBUG
            Debug.WriteLine("Configuration " + Name);
#endif

            SetConfigured();
        }

        // ReSharper disable InconsistentNaming
        // ReSharper disable UnusedVariable

        internal void ParseSliceHeader(IList<byte> buffer)
        {
            using (var r = new H264Bitstream(buffer))
            {
                var forbidden_zero_bit = r.ReadBits(1);
                var nal_ref_idc = r.ReadBits(2);
                var nal_unit_type = r.ReadBits(5);

                _h264Reader.ReadSliceHeader(r, NalUnitType.Idr == (NalUnitType)nal_unit_type);
            }

            _h264Reader.TryReparseTimingSei(this);

            CheckConfigure();
        }

        internal void ParseSei(ICollection<byte> buffer)
        {
            using (var r = new H264Bitstream(buffer))
            {
                var forbidden_zero_bit = r.ReadBits(1);
                var nal_ref_idc = r.ReadBits(2);
                var nal_unit_type = r.ReadBits(5);

                _h264Reader.ReadSei(r, buffer);
            }

            CheckConfigure();
        }

        internal void ParseAud(IList<byte> buffer)
        {
            using (var r = new H264Bitstream(buffer))
            {
                var forbidden_zero_bit = r.ReadBits(1);
                var nal_ref_idc = r.ReadBits(2);
                var nal_unit_type = r.ReadBits(5);

                var primary_pic_type = r.ReadBits(3);
            }
        }

        // ReSharper restore UnusedVariable
        // ReSharper restore InconsistentNaming
    }
}

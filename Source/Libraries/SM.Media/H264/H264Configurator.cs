// -----------------------------------------------------------------------
//  <copyright file="H264Configurator.cs" company="Henric Jungheim">
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
using System.Linq;
using System.Text;
using SM.Media.Configuration;

namespace SM.Media.H264
{
    sealed class H264Configurator : ConfiguratorBase, IH264Configuration
    {
        static readonly uint[] ProfileIdcHasChromaFormat
            = new[]
              {
                  100u,
                  110u,
                  122u,
                  244u,
                  44u,
                  83u,
                  86u,
                  118u,
                  128u
              }.OrderBy(k => k)
               .ToArray();

        readonly StringBuilder _codecPrivateData = new StringBuilder();
        IEnumerable<byte> _ppsBytes;
        IEnumerable<byte> _spsBytes;

        public H264Configurator(string streamDescription = null)
        {
            StreamDescription = streamDescription;
        }

        #region IH264Configuration Members

        public IEnumerable<byte> SpsBytes
        {
            get { return _spsBytes; }
            set
            {
                if (SequencesAreEquivalent(value, _spsBytes))
                    return;

                _spsBytes = value.ToArray();

                // Get the height/width
                ParseSps(_spsBytes);

                CheckConfigure();
            }
        }

        public IEnumerable<byte> PpsBytes
        {
            get { return _ppsBytes; }
            set
            {
                if (SequencesAreEquivalent(value, _ppsBytes))
                    return;

                _ppsBytes = value.ToArray();

                CheckConfigure();
            }
        }

        public string VideoFourCc
        {
            get { return "H264"; }
        }

        public int? Height { get; private set; }
        public int? Width { get; private set; }

        public override string CodecPrivateData
        {
            get
            {
                if (_codecPrivateData.Length > 0)
                    return _codecPrivateData.ToString();

                _codecPrivateData.Append("00000001");

                foreach (var b in RbspEscape(SpsBytes))
                    _codecPrivateData.Append(b.ToString("X2"));

                _codecPrivateData.Append("00000001");

                foreach (var b in RbspEscape(PpsBytes))
                    _codecPrivateData.Append(b.ToString("X2"));

                return _codecPrivateData.ToString();
            }
        }

        #endregion

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

        bool SequencesAreEquivalent(IEnumerable<byte> a, IEnumerable<byte> b)
        {
            if (ReferenceEquals(a, b))
                return true;

            if (null == a || null == b)
                return false;

            return a.SequenceEqual(b);
        }

        void CheckConfigure()
        {
            _codecPrivateData.Length = 0;

            if (null == _spsBytes || null == _ppsBytes)
                return;

            SetConfigured();
        }

        void ParseScalingList(H264Bitstream r, int sizeOfScalingList)
        {
            var lastScale = 8;
            var nextScale = 8;

            for (var j = 0; j < sizeOfScalingList; ++j)
            {
                if (0 != nextScale)
                {
                    var delta_scale = r.ReadSe();

                    nextScale = (lastScale + delta_scale + 256) & 0xff;
                    var useDefaultScalingMatrixFlag = (0 == j && 0 == nextScale);
                }

                var scalingList = 0 == nextScale ? lastScale : nextScale;
                lastScale = scalingList;
            }
        }

        void ParseSps(IEnumerable<byte> buffer)
        {
            uint profile_idc;
            uint constraint_sets;
            uint level_idc;
            uint width;
            uint height;

            using (var r = new H264Bitstream(buffer))
            {
                var forbidden_zero_bit = r.ReadBits(1);
                var nal_ref_idc = r.ReadBits(2);
                var nal_unit_type = r.ReadBits(5);

                profile_idc = r.ReadBits(8);
                constraint_sets = r.ReadBits(8);
                level_idc = r.ReadBits(8);
                var seq_parameter_set_id = r.ReadUe();

                if (Array.BinarySearch(ProfileIdcHasChromaFormat, profile_idc) >= 0)
                {
                    var chroma_format_idc = r.ReadUe();

                    if (3 == chroma_format_idc)
                    {
                        var separate_colour_plane_flag = r.ReadBits(1);
                    }

                    var bit_depth_luma_minus8 = r.ReadUe();

                    var bit_depth_chroma_minus8 = r.ReadUe();

                    var qpprime_y_zero_transform_bypass_flag = r.ReadBits(1);

                    var seq_scaling_matrix_present_flag = r.ReadBits(1);

                    if (0 != seq_scaling_matrix_present_flag)
                    {
                        for (var i = 0; i < (3 != chroma_format_idc ? 8 : 12); ++i)
                        {
                            var seq_caling_list_present_flag = r.ReadBits(1);

                            if (0 != seq_scaling_matrix_present_flag)
                            {
                                if (i < 6)
                                    ParseScalingList(r, 16);
                                else
                                    ParseScalingList(r, 64);
                            }
                        }
                    }
                }

                var log2_max_frame_num_minus4 = r.ReadUe();
                var pic_order_cnt_type = r.ReadUe();

                if (0 == pic_order_cnt_type)
                {
                    var log2_max_pic_order_cnt_lsb_minus4 = r.ReadUe();
                }
                else if (1 == pic_order_cnt_type)
                {
                    var delta_pic_order_always_zero_flag = r.ReadBits(1);
                    var offset_for_non_ref_pic = r.ReadSe();
                    var offset_for_top_to_bottom_field = r.ReadSe();
                    var num_ref_frames_in_pic_order_cnt_cycle = r.ReadUe();

                    for (var i = 0; i < num_ref_frames_in_pic_order_cnt_cycle; ++i)
                    {
                        var offset_for_ref_frame = r.ReadSe();
                    }
                }

                var max_num_ref_frames = r.ReadUe();
                var gaps_in_frame_num_value_allowed_flag = r.ReadBits(1);
                var pic_width_in_mbs_minus1 = r.ReadUe();
                var pic_height_in_map_units_minus1 = r.ReadUe();
                var frame_mbs_only_flag = r.ReadBits(1);

                if (0 == frame_mbs_only_flag)
                {
                    var mb_adaptive_frame_field_flag = r.ReadBits(1);
                }

                var direct_8x8_inference_flag = r.ReadBits(1);
                var frame_cropping_flag = r.ReadBits(1);

                width = ((pic_width_in_mbs_minus1 + 1) * 16);
                height = ((2 - frame_mbs_only_flag) * (pic_height_in_map_units_minus1 + 1) * 16);

                if (0 != frame_cropping_flag)
                {
                    var frame_crop_left_offset = r.ReadUe();
                    var frame_crop_right_offset = r.ReadUe();
                    var frame_crop_top_offset = r.ReadUe();
                    var frame_crop_bottom_offset = r.ReadUe();

                    width = width - frame_crop_left_offset * 2 - frame_crop_right_offset * 2;
                    height = height - (frame_crop_top_offset * 2) - (frame_crop_bottom_offset * 2);
                }
            }

            Height = (int)height;
            Width = (int)width;

            var profileName = ProfileName(profile_idc, constraint_sets);

            Name = string.Format("H.264 \"{0}\" profile, level {1} {2}x{3}",
                profileName, level_idc / 10.0, width, height);
#if DEBUG
            Debug.WriteLine("Configuration " + Name);
#endif
        }

        string ProfileName(uint profile_idc, uint constraint_sets)
        {
            var constraint_set1 = 0 != (constraint_sets & (1 << 1));
            var constraint_set3 = 0 != (constraint_sets & (1 << 3));
            var constraint_set4 = 0 != (constraint_sets & (1 << 4));
            var constraint_set5 = 0 != (constraint_sets & (1 << 5));

            switch (profile_idc)
            {
                case 44:
                    return "CAVLC 4:4:4 Intra";
                case 66:
                    return constraint_set1 ? "Constrained Baseline" : "Baseline";
                case 88:
                    return "Extended";
                case 77:
                    return "Main";
                case 244:
                    return constraint_set3 ? "High 4:4:4 Intra" : "High 4:4:4 Predictive";
                case 122:
                    return constraint_set3 ? "High 4:2:2 Intra" : "High 4:2:2";
                case 110:
                    return constraint_set3 ? "High 10 Intra" : "High 10";
                case 100:
                    if (constraint_set4 && constraint_set5)
                        return "Constrained High";

                    if (constraint_set4)
                        return "Progressive High";

                    return "High";
            }

            if (constraint_set1)
                return "Constrained Baseline";

            var sb = new StringBuilder();

            sb.AppendFormat("P{0}-CS[", profile_idc);

            for (var i = 0; i < 8; ++i)
            {
                if (0 != (constraint_sets & (1 << i)))
                    sb.Append(i);
                else
                    sb.Append('.');
            }

            sb.Append(']');

            return sb.ToString();
        }
    }
}

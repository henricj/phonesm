// -----------------------------------------------------------------------
//  <copyright file="H264Reader.cs" company="Henric Jungheim">
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
using System.Linq;
using System.Text;
using SM.Media.Utility;

namespace SM.Media.H264
{
    interface IH264Reader
    {
        int? FrameRateDenominator { get; }
        int? FrameRateNumerator { get; }
        string Name { get; }
        int? Width { get; }
        int? Height { get; }
        // ReSharper disable once InconsistentNaming
        void ReadSliceHeader(H264Bitstream r, bool IdrPicFlag);
        void ReadSei(H264Bitstream r, ICollection<byte> buffer);
        bool ReaderCheckConfigure(H264Configurator h264Configurator);
        void ReadSps(H264Bitstream r);
        void ReadPps(H264Bitstream r);
        void TryReparseTimingSei(H264Configurator h264Configurator);
    }

    sealed class H264Reader : IH264Reader
    {
        static readonly byte[] NumClockTsLookup = { 1, 1, 1, 2, 2, 3, 3, 2, 3 };

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

        static readonly byte[] DivisorFromPicStruct = { 2, 1, 1, 2, 2, 3, 3, 4, 6 };

        uint? _bottomFieldPicOrderInFramePresentFlag;
        uint? _chromaFormatIdc;
        uint? _cpbRemovalDelayLengthMinus1;
        uint? _deltaPicOrderAlwaysZeroFlag;
        uint? _dpbOutputDelayLengthMinus1;
        bool? _fieldPicFlag;
        bool? _fixedFrameRateFlag;
        uint? _frameMbsOnlyFlag;
        uint? _log2MaxFrameNumMinus4;
        bool? _nalHrdParametersPresentFlag;
        uint? _numUnitsInTick;
        uint? _picOrderCntType;
        uint? _picParameterSetId;
        uint? _picStruct;
        bool? _picStructPresentFlag;
        uint? _ppsSeqParameterSetId;
        uint? _redundantPicCntPresentFlag;
        uint? _separateColourPlaneFlag;
        uint? _seqParameterSetId;
        uint? _slicePicParameterSetId;
        uint? _timeOffsetLength;
        uint? _timeScale;
        ICollection<byte> _timingBytes;
        bool? _vclHrdParametersPresentFlag;

        #region IH264Reader Members

        public int? FrameRateDenominator { get; private set; }
        public int? FrameRateNumerator { get; private set; }

        public string Name { get; private set; }

        public int? Width { get; private set; }
        public int? Height { get; private set; }

        public void ReadSliceHeader(H264Bitstream r, bool IdrPicFlag)
        {
            var first_mb_in_slice = r.ReadUe();
            var slice_type = r.ReadUe();
            var pic_parameter_set_id = r.ReadUe();

            _slicePicParameterSetId = pic_parameter_set_id;

            if (_separateColourPlaneFlag == 1)
            {
                var colour_plane_id = r.ReadBits(2);
            }

            var frame_num = r.ReadBits((int)(_log2MaxFrameNumMinus4 ?? 0) + 4);

            uint field_pic_flag = 0;

            if (0 == _frameMbsOnlyFlag)
            {
                field_pic_flag = r.ReadBits(1);
                if (0 != field_pic_flag)
                {
                    var bottom_field_flag = r.ReadBits(1);
                }
            }

            _fieldPicFlag = 0 != field_pic_flag;

            if (IdrPicFlag)
            {
                var idr_pic_id = r.ReadUe();

                if (_picOrderCntType == 0)
                {
                    var pic_order_cnt_lsb = r.ReadBits((int)(_log2MaxFrameNumMinus4 ?? 0) + 4);

                    if (0 != _bottomFieldPicOrderInFramePresentFlag && 0 == field_pic_flag)
                    {
                        var delta_pic_order_cnt_bottom = r.ReadSe();
                    }
                }
                if (_picOrderCntType == 1 && 0 == _deltaPicOrderAlwaysZeroFlag)
                {
                    var delta_pic_order_cnt_0 = r.ReadSe();

                    if (0 != _bottomFieldPicOrderInFramePresentFlag && 0 == field_pic_flag)
                    {
                        var delta_pic_order_cnt_1 = r.ReadSe();
                    }
                }

                if (0 != _redundantPicCntPresentFlag)
                {
                    var redundant_pic_cnt = r.ReadUe();
                }
            }
        }

        public void ReadSei(H264Bitstream r, ICollection<byte> buffer)
        {
            var payloadType = r.ReadFfSum();
            var payloadLength = r.ReadFfSum();

            // Rec. ITU-T H.264 (04/2013) Section D.1
            switch (payloadType)
            {
                case 1: // pic_timing
                    if (_slicePicParameterSetId.HasValue)
                        ReadPicTiming(r, payloadLength);
                    else if (!buffer.SequencesAreEquivalent(_timingBytes))
                        _timingBytes = buffer.ToArray();

                    break;
#if false
                case 5: // user_data_unregistered
                    ReadUserDataUnregistered(r, payloadLength);
                    break;
#endif
            }
        }

        public bool ReaderCheckConfigure(H264Configurator h264Configurator)
        {
            if (!_slicePicParameterSetId.HasValue || !_ppsSeqParameterSetId.HasValue || !_seqParameterSetId.HasValue)
                return false;

            if (_slicePicParameterSetId == _picParameterSetId && _ppsSeqParameterSetId == _seqParameterSetId)
            {
                if (!ComputeFrameRate())
                    return false;

                return true;
            }

            return false;
        }

        public void ReadSps(H264Bitstream r)
        {
            _ppsSeqParameterSetId = null;

            uint profile_idc;
            uint constraint_sets;
            uint level_idc;
            uint width;
            uint height;

            var forbidden_zero_bit = r.ReadBits(1);
            var nal_ref_idc = r.ReadBits(2);
            var nal_unit_type = r.ReadBits(5);

            profile_idc = r.ReadBits(8);
            constraint_sets = r.ReadBits(8);
            level_idc = r.ReadBits(8);
            var seq_parameter_set_id = r.ReadUe();

            _seqParameterSetId = seq_parameter_set_id;

            if (Array.BinarySearch(ProfileIdcHasChromaFormat, profile_idc) >= 0)
            {
                var chroma_format_idc = r.ReadUe();

                _chromaFormatIdc = chroma_format_idc;

                if (3 == chroma_format_idc)
                {
                    var separate_colour_plane_flag = r.ReadBits(1);

                    _separateColourPlaneFlag = separate_colour_plane_flag;
                }

                var bit_depth_luma_minus8 = r.ReadUe();

                var bit_depth_chroma_minus8 = r.ReadUe();

                var qpprime_y_zero_transform_bypass_flag = r.ReadBits(1);

                var seq_scaling_matrix_present_flag = r.ReadBits(1);

                if (0 != seq_scaling_matrix_present_flag)
                {
                    for (var i = 0; i < (3 != chroma_format_idc ? 8 : 12); ++i)
                    {
                        var seq_scaling_list_present_flag = r.ReadBits(1);

                        if (0 != seq_scaling_list_present_flag)
                            ParseScalingList(r, i < 6 ? 16 : 64);
                    }
                }
            }

            var log2_max_frame_num_minus4 = r.ReadUe();

            _log2MaxFrameNumMinus4 = log2_max_frame_num_minus4;

            var pic_order_cnt_type = r.ReadUe();

            _picOrderCntType = pic_order_cnt_type;

            if (0 == pic_order_cnt_type)
            {
                var log2_max_pic_order_cnt_lsb_minus4 = r.ReadUe();
            }
            else if (1 == pic_order_cnt_type)
            {
                var delta_pic_order_always_zero_flag = r.ReadBits(1);

                _deltaPicOrderAlwaysZeroFlag = delta_pic_order_always_zero_flag;

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

            _frameMbsOnlyFlag = frame_mbs_only_flag;

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

            var vui_parameters_present_flag = r.ReadBits(1);

            if (0 != vui_parameters_present_flag)
            {
                ReadVuiParameters(r);
            }

            Height = (int)height;
            Width = (int)width;

            var profileName = ProfileName(profile_idc, constraint_sets);

            Name = String.Format("H.264 \"{0}\" profile, level {1} {2}x{3}",
                profileName, level_idc / 10.0, width, height);
        }

        public void ReadPps(H264Bitstream r)
        {
            _slicePicParameterSetId = null;

            var forbidden_zero_bit = r.ReadBits(1);
            var nal_ref_idc = r.ReadBits(2);
            var nal_unit_type = r.ReadBits(5);

            var pic_parameter_set_id = r.ReadUe();

            _picParameterSetId = pic_parameter_set_id;

            var seq_parameter_set_id = r.ReadUe();

            _ppsSeqParameterSetId = seq_parameter_set_id;

            var entropy_coding_mode_flag = r.ReadBits(1);
            var bottom_field_pic_order_in_frame_present_flag = r.ReadBits(1);

            _bottomFieldPicOrderInFramePresentFlag = bottom_field_pic_order_in_frame_present_flag;

            var num_slice_groups_minus1 = r.ReadUe();

            if (num_slice_groups_minus1 > 0)
            {
                var slice_group_map_type = r.ReadUe();
                if (0 == slice_group_map_type)
                {
                    for (var iGroup = 0; iGroup <= num_slice_groups_minus1; iGroup++)
                    {
                        var run_length_minus1_iGroup = r.ReadUe();
                    }
                }
                else if (2 == slice_group_map_type)
                {
                    for (var iGroup = 0; iGroup < num_slice_groups_minus1; iGroup++)
                    {
                        var top_left_iGroup = r.ReadUe();
                        var bottom_right_iGroup = r.ReadUe();
                    }
                }
                else if (3 == slice_group_map_type || 4 == slice_group_map_type || 5 == slice_group_map_type)
                {
                    var slice_group_change_direction_flag = r.ReadBits(1);
                    var slice_group_change_rate_minus1 = r.ReadUe();
                }
                else if (6 == slice_group_map_type)
                {
                    var nsgBits = GetBitSize(num_slice_groups_minus1);

                    var pic_size_in_map_units_minus1 = r.ReadUe();
                    for (var i = 0; i <= pic_size_in_map_units_minus1; i++)
                    {
                        var slice_group_id_i = r.ReadBits(nsgBits);
                    }
                }
            }

            var num_ref_idx_l0_default_active_minus1 = r.ReadUe();
            var num_ref_idx_l1_default_active_minus1 = r.ReadUe();
            var weighted_pred_flag = r.ReadBits(1);
            var weighted_bipred_idc = r.ReadBits(2);
            var pic_init_qp_minus26 /* relative to 26 */ = r.ReadSe();
            var pic_init_qs_minus26 /* relative to 26 */ = r.ReadSe();
            var chroma_qp_index_offset = r.ReadSe();
            var deblocking_filter_control_present_flag = r.ReadBits(1);
            var constrained_intra_pred_flag = r.ReadBits(1);
            var redundant_pic_cnt_present_flag = r.ReadBits(1);

            _redundantPicCntPresentFlag = redundant_pic_cnt_present_flag;

            if (more_rbsp_data(r))
            {
                var transform_8x8_mode_flag = r.ReadBits(1);
                var pic_scaling_matrix_present_flag = r.ReadBits(1);
                if (0 != pic_scaling_matrix_present_flag)
                {
                    for (var i = 0; i < 6 + ((_chromaFormatIdc != 3) ? 2 : 6) * transform_8x8_mode_flag; i++)
                    {
                        var pic_scaling_list_present_flag_i = r.ReadBits(1);
                        if (0 != pic_scaling_list_present_flag_i)
                        {
                            if (i < 6)
                            {
                                ReadScalingList(r, 16);
                                //scaling_list(ScalingList4x4[i], 16, UseDefaultScalingMatrix4x4Flag[i]);
                            }
                            else
                            {
                                ReadScalingList(r, 64);
                                //scaling_list(ScalingList8x8[i - 6], 64, UseDefaultScalingMatrix8x8Flag[i - 6]);
                            }
                        }
                    }
                }

                var second_chroma_qp_index_offset = r.ReadSe();
            }
        }

        public void TryReparseTimingSei(H264Configurator h264Configurator)
        {
            if (null != _timingBytes && _seqParameterSetId.HasValue)
                h264Configurator.ParseSei(_timingBytes);
        }

        #endregion

        // ReSharper disable InconsistentNaming
        // ReSharper disable UnusedVariable

        void ReadVuiParameters(H264Bitstream r)
        {
            var aspect_ratio_info_present_flag = r.ReadBits(1);

            if (0 != aspect_ratio_info_present_flag)
            {
                var aspect_ratio_idc = r.ReadBits(8);

                const int Extended_SAR = 255;

                if (Extended_SAR == aspect_ratio_idc)
                {
                    var sar_width = r.ReadBits(16);
                    var sar_height = r.ReadBits(16);
                }
            }

            var overscan_info_present_flag = r.ReadBits(1);

            if (0 != overscan_info_present_flag)
            {
                var overscan_appropriate_flag = r.ReadBits(1);
            }

            var video_signal_type_present_flag = r.ReadBits(1);
            if (0 != video_signal_type_present_flag)
            {
                var video_format = r.ReadBits(3);
                var video_full_range_flag = r.ReadBits(1);
                var colour_description_present_flag = r.ReadBits(1);
                if (0 != colour_description_present_flag)
                {
                    var colour_primaries = r.ReadBits(8);
                    var transfer_characteristics = r.ReadBits(8);
                    var matrix_coefficients = r.ReadBits(8);
                }
            }

            var chroma_loc_info_present_flag = r.ReadBits(1);
            if (0 != chroma_loc_info_present_flag)
            {
                var chroma_sample_loc_type_top_field = r.ReadUe();
                var chroma_sample_loc_type_bottom_field = r.ReadUe();
            }

            var timing_info_present_flag = r.ReadBits(1);
            if (0 != timing_info_present_flag)
            {
                var num_units_in_tick = r.ReadBits(32);
                _numUnitsInTick = num_units_in_tick;

                var time_scale = r.ReadBits(32);
                _timeScale = time_scale;

                var fixed_frame_rate_flag = r.ReadBits(1);
                _fixedFrameRateFlag = 0 != fixed_frame_rate_flag;
            }

            var nal_hrd_parameters_present_flag = r.ReadBits(1);

            _nalHrdParametersPresentFlag = 0 != nal_hrd_parameters_present_flag;

            if (0 != nal_hrd_parameters_present_flag)
                ReadHrdParameters(r);

            var vcl_hrd_parameters_present_flag = r.ReadBits(1);

            _vclHrdParametersPresentFlag = 0 != vcl_hrd_parameters_present_flag;

            if (0 != vcl_hrd_parameters_present_flag)
                ReadHrdParameters(r);

            if (0 != nal_hrd_parameters_present_flag || 0 != vcl_hrd_parameters_present_flag)
            {
                var low_delay_hrd_flag = r.ReadBits(1);
            }

            var pic_struct_present_flag = r.ReadBits(1);

            _picStructPresentFlag = 0 != pic_struct_present_flag;

            var bitstream_restriction_flag = r.ReadBits(1);
            if (0 != bitstream_restriction_flag)
            {
                var motion_vectors_over_pic_boundaries_flag = r.ReadBits(1);
                var max_bytes_per_pic_denom = r.ReadUe();
                var max_bits_per_mb_denom = r.ReadUe();
                var log2_max_mv_length_horizontal = r.ReadUe();
                var log2_max_mv_length_vertical = r.ReadUe();
                var max_num_reorder_frames = r.ReadUe();
                var max_dec_frame_buffering = r.ReadUe();
            }
        }

        void ReadHrdParameters(H264Bitstream r)
        {
            var cpb_cnt_minus1 = r.ReadUe();
            var bit_rate_scale = r.ReadBits(4);
            var cpb_size_scale = r.ReadBits(4);
            for (var SchedSelIdx = 0; SchedSelIdx <= cpb_cnt_minus1; SchedSelIdx++)
            {
                var bit_rate_value_minus1_SchedSelIdx = r.ReadUe();
                var cpb_size_value_minus1_SchedSelIdx = r.ReadUe();
                var cbr_flag_SchedSelIdx = r.ReadBits(1);
            }
            var initial_cpb_removal_delay_length_minus1 = r.ReadBits(5);
            var cpb_removal_delay_length_minus1 = r.ReadBits(5);

            _cpbRemovalDelayLengthMinus1 = cpb_removal_delay_length_minus1;

            var dpb_output_delay_length_minus1 = r.ReadBits(5);

            _dpbOutputDelayLengthMinus1 = dpb_output_delay_length_minus1;

            var time_offset_length = r.ReadBits(5);

            _timeOffsetLength = time_offset_length;
        }

        void ReadScalingList(H264Bitstream r, int sizeOfScalingList)
        {
            var lastScale = 8;
            var nextScale = 8;
            for (var j = 0; j < sizeOfScalingList; j++)
            {
                if (nextScale != 0)
                {
                    var delta_scale = r.ReadSe();
                    nextScale = (lastScale + delta_scale + 256) % 256;
                    var useDefaultScalingMatrixFlag = (j == 0 && nextScale == 0);
                }
                var scalingList_j = (nextScale == 0) ? lastScale : nextScale;
                lastScale = scalingList_j;
            }
        }

        void ReadUserDataUnregistered(H264Bitstream r, uint length)
        {
            var buffer = new byte[16];

            for (var i = 0; i < buffer.Length; ++i)
                buffer[i] = (byte)r.ReadBits(8);

            var uuid = new Guid(buffer); // Byte order...?
        }

        int? GetNumClockTs()
        {
            if (!_picStruct.HasValue)
                return null;

            var pic_struct = _picStruct.Value;

            if (pic_struct >= NumClockTsLookup.Length)
                return null;

            return NumClockTsLookup[pic_struct];
        }

        void ReadPicTiming(H264Bitstream r, uint length)
        {
            // Rec. ITU-T H.264 (04/2013) Section D.2.2

            var CpbDpbDelaysPresentFlag = (_nalHrdParametersPresentFlag.HasValue && _nalHrdParametersPresentFlag.Value)
                                          || (_vclHrdParametersPresentFlag.HasValue && _vclHrdParametersPresentFlag.Value);

            if (CpbDpbDelaysPresentFlag)
            {
                if (!_cpbRemovalDelayLengthMinus1.HasValue)
                    return;

                var cpb_removal_delay = r.ReadBits((int)_cpbRemovalDelayLengthMinus1.Value + 1);
                if (!_dpbOutputDelayLengthMinus1.HasValue)
                    return;

                var dpb_output_delay = r.ReadBits((int)_dpbOutputDelayLengthMinus1.Value + 1);
            }
            if (_picStructPresentFlag.HasValue && _picStructPresentFlag.Value)
            {
                var pic_struct = r.ReadBits(4);

                _picStruct = pic_struct;

                var nct = GetNumClockTs();

                if (!nct.HasValue)
                    return;

                var NumClockTS = nct.Value;

                for (var i = 0; i < NumClockTS; i++)
                {
                    var clock_timestamp_flag_i = r.ReadBits(1);
                    if (0 != clock_timestamp_flag_i)
                    {
                        var ct_type = r.ReadBits(2);
                        var nuit_field_based_flag = r.ReadBits(1);
                        var counting_type = r.ReadBits(5);
                        var full_timestamp_flag = r.ReadBits(1);
                        var discontinuity_flag = r.ReadBits(1);
                        var cnt_dropped_flag = r.ReadBits(1);
                        var n_frames = r.ReadBits(8);
                        if (0 != full_timestamp_flag)
                        {
                            var seconds_value /* 0..59 */ = r.ReadBits(6);
                            var minutes_value /* 0..59 */ = r.ReadBits(6);
                            var hours_value /* 0..23 */ = r.ReadBits(5);
                        }
                        else
                        {
                            var seconds_flag = r.ReadBits(1);
                            if (0 != seconds_flag)
                            {
                                var seconds_value /* range 0..59 */ = r.ReadBits(6);
                                var minutes_flag = r.ReadBits(1);
                                if (0 != minutes_flag)
                                {
                                    var minutes_value /* 0..59 */ = r.ReadBits(6);
                                    var hours_flag = r.ReadBits(1);
                                    if (0 != hours_flag)
                                    {
                                        var hours_value /* 0..23 */ = r.ReadBits(5);
                                    }
                                }
                            }
                        }

                        var time_offset_length = _timeOffsetLength ?? 24;

                        if (time_offset_length > 0)
                        {
                            var time_offset = r.ReadSignedBits((int)time_offset_length);
                        }
                    }
                }
            }
        }

        bool ComputeFrameRate()
        {
            if (!_numUnitsInTick.HasValue || !_timeScale.HasValue)
                return true;

            var denominator = _numUnitsInTick.Value;
            var numerator = _timeScale.Value;

            uint divisor;

            if (!_picStructPresentFlag.HasValue)
                return true;

            if (_picStructPresentFlag.Value)
            {
                if (!_picStruct.HasValue)
                {
                    return false;
                }

                var pic_struct = _picStruct.Value;

                if (pic_struct >= DivisorFromPicStruct.Length)
                    return true;

                divisor = DivisorFromPicStruct[_picStruct.Value];
            }
            else
            {
                if (!_fieldPicFlag.HasValue)
                    return true;

                divisor = _fieldPicFlag.Value ? 1u : 2u;
            }

            if (1 != divisor)
            {
                if (0 == (numerator % divisor))
                    numerator /= divisor;
                else
                    denominator *= divisor;
            }

            FrameRateDenominator = (int)denominator;
            FrameRateNumerator = (int)numerator;

            return true;
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

        static string ProfileName(uint profile_idc, uint constraint_sets)
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

        // ReSharper restore UnusedVariable
        // ReSharper restore InconsistentNaming

        static int GetBitSize(uint value)
        {
            if (1 < value)
                return 0;

            var log2 = 1;
            var n = 1u;

            while (n < value)
            {
                ++log2;
                n = (n << 1) | 1;
            }

            return log2;
        }

        bool more_rbsp_data(H264Bitstream r)
        {
            return r.HasData;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NWebp.Internal
{
	public partial class Internal
	{
		// version numbers
		const int DEC_MAJ_VERSION = 0;
		const int DEC_MIN_VERSION = 1;
		const int DEC_REV_VERSION = 3;
	}

	// @TODO: Move to Internal
	enum _NUMEnum
	{
		MB_FEATURE_TREE_PROBS = 3,
		NUM_MB_SEGMENTS = 4,
		NUM_REF_LF_DELTAS = 4,
		NUM_MODE_LF_DELTAS = 4,    // I4x4, ZERO, *, SPLIT
		MAX_NUM_PARTITIONS = 8,
		// Probabilities
		NUM_TYPES = 4,
		NUM_BANDS = 8,
		NUM_CTX = 3,
		NUM_PROBAS = 11,
		NUM_MV_PROBAS = 19
	};

	//------------------------------------------------------------------------------
	// Various defines and enums

	//bool ONLY_KEYFRAME_CODE = true;

	/// <summary>
	/// intra prediction modes
	/// </summary>
	enum IntraPredictionModes
	{
		B_DC_PRED = 0,   // 4x4 modes
		B_TM_PRED,
		B_VE_PRED,
		B_HE_PRED,
		B_RD_PRED,
		B_VR_PRED,
		B_LD_PRED,
		B_VL_PRED,
		B_HD_PRED,
		B_HU_PRED,
		NUM_BMODES = B_HU_PRED + 1 - B_DC_PRED,  // = 10

		// Luma16 or UV modes
		DC_PRED = B_DC_PRED, V_PRED = B_VE_PRED,
		H_PRED = B_HE_PRED, TM_PRED = B_TM_PRED,
		B_PRED = NUM_BMODES,   // refined I4x4 mode

		// special modes
		B_DC_PRED_NOTOP = 4,
		B_DC_PRED_NOLEFT = 5,
		B_DC_PRED_NOTOPLEFT = 6,
		NUM_B_DC_MODES = 7
	};

	public partial class Internal
	{
		// YUV-cache parameters.
		// Constraints are: We need to store one 16x16 block of luma samples (y),
		// and two 8x8 chroma blocks (u/v). These are better be 16-bytes aligned,
		// in order to be SIMD-friendly. We also need to store the top, left and
		// top-left samples (from previously decoded blocks), along with four
		// extra top-right samples for luma (intra4x4 prediction only).
		// One possible layout is, using 32 * (17 + 9) bytes:
		//
		//   .+------   <- only 1 pixel high
		//   .|yyyyt.
		//   .|yyyyt.
		//   .|yyyyt.
		//   .|yyyy..
		//   .+--.+--   <- only 1 pixel high
		//   .|uu.|vv
		//   .|uu.|vv
		//
		// Every character is a 4x4 block, with legend:
		//  '.' = unused
		//  'y' = y-samples   'u' = u-samples     'v' = u-samples
		//  '|' = left sample,   '-' = top sample,    '+' = top-left sample
		//  't' = extra top-right sample for 4x4 modes
		// With this layout, BPS (=Bytes Per Scan-line) is one cacheline size.
		const int BPS = 32;    // this is the common stride used by yuv[]
		const int YUV_SIZE = (BPS * 17 + BPS * 9);
		const int Y_SIZE = (BPS * 17);
		const int Y_OFF = (BPS * 1 + 8);
		const int U_OFF = (Y_OFF + BPS * 16 + BPS);
		const int V_OFF = (U_OFF + 16);
	}

	//------------------------------------------------------------------------------
	// Headers

	public struct VP8FrameHeader
	{
		public byte key_frame_;
		public byte profile_;
		public byte show_;
		public uint partition_length_;
	}

	public struct VP8PictureHeader
	{
		public ushort width_;
		public ushort height_;
		public byte xscale_;
		public byte yscale_;
		public byte colorspace_;   // 0 = YCbCr
		public byte clamp_type_;
	}

	/// <summary>
	/// segment features
	/// </summary>
	public unsafe struct VP8SegmentHeader
	{
		/// <summary>
		/// 
		/// </summary>
		public int use_segment_;
		
		/// <summary>
		/// whether to update the segment map or not
		/// </summary>
		public int update_map_;

		/// <summary>
		/// absolute or delta values for quantizer and filter
		/// </summary>
		public int absolute_delta_;   

		/// <summary>
		/// quantization changes
		/// </summary>
		public fixed sbyte quantizer_[(int)_NUMEnum.NUM_MB_SEGMENTS];      

		/// <summary>
		/// filter strength for segments
		/// </summary>
		public fixed sbyte filter_strength_[(int)_NUMEnum.NUM_MB_SEGMENTS];
	}

	/// <summary>
	/// Struct collecting all frame-persistent probabilities.
	/// </summary>
	unsafe public partial class VP8Proba
	{
		byte[] segments_ = new byte[(int)_NUMEnum.MB_FEATURE_TREE_PROBS];
		// Type: 0:Intra16-AC  1:Intra16-DC   2:Chroma   3:Intra4
		byte[, , ,] coeffs_ = new byte[(int)_NUMEnum.NUM_TYPES, (int)_NUMEnum.NUM_BANDS, (int)_NUMEnum.NUM_CTX, (int)_NUMEnum.NUM_PROBAS];
#if !ONLY_KEYFRAME_CODE
		byte ymode_[4], uvmode_[3];
		byte mv_[2][NUM_MV_PROBAS];
#endif
	}

	/// <summary>
	/// Filter parameters
	/// </summary>
	struct VP8FilterHeader
	{
		int simple_;                  // 0=complex, 1=simple
		int level_;                   // [0..63]
		int sharpness_;               // [0..7]
		int use_lf_delta_;
		fixed int ref_lf_delta_[(int)_NUMEnum.NUM_REF_LF_DELTAS];
		fixed int mode_lf_delta_[(int)_NUMEnum.NUM_MODE_LF_DELTAS];
	}

	/// <summary>
	/// filter specs
	/// Informations about the macroblocks.
	/// </summary>
	struct VP8FInfo
	{
		/*
		uint f_level_:6;      // filter strength: 0..63
		uint f_ilevel_:6;     // inner limit: 1..63
		uint f_inner_:1;      // do inner filtering?
		*/
		uint f_level_ { get { throw (new NotImplementedException()); } set { throw (new NotImplementedException()); } }
		uint f_ilevel_ { get { throw (new NotImplementedException()); } set { throw (new NotImplementedException()); } }
		uint f_inner_ { get { throw (new NotImplementedException()); } set { throw (new NotImplementedException()); } }
	}

	/// <summary>
	/// used for syntax-parsing
	/// Informations about the macroblocks.
	/// </summary>
	struct VP8MB
	{
		/*
		uint nz_;          // non-zero AC/DC coeffs
		uint dc_nz_:1;     // non-zero DC coeffs
		uint skip_:1;      // block type
		*/
		uint dc_nz_ { get { throw (new NotImplementedException()); } set { throw (new NotImplementedException()); } }
		uint skip_ { get { throw (new NotImplementedException()); } set { throw (new NotImplementedException()); } }
	}

	
	/// <summary>
	/// Dequantization matrices
	/// [DC / AC].  Can be 'ushort[2]' too (~slower).
	/// </summary>
	/// typedef int quant_t[2];
	public struct quant_t
	{
		public int v0;
		public int v1;
	}

	/// <summary>
	/// 
	/// </summary>
	public struct VP8QuantMatrix
	{
		public quant_t y1_mat_, y2_mat_, uv_mat_;
	}

	/// <summary>
	/// Persistent information needed by the parallel processing
	/// </summary>
	public struct VP8ThreadContext
	{
		/// <summary>
		/// cache row to process (in [0..2])
		/// </summary>
		public int id_;         

		/// <summary>
		/// macroblock position of the row
		/// </summary>
		public int mb_y_;          
		
		/// <summary>
		/// true if row-filtering is needed
		/// </summary>
		public bool filter_row_;   

		/// <summary>
		/// filter strengths
		/// </summary>
		public VP8FInfo* f_info_;  

		/// <summary>
		/// copy of the VP8Io to pass to put()
		/// </summary>
		public VP8Io io_;          
	}

	/// <summary>
	/// VP8Decoder: the main opaque structure handed over to user
	/// </summary>
	unsafe partial class VP8Decoder
	{
		public VP8StatusCode status_;

		/// <summary>
		/// true if ready to decode a picture with VP8Decode()
		/// </summary>
		public bool ready_;    

		/// <summary>
		/// set when status_ is not OK
		/// </summary>
		public string error_msg_;

		// Main data source
		public VP8BitReader br_;

		// headers
		public VP8FrameHeader frm_hdr_;
		public VP8PictureHeader pic_hdr_;
		public VP8FilterHeader filter_hdr_;
		public VP8SegmentHeader segment_hdr_;

		// Worker
		public WebPWorker worker_;

		/// <summary>
		/// use multi-thread
		/// </summary>
		public int use_threads_;   

		/// <summary>
		/// current cache row
		/// </summary>
		public int cache_id_;       

		/// <summary>
		/// number of cached rows of 16 pixels (1, 2 or 3)
		/// </summary>
		public int num_caches_;     

		/// <summary>
		/// Thread context
		/// </summary>
		public VP8ThreadContext thread_ctx_;  

		/// <summary>
		/// dimension, in macroblock units.
		/// </summary>
		public int mb_w_, mb_h_;

		// Macroblock to process/filter, depending on cropping and filter_type.
		public int tl_mb_x_, tl_mb_y_;  // top-left MB that must be in-loop filtered
		public int br_mb_x_, br_mb_y_;  // last bottom-right MB that must be decoded

		/// <summary>
		/// number of partitions.
		/// </summary>
		public int num_parts_;
		
		/// <summary>
		/// per-partition boolean decoders.
		/// </summary>
		public VP8BitReader[] parts_ = new VP8BitReader[(int)_NUMEnum.MAX_NUM_PARTITIONS];

		// buffer refresh flags
		//   bit 0: refresh Gold, bit 1: refresh Alt
		//   bit 2-3: copy to Gold, bit 4-5: copy to Alt
		//   bit 6: Gold sign bias, bit 7: Alt sign bias
		//   bit 8: refresh last frame
		public uint buffer_flags_;

		/// <summary>
		/// dequantization (one set of DC/AC dequant factor per segment)
		/// </summary>
		public VP8QuantMatrix[] dqm_ = new VP8QuantMatrix[(int)_NUMEnum.NUM_MB_SEGMENTS];

		// probabilities
		public VP8Proba proba_;
		public bool use_skip_proba_;
		public byte skip_p_;
#if !ONLY_KEYFRAME_CODE
		public byte intra_p_, last_p_, golden_p_;
		public VP8Proba proba_saved_;
		public int update_proba_;
#endif

		// Boundary data cache and persistent buffers.
		public byte* intra_t_;     // top intra modes values: 4 * mb_w_
		public fixed byte intra_l_[4];  // left intra modes values
		public byte* y_t_;         // top luma samples: 16 * mb_w_
		public byte* u_t_, v_t_;  // top u/v samples: 8 * mb_w_ each

		public VP8MB* mb_info_;       // contextual macroblock info (mb_w_ + 1)
		public VP8FInfo* f_info_;     // filter strength info
		public byte* yuv_b_;       // main block for Y/U/V (size = YUV_SIZE)
		public short* coeffs_;      // 384 coeffs = (16+8+8) * 4*4

		public byte* cache_y_;     // macroblock row for storing unfiltered samples
		public byte* cache_u_;
		public byte* cache_v_;
		public int cache_y_stride_;
		public int cache_uv_stride_;

		// main memory chunk for the above data. Persistent.
		public void* mem_;
		public uint mem_size_;

		// Per macroblock non-persistent infos.
		public int mb_x_, mb_y_;       // current position, in macroblock units
		public byte is_i4x4_;       // true if intra4x4
		public fixed byte imodes_[16];    // one 16x16 mode (#0) or sixteen 4x4 modes
		public byte uvmode_;        // chroma prediction mode
		public byte segment_;       // block's segment

		// bit-wise info about the content of each sub-4x4 blocks: there are 16 bits
		// for luma (bits #0.#15), then 4 bits for chroma-u (#16.#19) and 4 bits for
		// chroma-v (#20.#23), each corresponding to one 4x4 block in decoding order.
		// If the bit is set, the 4x4 block contains some non-zero coefficients.
		public uint non_zero_;
		public uint non_zero_ac_;

		/// <summary>
		/// 0=off, 1=simple, 2=complex
		/// Filtering side-info
		/// </summary>
		public int filter_type_;   
		
		/// <summary>
		/// per-row flag
		/// </summary>
		public int filter_row_;                    
		
		/// <summary>
		/// precalculated per-segment
		/// </summary>
		public fixed byte filter_levels_[(int)_NUMEnum.NUM_MB_SEGMENTS];

		/// <summary>
		/// compressed alpha data (if present)
		/// extensions
		/// </summary>
		public byte* alpha_data_;  
		public uint alpha_data_size_;

		/// <summary>
		/// output
		/// </summary>
		public byte* alpha_plane_;      

		public int layer_colorspace_;

		/// <summary>
		/// compressed layer data (if present)
		/// </summary>
		public byte* layer_data_;
		public uint layer_data_size_;
	}
}

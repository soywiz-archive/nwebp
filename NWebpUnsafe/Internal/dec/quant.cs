﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#if false
namespace NWebp.Internal
{
	public partial class Internal
	{
		static int clip(int v, int M) {
		  return v < 0 ? 0 : v > M ? M : v;
		}

		// Paragraph 14.1
		static readonly byte[] kDcTable = new byte[128]
		{
		  4,     5,   6,   7,   8,   9,  10,  10,
		  11,   12,  13,  14,  15,  16,  17,  17,
		  18,   19,  20,  20,  21,  21,  22,  22,
		  23,   23,  24,  25,  25,  26,  27,  28,
		  29,   30,  31,  32,  33,  34,  35,  36,
		  37,   37,  38,  39,  40,  41,  42,  43,
		  44,   45,  46,  46,  47,  48,  49,  50,
		  51,   52,  53,  54,  55,  56,  57,  58,
		  59,   60,  61,  62,  63,  64,  65,  66,
		  67,   68,  69,  70,  71,  72,  73,  74,
		  75,   76,  76,  77,  78,  79,  80,  81,
		  82,   83,  84,  85,  86,  87,  88,  89,
		  91,   93,  95,  96,  98, 100, 101, 102,
		  104, 106, 108, 110, 112, 114, 116, 118,
		  122, 124, 126, 128, 130, 132, 134, 136,
		  138, 140, 143, 145, 148, 151, 154, 157
		};

		static readonly ushort[] kAcTable = new ushort[128]
		{
		  4,     5,   6,   7,   8,   9,  10,  11,
		  12,   13,  14,  15,  16,  17,  18,  19,
		  20,   21,  22,  23,  24,  25,  26,  27,
		  28,   29,  30,  31,  32,  33,  34,  35,
		  36,   37,  38,  39,  40,  41,  42,  43,
		  44,   45,  46,  47,  48,  49,  50,  51,
		  52,   53,  54,  55,  56,  57,  58,  60,
		  62,   64,  66,  68,  70,  72,  74,  76,
		  78,   80,  82,  84,  86,  88,  90,  92,
		  94,   96,  98, 100, 102, 104, 106, 108,
		  110, 112, 114, 116, 119, 122, 125, 128,
		  131, 134, 137, 140, 143, 146, 149, 152,
		  155, 158, 161, 164, 167, 170, 173, 177,
		  181, 185, 189, 193, 197, 201, 205, 209,
		  213, 217, 221, 225, 229, 234, 239, 245,
		  249, 254, 259, 264, 269, 274, 279, 284
		};

		//------------------------------------------------------------------------------
		// Paragraph 9.6

		void VP8ParseQuant(VP8Decoder* dec) {
		  VP8BitReader* br = &dec.br_;
		  int base_q0 = VP8GetValue(br, 7);
		  int dqy1_dc = VP8Get(br) ? VP8GetSignedValue(br, 4) : 0;
		  int dqy2_dc = VP8Get(br) ? VP8GetSignedValue(br, 4) : 0;
		  int dqy2_ac = VP8Get(br) ? VP8GetSignedValue(br, 4) : 0;
		  int dquv_dc = VP8Get(br) ? VP8GetSignedValue(br, 4) : 0;
		  int dquv_ac = VP8Get(br) ? VP8GetSignedValue(br, 4) : 0;

		  VP8SegmentHeader* hdr = &dec.segment_hdr_;
		  int i;

		  for (i = 0; i < NUM_MB_SEGMENTS; ++i) {
			int q;
			if (hdr.use_segment_) {
			  q = hdr.quantizer_[i];
			  if (!hdr.absolute_delta_) {
				q += base_q0;
			  }
			} else {
			  if (i > 0) {
				dec.dqm_[i] = dec.dqm_[0];
				continue;
			  } else {
				q = base_q0;
			  }
			}
			{
			  VP8QuantMatrix* m = &dec.dqm_[i];
			  m.y1_mat_[0] = kDcTable[clip(q + dqy1_dc, 127)];
			  m.y1_mat_[1] = kAcTable[clip(q + 0,       127)];

			  m.y2_mat_[0] = kDcTable[clip(q + dqy2_dc, 127)] * 2;
			  // TODO(skal): make it another table?
			  m.y2_mat_[1] = kAcTable[clip(q + dqy2_ac, 127)] * 155 / 100;
			  if (m.y2_mat_[1] < 8) m.y2_mat_[1] = 8;

			  m.uv_mat_[0] = kDcTable[clip(q + dquv_dc, 117)];
			  m.uv_mat_[1] = kAcTable[clip(q + dquv_ac, 127)];
			}
		  }
		}

		//------------------------------------------------------------------------------
	}
}
#endif

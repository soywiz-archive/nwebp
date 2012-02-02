using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NWebp.Internal.dsp
{
	class yuv
	{
		enum { YUV_FIX = 16,                // fixed-point precision
			   YUV_RANGE_MIN = -227,        // min value of r/g/b output
			   YUV_RANGE_MAX = 256 + 226    // max value of r/g/b output
		};
		extern short VP8kVToR[256], VP8kUToB[256];
		extern int VP8kVToG[256], VP8kUToG[256];
		extern byte VP8kClip[YUV_RANGE_MAX - YUV_RANGE_MIN];
		extern byte VP8kClip4Bits[YUV_RANGE_MAX - YUV_RANGE_MIN];

		static WEBP_INLINE void VP8YuvToRgb(byte y, byte u, byte v,
											byte* const rgb) {
		  const int r_off = VP8kVToR[v];
		  const int g_off = (VP8kVToG[v] + VP8kUToG[u]) >> YUV_FIX;
		  const int b_off = VP8kUToB[u];
		  rgb[0] = VP8kClip[y + r_off - YUV_RANGE_MIN];
		  rgb[1] = VP8kClip[y + g_off - YUV_RANGE_MIN];
		  rgb[2] = VP8kClip[y + b_off - YUV_RANGE_MIN];
		}

		static WEBP_INLINE void VP8YuvToRgb565(byte y, byte u, byte v,
											   byte* const rgb) {
		  const int r_off = VP8kVToR[v];
		  const int g_off = (VP8kVToG[v] + VP8kUToG[u]) >> YUV_FIX;
		  const int b_off = VP8kUToB[u];
		  rgb[0] = ((VP8kClip[y + r_off - YUV_RANGE_MIN] & 0xf8) |
					(VP8kClip[y + g_off - YUV_RANGE_MIN] >> 5));
		  rgb[1] = (((VP8kClip[y + g_off - YUV_RANGE_MIN] << 3) & 0xe0) |
					(VP8kClip[y + b_off - YUV_RANGE_MIN] >> 3));
		}

		static WEBP_INLINE void VP8YuvToArgbKeepA(byte y, byte u, byte v,
												  byte* const argb) {
		  // Don't update Aplha (argb[0])
		  VP8YuvToRgb(y, u, v, argb + 1);
		}

		static WEBP_INLINE void VP8YuvToArgb(byte y, byte u, byte v,
											 byte* const argb) {
		  argb[0] = 0xff;
		  VP8YuvToArgbKeepA(y, u, v, argb);
		}

		static WEBP_INLINE void VP8YuvToRgba4444KeepA(byte y, byte u, byte v,
													  byte* const argb) {
		  const int r_off = VP8kVToR[v];
		  const int g_off = (VP8kVToG[v] + VP8kUToG[u]) >> YUV_FIX;
		  const int b_off = VP8kUToB[u];
		  // Don't update Aplha (last 4 bits of argb[1])
		  argb[0] = ((VP8kClip4Bits[y + r_off - YUV_RANGE_MIN] << 4) |
					 VP8kClip4Bits[y + g_off - YUV_RANGE_MIN]);
		  argb[1] = (argb[1] & 0x0f) | (VP8kClip4Bits[y + b_off - YUV_RANGE_MIN] << 4);
		}

		static WEBP_INLINE void VP8YuvToRgba4444(byte y, byte u, byte v,
												 byte* const argb) {
		  argb[1] = 0x0f;
		  VP8YuvToRgba4444KeepA(y, u, v, argb);
		}

		static WEBP_INLINE void VP8YuvToBgr(byte y, byte u, byte v,
											byte* const bgr) {
		  const int r_off = VP8kVToR[v];
		  const int g_off = (VP8kVToG[v] + VP8kUToG[u]) >> YUV_FIX;
		  const int b_off = VP8kUToB[u];
		  bgr[0] = VP8kClip[y + b_off - YUV_RANGE_MIN];
		  bgr[1] = VP8kClip[y + g_off - YUV_RANGE_MIN];
		  bgr[2] = VP8kClip[y + r_off - YUV_RANGE_MIN];
		}

		static WEBP_INLINE void VP8YuvToBgra(byte y, byte u, byte v,
											 byte* const bgra) {
		  VP8YuvToBgr(y, u, v, bgra);
		  bgra[3] = 0xff;
		}

		static WEBP_INLINE void VP8YuvToRgba(byte y, byte u, byte v,
											 byte* const rgba) {
		  VP8YuvToRgb(y, u, v, rgba);
		  rgba[3] = 0xff;
		}


		enum { YUV_HALF = 1 << (YUV_FIX - 1) };

		short VP8kVToR[256], VP8kUToB[256];
		int VP8kVToG[256], VP8kUToG[256];
		byte VP8kClip[YUV_RANGE_MAX - YUV_RANGE_MIN];
		byte VP8kClip4Bits[YUV_RANGE_MAX - YUV_RANGE_MIN];

		static int done = 0;

		static WEBP_INLINE byte clip(int v, int max_value) {
		  return v < 0 ? 0 : v > max_value ? max_value : v;
		}

		void VP8YUVInit(void) {
		  int i;
		  if (done) {
			return;
		  }
		  for (i = 0; i < 256; ++i) {
			VP8kVToR[i] = (89858 * (i - 128) + YUV_HALF) >> YUV_FIX;
			VP8kUToG[i] = -22014 * (i - 128) + YUV_HALF;
			VP8kVToG[i] = -45773 * (i - 128);
			VP8kUToB[i] = (113618 * (i - 128) + YUV_HALF) >> YUV_FIX;
		  }
		  for (i = YUV_RANGE_MIN; i < YUV_RANGE_MAX; ++i) {
			const int k = ((i - 16) * 76283 + YUV_HALF) >> YUV_FIX;
			VP8kClip[i - YUV_RANGE_MIN] = clip(k, 255);
			VP8kClip4Bits[i - YUV_RANGE_MIN] = clip((k + 8) >> 4, 15);
		  }
		  done = 1;
		}


	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NWebp.Internal.dsp
{
	class upsampling
	{
		//------------------------------------------------------------------------------
		// Fancy upsampler

		#ifdef FANCY_UPSAMPLING

		// Fancy upsampling functions to convert YUV to RGB
		WebPUpsampleLinePairFunc WebPUpsamplers[MODE_LAST];
		WebPUpsampleLinePairFunc WebPUpsamplersKeepAlpha[MODE_LAST];

		// Given samples laid out in a square as:
		//  [a b]
		//  [c d]
		// we interpolate u/v as:
		//  ([9*a + 3*b + 3*c +   d    3*a + 9*b + 3*c +   d] + [8 8]) / 16
		//  ([3*a +   b + 9*c + 3*d      a + 3*b + 3*c + 9*d]   [8 8]) / 16

		// We process u and v together stashed into 32bit (16bit each).
		#define LOAD_UV(u,v) ((u) | ((v) << 16))

		#define UPSAMPLE_FUNC(FUNC_NAME, FUNC, XSTEP)                                  \
		static void FUNC_NAME(const byte* top_y, const byte* bottom_y,           \
							  const byte* top_u, const byte* top_v,              \
							  const byte* cur_u, const byte* cur_v,              \
							  byte* top_dst, byte* bottom_dst, int len) {        \
		  int x;                                                                       \
		  const int last_pixel_pair = (len - 1) >> 1;                                  \
		  uint tl_uv = LOAD_UV(top_u[0], top_v[0]);   /* top-left sample */        \
		  uint l_uv  = LOAD_UV(cur_u[0], cur_v[0]);   /* left-sample */            \
		  if (top_y) {                                                                 \
			const uint uv0 = (3 * tl_uv + l_uv + 0x00020002u) >> 2;                \
			FUNC(top_y[0], uv0 & 0xff, (uv0 >> 16), top_dst);                          \
		  }                                                                            \
		  if (bottom_y) {                                                              \
			const uint uv0 = (3 * l_uv + tl_uv + 0x00020002u) >> 2;                \
			FUNC(bottom_y[0], uv0 & 0xff, (uv0 >> 16), bottom_dst);                    \
		  }                                                                            \
		  for (x = 1; x <= last_pixel_pair; ++x) {                                     \
			const uint t_uv = LOAD_UV(top_u[x], top_v[x]);  /* top sample */       \
			const uint uv   = LOAD_UV(cur_u[x], cur_v[x]);  /* sample */           \
			/* precompute invariant values associated with first and second diagonals*/\
			const uint avg = tl_uv + t_uv + l_uv + uv + 0x00080008u;               \
			const uint diag_12 = (avg + 2 * (t_uv + l_uv)) >> 3;                   \
			const uint diag_03 = (avg + 2 * (tl_uv + uv)) >> 3;                    \
			if (top_y) {                                                               \
			  const uint uv0 = (diag_12 + tl_uv) >> 1;                             \
			  const uint uv1 = (diag_03 + t_uv) >> 1;                              \
			  FUNC(top_y[2 * x - 1], uv0 & 0xff, (uv0 >> 16),                          \
				   top_dst + (2 * x - 1) * XSTEP);                                     \
			  FUNC(top_y[2 * x - 0], uv1 & 0xff, (uv1 >> 16),                          \
				   top_dst + (2 * x - 0) * XSTEP);                                     \
			}                                                                          \
			if (bottom_y) {                                                            \
			  const uint uv0 = (diag_03 + l_uv) >> 1;                              \
			  const uint uv1 = (diag_12 + uv) >> 1;                                \
			  FUNC(bottom_y[2 * x - 1], uv0 & 0xff, (uv0 >> 16),                       \
				   bottom_dst + (2 * x - 1) * XSTEP);                                  \
			  FUNC(bottom_y[2 * x + 0], uv1 & 0xff, (uv1 >> 16),                       \
				   bottom_dst + (2 * x + 0) * XSTEP);                                  \
			}                                                                          \
			tl_uv = t_uv;                                                              \
			l_uv = uv;                                                                 \
		  }                                                                            \
		  if (!(len & 1)) {                                                            \
			if (top_y) {                                                               \
			  const uint uv0 = (3 * tl_uv + l_uv + 0x00020002u) >> 2;              \
			  FUNC(top_y[len - 1], uv0 & 0xff, (uv0 >> 16),                            \
				   top_dst + (len - 1) * XSTEP);                                       \
			}                                                                          \
			if (bottom_y) {                                                            \
			  const uint uv0 = (3 * l_uv + tl_uv + 0x00020002u) >> 2;              \
			  FUNC(bottom_y[len - 1], uv0 & 0xff, (uv0 >> 16),                         \
				   bottom_dst + (len - 1) * XSTEP);                                    \
			}                                                                          \
		  }                                                                            \
		}

		// All variants implemented.
		UPSAMPLE_FUNC(UpsampleRgbLinePair,  VP8YuvToRgb,  3)
		UPSAMPLE_FUNC(UpsampleBgrLinePair,  VP8YuvToBgr,  3)
		UPSAMPLE_FUNC(UpsampleRgbaLinePair, VP8YuvToRgba, 4)
		UPSAMPLE_FUNC(UpsampleBgraLinePair, VP8YuvToBgra, 4)
		UPSAMPLE_FUNC(UpsampleArgbLinePair, VP8YuvToArgb, 4)
		UPSAMPLE_FUNC(UpsampleRgba4444LinePair, VP8YuvToRgba4444, 2)
		UPSAMPLE_FUNC(UpsampleRgb565LinePair,  VP8YuvToRgb565,  2)
		// These variants don't erase the alpha value
		UPSAMPLE_FUNC(UpsampleRgbaKeepAlphaLinePair, VP8YuvToRgb, 4)
		UPSAMPLE_FUNC(UpsampleBgraKeepAlphaLinePair, VP8YuvToBgr, 4)
		UPSAMPLE_FUNC(UpsampleArgbKeepAlphaLinePair, VP8YuvToArgbKeepA, 4)
		UPSAMPLE_FUNC(UpsampleRgba4444KeepAlphaLinePair, VP8YuvToRgba4444KeepA, 2)

		#undef LOAD_UV
		#undef UPSAMPLE_FUNC

		#endif  // FANCY_UPSAMPLING

		//------------------------------------------------------------------------------
		// simple point-sampling

		#define SAMPLE_FUNC(FUNC_NAME, FUNC, XSTEP)                                    \
		static void FUNC_NAME(const byte* top_y, const byte* bottom_y,           \
							  const byte* u, const byte* v,                      \
							  byte* top_dst, byte* bottom_dst, int len) {        \
		  int i;                                                                       \
		  for (i = 0; i < len - 1; i += 2) {                                           \
			FUNC(top_y[0], u[0], v[0], top_dst);                                       \
			FUNC(top_y[1], u[0], v[0], top_dst + XSTEP);                               \
			FUNC(bottom_y[0], u[0], v[0], bottom_dst);                                 \
			FUNC(bottom_y[1], u[0], v[0], bottom_dst + XSTEP);                         \
			top_y += 2;                                                                \
			bottom_y += 2;                                                             \
			u++;                                                                       \
			v++;                                                                       \
			top_dst += 2 * XSTEP;                                                      \
			bottom_dst += 2 * XSTEP;                                                   \
		  }                                                                            \
		  if (i == len - 1) {    /* last one */                                        \
			FUNC(top_y[0], u[0], v[0], top_dst);                                       \
			FUNC(bottom_y[0], u[0], v[0], bottom_dst);                                 \
		  }                                                                            \
		}

		// All variants implemented.
		SAMPLE_FUNC(SampleRgbLinePair,      VP8YuvToRgb,  3)
		SAMPLE_FUNC(SampleBgrLinePair,      VP8YuvToBgr,  3)
		SAMPLE_FUNC(SampleRgbaLinePair,     VP8YuvToRgba, 4)
		SAMPLE_FUNC(SampleBgraLinePair,     VP8YuvToBgra, 4)
		SAMPLE_FUNC(SampleArgbLinePair,     VP8YuvToArgb, 4)
		SAMPLE_FUNC(SampleRgba4444LinePair, VP8YuvToRgba4444, 2)
		SAMPLE_FUNC(SampleRgb565LinePair,   VP8YuvToRgb565, 2)
		// These variants don't erase the alpha value
		SAMPLE_FUNC(SampleRgbaKeepAlphaLinePair, VP8YuvToRgb, 4)
		SAMPLE_FUNC(SampleBgraKeepAlphaLinePair, VP8YuvToBgr, 4)
		SAMPLE_FUNC(SampleArgbKeepAlphaLinePair, VP8YuvToArgbKeepA, 4)
		SAMPLE_FUNC(SampleRgba4444KeepAlphaLinePair, VP8YuvToRgba4444KeepA, 2)

		#undef SAMPLE_FUNC

		const WebPSampleLinePairFunc WebPSamplers[MODE_LAST] = {
		  SampleRgbLinePair,       // MODE_RGB
		  SampleRgbaLinePair,      // MODE_RGBA
		  SampleBgrLinePair,       // MODE_BGR
		  SampleBgraLinePair,      // MODE_BGRA
		  SampleArgbLinePair,      // MODE_ARGB
		  SampleRgba4444LinePair,  // MODE_RGBA_4444
		  SampleRgb565LinePair     // MODE_RGB_565
		};

		const WebPSampleLinePairFunc WebPSamplersKeepAlpha[MODE_LAST] = {
		  SampleRgbLinePair,                // MODE_RGB
		  SampleRgbaKeepAlphaLinePair,      // MODE_RGBA
		  SampleBgrLinePair,                // MODE_BGR
		  SampleBgraKeepAlphaLinePair,      // MODE_BGRA
		  SampleArgbKeepAlphaLinePair,      // MODE_ARGB
		  SampleRgba4444KeepAlphaLinePair,  // MODE_RGBA_4444
		  SampleRgb565LinePair              // MODE_RGB_565
		};

		//------------------------------------------------------------------------------
		// YUV444 converter

		#define YUV444_FUNC(FUNC_NAME, FUNC, XSTEP)                                    \
		static void FUNC_NAME(const byte* y, const byte* u, const byte* v,    \
							  byte* dst, int len) {                                 \
		  int i;                                                                       \
		  for (i = 0; i < len; ++i) FUNC(y[i], u[i], v[i], &dst[i * XSTEP]);           \
		}

		YUV444_FUNC(Yuv444ToRgb,      VP8YuvToRgb,  3)
		YUV444_FUNC(Yuv444ToBgr,      VP8YuvToBgr,  3)
		YUV444_FUNC(Yuv444ToRgba,     VP8YuvToRgba, 4)
		YUV444_FUNC(Yuv444ToBgra,     VP8YuvToBgra, 4)
		YUV444_FUNC(Yuv444ToArgb,     VP8YuvToArgb, 4)
		YUV444_FUNC(Yuv444ToRgba4444, VP8YuvToRgba4444, 2)
		YUV444_FUNC(Yuv444ToRgb565,   VP8YuvToRgb565, 2)

		#undef YUV444_FUNC

		const WebPYUV444Converter WebPYUV444Converters[MODE_LAST] = {
		  Yuv444ToRgb,       // MODE_RGB
		  Yuv444ToRgba,      // MODE_RGBA
		  Yuv444ToBgr,       // MODE_BGR
		  Yuv444ToBgra,      // MODE_BGRA
		  Yuv444ToArgb,      // MODE_ARGB
		  Yuv444ToRgba4444,  // MODE_RGBA_4444
		  Yuv444ToRgb565     // MODE_RGB_565
		};

		//------------------------------------------------------------------------------
		// Main call

		void WebPInitUpsamplers(void) {
		#ifdef FANCY_UPSAMPLING
		  WebPUpsamplers[MODE_RGB]       = UpsampleRgbLinePair;
		  WebPUpsamplers[MODE_RGBA]      = UpsampleRgbaLinePair;
		  WebPUpsamplers[MODE_BGR]       = UpsampleBgrLinePair;
		  WebPUpsamplers[MODE_BGRA]      = UpsampleBgraLinePair;
		  WebPUpsamplers[MODE_ARGB]      = UpsampleArgbLinePair;
		  WebPUpsamplers[MODE_RGBA_4444] = UpsampleRgba4444LinePair;
		  WebPUpsamplers[MODE_RGB_565]   = UpsampleRgb565LinePair;

		  WebPUpsamplersKeepAlpha[MODE_RGB]       = UpsampleRgbLinePair;
		  WebPUpsamplersKeepAlpha[MODE_RGBA]      = UpsampleRgbaKeepAlphaLinePair;
		  WebPUpsamplersKeepAlpha[MODE_BGR]       = UpsampleBgrLinePair;
		  WebPUpsamplersKeepAlpha[MODE_BGRA]      = UpsampleBgraKeepAlphaLinePair;
		  WebPUpsamplersKeepAlpha[MODE_ARGB]      = UpsampleArgbKeepAlphaLinePair;
		  WebPUpsamplersKeepAlpha[MODE_RGBA_4444] = UpsampleRgba4444KeepAlphaLinePair;
		  WebPUpsamplersKeepAlpha[MODE_RGB_565]   = UpsampleRgb565LinePair;

		  // If defined, use CPUInfo() to overwrite some pointers with faster versions.
		  if (VP8GetCPUInfo) {
		#if defined(WEBP_USE_SSE2)
			if (VP8GetCPUInfo(kSSE2)) {
			  WebPInitUpsamplersSSE2();
			}
		#endif
		  }
		#endif  // FANCY_UPSAMPLING
		}


	}
}

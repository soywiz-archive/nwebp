﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NWebp.Internal
{
	/*
	class dsp
	{

		//------------------------------------------------------------------------------
		// CPU detection

		#if defined(_MSC_VER) && (defined(_M_X64) || defined(_M_IX86))
		#define WEBP_MSC_SSE2  // Visual C++ SSE2 targets
		#endif

		#if defined(__SSE2__) || defined(WEBP_MSC_SSE2)
		#define WEBP_USE_SSE2
		#endif

		enum CPUFeature
		{
		  kSSE2,
		  kSSE3,
		  kNEON
		} ;
		// returns true if the CPU supports the feature.
		typedef int (*VP8CPUInfo)(CPUFeature feature);
		extern VP8CPUInfo VP8GetCPUInfo;

		//------------------------------------------------------------------------------
		// Encoding

		int VP8GetAlpha(int histo[]);

		// Transforms
		// VP8Idct: Does one of two inverse transforms. If do_two is set, the transforms
		//          will be done for (ref, in, dst) and (ref + 4, in + 16, dst + 4).
		typedef void (*VP8Idct)(byte* ref, short* in, byte* dst,
								int do_two);
		typedef void (*VP8Fdct)(byte* src, byte* ref, short* out);
		typedef void (*VP8WHT)(short* in, short* out);
		extern VP8Idct VP8ITransform;
		extern VP8Fdct VP8FTransform;
		extern VP8WHT VP8ITransformWHT;
		extern VP8WHT VP8FTransformWHT;
		// Predictions
		// *dst is the destination block. *top and *left can be null.
		typedef void (*VP8IntraPreds)(byte *dst, byte* left,
									  byte* top);
		typedef void (*VP8Intra4Preds)(byte *dst, byte* top);
		extern VP8Intra4Preds VP8EncPredLuma4;
		extern VP8IntraPreds VP8EncPredLuma16;
		extern VP8IntraPreds VP8EncPredChroma8;

		typedef int (*VP8Metric)(byte* pix, byte* ref);
		extern VP8Metric VP8SSE16x16, VP8SSE16x8, VP8SSE8x8, VP8SSE4x4;
		typedef int (*VP8WMetric)(byte* pix, byte* ref,
								  ushort* weights);
		extern VP8WMetric VP8TDisto4x4, VP8TDisto16x16;

		typedef void (*VP8BlockCopy)(byte* src, byte* dst);
		extern VP8BlockCopy VP8Copy4x4;
		// Quantization
		struct VP8Matrix;   // forward declaration
		typedef int (*VP8QuantizeBlock)(short in[16], short out[16],
										int n, struct VP8Matrix* mtx);
		extern VP8QuantizeBlock VP8EncQuantizeBlock;

		// Compute susceptibility based on DCT-coeff histograms:
		// the higher, the "easier" the macroblock is to compress.
		typedef int (*VP8CHisto)(byte* ref, byte* pred,
								 int start_block, int end_block);
		extern int VP8DspScan[16 + 4 + 4];
		extern VP8CHisto VP8CollectHistogram;

		void VP8EncDspInit(void);   // must be called before using any of the above

		//------------------------------------------------------------------------------
		// Decoding

		typedef void (*VP8DecIdct)(short* coeffs, byte* dst);
		// when doing two transforms, coeffs is actually short[2][16].
		typedef void (*VP8DecIdct2)(short* coeffs, byte* dst, int do_two);
		extern VP8DecIdct2 VP8Transform;
		extern VP8DecIdct VP8TransformUV;
		extern VP8DecIdct VP8TransformDC;
		extern VP8DecIdct VP8TransformDCUV;
		extern void (*VP8TransformWHT)(short* in, short* out);

		// *dst is the destination block, with stride BPS. Boundary samples are
		// assumed accessible when needed.
		typedef void (*VP8PredFunc)(byte* dst);
		extern VP8PredFunc VP8PredLuma16[// NUM_B_DC_MODES ];
		extern VP8PredFunc VP8PredChroma8[// NUM_B_DC_MODES ];
		extern VP8PredFunc VP8PredLuma4[// NUM_BMODES ];

		// simple filter (only for luma)
		typedef void (*VP8SimpleFilterFunc)(byte* p, int stride, int thresh);
		extern VP8SimpleFilterFunc VP8SimpleVFilter16;
		extern VP8SimpleFilterFunc VP8SimpleHFilter16;
		extern VP8SimpleFilterFunc VP8SimpleVFilter16i;  // filter 3 inner edges
		extern VP8SimpleFilterFunc VP8SimpleHFilter16i;

		// regular filter (on both macroblock edges and inner edges)
		typedef void (*VP8LumaFilterFunc)(byte* luma, int stride,
										  int thresh, int ithresh, int hev_t);
		typedef void (*VP8ChromaFilterFunc)(byte* u, byte* v, int stride,
											int thresh, int ithresh, int hev_t);
		// on outer edge
		extern VP8LumaFilterFunc VP8VFilter16;
		extern VP8LumaFilterFunc VP8HFilter16;
		extern VP8ChromaFilterFunc VP8VFilter8;
		extern VP8ChromaFilterFunc VP8HFilter8;

		// on inner edge
		extern VP8LumaFilterFunc VP8VFilter16i;   // filtering 3 inner edges altogether
		extern VP8LumaFilterFunc VP8HFilter16i;
		extern VP8ChromaFilterFunc VP8VFilter8i;  // filtering u and v altogether
		extern VP8ChromaFilterFunc VP8HFilter8i;

		// must be called before anything using the above
		void VP8DspInit(void);

		//------------------------------------------------------------------------------
		// WebP I/O

		#define FANCY_UPSAMPLING   // undefined to remove fancy upsampling support

		#if FANCY_UPSAMPLING
		typedef void (*WebPUpsampleLinePairFunc)(
			byte* top_y, byte* bottom_y,
			byte* top_u, byte* top_v,
			byte* cur_u, byte* cur_v,
			byte* top_dst, byte* bottom_dst, int len);


		// Fancy upsampling functions to convert YUV to RGB(A) modes
		extern WebPUpsampleLinePairFunc WebPUpsamplers[// MODE_LAST //];
		extern WebPUpsampleLinePairFunc WebPUpsamplersKeepAlpha[// MODE_LAST //];

		// Initializes SSE2 version of the fancy upsamplers.
		void WebPInitUpsamplersSSE2(void);

		#endif    // FANCY_UPSAMPLING

		// Point-sampling methods.
		typedef void (*WebPSampleLinePairFunc)(
			byte* top_y, byte* bottom_y,
			byte* u, byte* v,
			byte* top_dst, byte* bottom_dst, int len);

		extern WebPSampleLinePairFunc WebPSamplers[// MODE_LAST //];
		extern WebPSampleLinePairFunc WebPSamplersKeepAlpha[// MODE_LAST //];

		// YUV444.RGB converters
		typedef void (*WebPYUV444Converter)(byte* y,
											byte* u, byte* v,
											byte* dst, int len);

		extern WebPYUV444Converter WebPYUV444Converters[// MODE_LAST //];

		// Main function to be called
		void WebPInitUpsamplers(void);

		//------------------------------------------------------------------------------


	}
	*/
}

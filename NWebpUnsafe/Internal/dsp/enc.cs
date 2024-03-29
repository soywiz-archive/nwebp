﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#if false
namespace NWebp.Internal
{
	class enc
	{

		//------------------------------------------------------------------------------
		// Compute susceptibility based on DCT-coeff histograms:
		// the higher, the "easier" the macroblock is to compress.

		static int ClipAlpha(int alpha) {
			return alpha < 0 ? 0 : alpha > 255 ? 255 : alpha;
		}

		int VP8GetAlpha(int histo[MAX_COEFF_THRESH + 1]) {
			int num = 0, den = 0, val = 0;
			int k;
			int alpha;
			// note: changing this loop to avoid the numerous "k + 1" slows things down.
			for (k = 0; k < MAX_COEFF_THRESH; ++k) {
			if (histo[k + 1]) {
				val += histo[k + 1];
				num += val * (k + 1);
				den += (k + 1) * (k + 1);
			}
			}
			// we scale the value to a usable [0..255] range
			alpha = den ? 10 * num / den - 5 : 0;
			return ClipAlpha(alpha);
		}

		int VP8DspScan[16 + 4 + 4] = {
			// Luma
			0 +  0 * BPS,  4 +  0 * BPS, 8 +  0 * BPS, 12 +  0 * BPS,
			0 +  4 * BPS,  4 +  4 * BPS, 8 +  4 * BPS, 12 +  4 * BPS,
			0 +  8 * BPS,  4 +  8 * BPS, 8 +  8 * BPS, 12 +  8 * BPS,
			0 + 12 * BPS,  4 + 12 * BPS, 8 + 12 * BPS, 12 + 12 * BPS,

			0 + 0 * BPS,   4 + 0 * BPS, 0 + 4 * BPS,  4 + 4 * BPS,    // U
			8 + 0 * BPS,  12 + 0 * BPS, 8 + 4 * BPS, 12 + 4 * BPS     // V
		};

		static int CollectHistogram(byte* ref, byte* pred,
									int start_block, int end_block) {
			int histo[MAX_COEFF_THRESH + 1] = { 0 };
			short out[16];
			int j, k;
			for (j = start_block; j < end_block; ++j) {
			VP8FTransform(ref + VP8DspScan[j], pred + VP8DspScan[j], out);

			// Convert coefficients to bin (within out[]).
			for (k = 0; k < 16; ++k) {
				int v = abs(out[k]) >> 2;
				out[k] = (v > MAX_COEFF_THRESH) ? MAX_COEFF_THRESH : v;
			}

			// Use bin to update histogram.
			for (k = 0; k < 16; ++k) {
				histo[out[k]]++;
			}
			}

			return VP8GetAlpha(histo);
		}

		//------------------------------------------------------------------------------
		// run-time tables (~4k)

		static byte clip1[255 + 510 + 1];    // clips [-255,510] to [0,255]

		// We declare this variable 'volatile' to prevent instruction reordering
		// and make sure it's set to true _last_ (so as to be thread-safe)
		static volatile int tables_ok = 0;

		static void InitTables(void) {
			if (!tables_ok) {
			int i;
			for (i = -255; i <= 255 + 255; ++i) {
				clip1[255 + i] = (i < 0) ? 0 : (i > 255) ? 255 : i;
			}
			tables_ok = 1;
			}
		}

		static byte clip_8b(int v) {
			return (!(v & ~0xff)) ? v : v < 0 ? 0 : 255;
		}

		//------------------------------------------------------------------------------
		// Transforms (Paragraph 14.4)

		void STORE(x, y, v) { return dst[(x) + (y) * BPS] = clip_8b(ref[(x) + (y) * BPS] + ((v) >> 3)); }

		static int kC1 = 20091 + (1 << 16);
		static int kC2 = 35468;
		void MUL(a, b) { return (((a) * (b)) >> 16); }

		static void ITransformOne(byte* ref, short* in,
												byte* dst) {
			int C[4 * 4], *tmp;
			int i;
			tmp = C;
			for (i = 0; i < 4; ++i) {    // vertical pass
			int a = in[0] + in[8];
			int b = in[0] - in[8];
			int c = MUL(in[4], kC2) - MUL(in[12], kC1);
			int d = MUL(in[4], kC1) + MUL(in[12], kC2);
			tmp[0] = a + d;
			tmp[1] = b + c;
			tmp[2] = b - c;
			tmp[3] = a - d;
			tmp += 4;
			in++;
			}

			tmp = C;
			for (i = 0; i < 4; ++i) {    // horizontal pass
			int dc = tmp[0] + 4;
			int a =  dc +  tmp[8];
			int b =  dc -  tmp[8];
			int c = MUL(tmp[4], kC2) - MUL(tmp[12], kC1);
			int d = MUL(tmp[4], kC1) + MUL(tmp[12], kC2);
			STORE(0, i, a + d);
			STORE(1, i, b + c);
			STORE(2, i, b - c);
			STORE(3, i, a - d);
			tmp++;
			}
		}

		static void ITransform(byte* ref, short* in, byte* dst,
								int do_two) {
			ITransformOne(ref, in, dst);
			if (do_two) {
			ITransformOne(ref + 4, in + 16, dst + 4);
			}
		}

		static void FTransform(byte* src, byte* ref, short* out) {
			int i;
			int tmp[16];
			for (i = 0; i < 4; ++i, src += BPS, ref += BPS) {
			int d0 = src[0] - ref[0];
			int d1 = src[1] - ref[1];
			int d2 = src[2] - ref[2];
			int d3 = src[3] - ref[3];
			int a0 = (d0 + d3) << 3;
			int a1 = (d1 + d2) << 3;
			int a2 = (d1 - d2) << 3;
			int a3 = (d0 - d3) << 3;
			tmp[0 + i * 4] = (a0 + a1);
			tmp[1 + i * 4] = (a2 * 2217 + a3 * 5352 + 14500) >> 12;
			tmp[2 + i * 4] = (a0 - a1);
			tmp[3 + i * 4] = (a3 * 2217 - a2 * 5352 +  7500) >> 12;
			}
			for (i = 0; i < 4; ++i) {
			int a0 = (tmp[0 + i] + tmp[12 + i]);
			int a1 = (tmp[4 + i] + tmp[ 8 + i]);
			int a2 = (tmp[4 + i] - tmp[ 8 + i]);
			int a3 = (tmp[0 + i] - tmp[12 + i]);
			out[0 + i] = (a0 + a1 + 7) >> 4;
			out[4 + i] = ((a2 * 2217 + a3 * 5352 + 12000) >> 16) + (a3 != 0);
			out[8 + i] = (a0 - a1 + 7) >> 4;
			out[12+ i] = ((a3 * 2217 - a2 * 5352 + 51000) >> 16);
			}
		}

		static void ITransformWHT(short* in, short* out) {
			int tmp[16];
			int i;
			for (i = 0; i < 4; ++i) {
			int a0 = in[0 + i] + in[12 + i];
			int a1 = in[4 + i] + in[ 8 + i];
			int a2 = in[4 + i] - in[ 8 + i];
			int a3 = in[0 + i] - in[12 + i];
			tmp[0  + i] = a0 + a1;
			tmp[8  + i] = a0 - a1;
			tmp[4  + i] = a3 + a2;
			tmp[12 + i] = a3 - a2;
			}
			for (i = 0; i < 4; ++i) {
			int dc = tmp[0 + i * 4] + 3;    // w/ rounder
			int a0 = dc             + tmp[3 + i * 4];
			int a1 = tmp[1 + i * 4] + tmp[2 + i * 4];
			int a2 = tmp[1 + i * 4] - tmp[2 + i * 4];
			int a3 = dc             - tmp[3 + i * 4];
			out[ 0] = (a0 + a1) >> 3;
			out[16] = (a3 + a2) >> 3;
			out[32] = (a0 - a1) >> 3;
			out[48] = (a3 - a2) >> 3;
			out += 64;
			}
		}

		static void FTransformWHT(short* in, short* out) {
			int tmp[16];
			int i;
			for (i = 0; i < 4; ++i, in += 64) {
			int a0 = (in[0 * 16] + in[2 * 16]) << 2;
			int a1 = (in[1 * 16] + in[3 * 16]) << 2;
			int a2 = (in[1 * 16] - in[3 * 16]) << 2;
			int a3 = (in[0 * 16] - in[2 * 16]) << 2;
			tmp[0 + i * 4] = (a0 + a1) + (a0 != 0);
			tmp[1 + i * 4] = a3 + a2;
			tmp[2 + i * 4] = a3 - a2;
			tmp[3 + i * 4] = a0 - a1;
			}
			for (i = 0; i < 4; ++i) {
			int a0 = (tmp[0 + i] + tmp[8 + i]);
			int a1 = (tmp[4 + i] + tmp[12+ i]);
			int a2 = (tmp[4 + i] - tmp[12+ i]);
			int a3 = (tmp[0 + i] - tmp[8 + i]);
			int b0 = a0 + a1;
			int b1 = a3 + a2;
			int b2 = a3 - a2;
			int b3 = a0 - a1;
			out[ 0 + i] = (b0 + (b0 > 0) + 3) >> 3;
			out[ 4 + i] = (b1 + (b1 > 0) + 3) >> 3;
			out[ 8 + i] = (b2 + (b2 > 0) + 3) >> 3;
			out[12 + i] = (b3 + (b3 > 0) + 3) >> 3;
			}
		}

		#undef MUL
		#undef STORE

		//------------------------------------------------------------------------------
		// Intra predictions

		void DST(x, y) { return dst[(x) + (y) * BPS]; }

		static void Fill(byte* dst, int value, int size) {
			int j;
			for (j = 0; j < size; ++j) {
			memset(dst + j * BPS, value, size);
			}
		}

		static void VerticalPred(byte* dst,
												byte* top, int size) {
			int j;
			if (top) {
			for (j = 0; j < size; ++j) memcpy(dst + j * BPS, top, size);
			} else {
			Fill(dst, 127, size);
			}
		}

		static void HorizontalPred(byte* dst,
												byte* left, int size) {
			if (left) {
			int j;
			for (j = 0; j < size; ++j) {
				memset(dst + j * BPS, left[j], size);
			}
			} else {
			Fill(dst, 129, size);
			}
		}

		static void TrueMotion(byte* dst, byte* left,
											byte* top, int size) {
			int y;
			if (left) {
			if (top) {
				byte* clip = clip1 + 255 - left[-1];
				for (y = 0; y < size; ++y) {
				byte* clip_table = clip + left[y];
				int x;
				for (x = 0; x < size; ++x) {
					dst[x] = clip_table[top[x]];
				}
				dst += BPS;
				}
			} else {
				HorizontalPred(dst, left, size);
			}
			} else {
			// true motion without left samples (hence: with default 129 value)
			// is equivalent to VE prediction where you just copy the top samples.
			// Note that if top samples are not available, the default value is
			// then 129, and not 127 as in the VerticalPred case.
			if (top) {
				VerticalPred(dst, top, size);
			} else {
				Fill(dst, 129, size);
			}
			}
		}

		static void DCMode(byte* dst, byte* left,
										byte* top,
										int size, int round, int shift) {
			int DC = 0;
			int j;
			if (top) {
			for (j = 0; j < size; ++j) DC += top[j];
			if (left) {   // top and left present
				for (j = 0; j < size; ++j) DC += left[j];
			} else {      // top, but no left
				DC += DC;
			}
			DC = (DC + round) >> shift;
			} else if (left) {   // left but no top
			for (j = 0; j < size; ++j) DC += left[j];
			DC += DC;
			DC = (DC + round) >> shift;
			} else {   // no top, no left, nothing.
			DC = 0x80;
			}
			Fill(dst, DC, size);
		}

		//------------------------------------------------------------------------------
		// Chroma 8x8 prediction (paragraph 12.2)

		static void IntraChromaPreds(byte* dst, byte* left,
										byte* top) {
			// U block
			DCMode(C8DC8 + dst, left, top, 8, 8, 4);
			VerticalPred(C8VE8 + dst, top, 8);
			HorizontalPred(C8HE8 + dst, left, 8);
			TrueMotion(C8TM8 + dst, left, top, 8);
			// V block
			dst += 8;
			if (top) top += 8;
			if (left) left += 16;
			DCMode(C8DC8 + dst, left, top, 8, 8, 4);
			VerticalPred(C8VE8 + dst, top, 8);
			HorizontalPred(C8HE8 + dst, left, 8);
			TrueMotion(C8TM8 + dst, left, top, 8);
		}

		//------------------------------------------------------------------------------
		// luma 16x16 prediction (paragraph 12.3)

		static void Intra16Preds(byte* dst,
									byte* left, byte* top) {
			DCMode(I16DC16 + dst, left, top, 16, 16, 5);
			VerticalPred(I16VE16 + dst, top, 16);
			HorizontalPred(I16HE16 + dst, left, 16);
			TrueMotion(I16TM16 + dst, left, top, 16);
		}

		//------------------------------------------------------------------------------
		// luma 4x4 prediction

		void AVG3(a, b, c) { return (((a) + 2 * (b) + (c) + 2) >> 2);
		void AVG2(a, b) { return (((a) + (b) + 1) >> 1); }

		static void VE4(byte* dst, byte* top) {    // vertical
			byte vals[4] = {
			AVG3(top[-1], top[0], top[1]),
			AVG3(top[ 0], top[1], top[2]),
			AVG3(top[ 1], top[2], top[3]),
			AVG3(top[ 2], top[3], top[4])
			};
			int i;
			for (i = 0; i < 4; ++i) {
			memcpy(dst + i * BPS, vals, 4);
			}
		}

		static void HE4(byte* dst, byte* top) {    // horizontal
			int X = top[-1];
			int I = top[-2];
			int J = top[-3];
			int K = top[-4];
			int L = top[-5];
			*(uint*)(dst + 0 * BPS) = 0x01010101U * AVG3(X, I, J);
			*(uint*)(dst + 1 * BPS) = 0x01010101U * AVG3(I, J, K);
			*(uint*)(dst + 2 * BPS) = 0x01010101U * AVG3(J, K, L);
			*(uint*)(dst + 3 * BPS) = 0x01010101U * AVG3(K, L, L);
		}

		static void DC4(byte* dst, byte* top) {
			uint dc = 4;
			int i;
			for (i = 0; i < 4; ++i) dc += top[i] + top[-5 + i];
			Fill(dst, dc >> 3, 4);
		}

		static void RD4(byte* dst, byte* top) {
			int X = top[-1];
			int I = top[-2];
			int J = top[-3];
			int K = top[-4];
			int L = top[-5];
			int A = top[0];
			int B = top[1];
			int C = top[2];
			int D = top[3];
			DST(0, 3)                                     = AVG3(J, K, L);
			DST(0, 2) = DST(1, 3)                         = AVG3(I, J, K);
			DST(0, 1) = DST(1, 2) = DST(2, 3)             = AVG3(X, I, J);
			DST(0, 0) = DST(1, 1) = DST(2, 2) = DST(3, 3) = AVG3(A, X, I);
			DST(1, 0) = DST(2, 1) = DST(3, 2)             = AVG3(B, A, X);
			DST(2, 0) = DST(3, 1)                         = AVG3(C, B, A);
			DST(3, 0)                                     = AVG3(D, C, B);
		}

		static void LD4(byte* dst, byte* top) {
			int A = top[0];
			int B = top[1];
			int C = top[2];
			int D = top[3];
			int E = top[4];
			int F = top[5];
			int G = top[6];
			int H = top[7];
			DST(0, 0)                                     = AVG3(A, B, C);
			DST(1, 0) = DST(0, 1)                         = AVG3(B, C, D);
			DST(2, 0) = DST(1, 1) = DST(0, 2)             = AVG3(C, D, E);
			DST(3, 0) = DST(2, 1) = DST(1, 2) = DST(0, 3) = AVG3(D, E, F);
			DST(3, 1) = DST(2, 2) = DST(1, 3)             = AVG3(E, F, G);
			DST(3, 2) = DST(2, 3)                         = AVG3(F, G, H);
			DST(3, 3)                                     = AVG3(G, H, H);
		}

		static void VR4(byte* dst, byte* top) {
			int X = top[-1];
			int I = top[-2];
			int J = top[-3];
			int K = top[-4];
			int A = top[0];
			int B = top[1];
			int C = top[2];
			int D = top[3];
			DST(0, 0) = DST(1, 2) = AVG2(X, A);
			DST(1, 0) = DST(2, 2) = AVG2(A, B);
			DST(2, 0) = DST(3, 2) = AVG2(B, C);
			DST(3, 0)             = AVG2(C, D);

			DST(0, 3) =             AVG3(K, J, I);
			DST(0, 2) =             AVG3(J, I, X);
			DST(0, 1) = DST(1, 3) = AVG3(I, X, A);
			DST(1, 1) = DST(2, 3) = AVG3(X, A, B);
			DST(2, 1) = DST(3, 3) = AVG3(A, B, C);
			DST(3, 1) =             AVG3(B, C, D);
		}

		static void VL4(byte* dst, byte* top) {
			int A = top[0];
			int B = top[1];
			int C = top[2];
			int D = top[3];
			int E = top[4];
			int F = top[5];
			int G = top[6];
			int H = top[7];
			DST(0, 0) =             AVG2(A, B);
			DST(1, 0) = DST(0, 2) = AVG2(B, C);
			DST(2, 0) = DST(1, 2) = AVG2(C, D);
			DST(3, 0) = DST(2, 2) = AVG2(D, E);

			DST(0, 1) =             AVG3(A, B, C);
			DST(1, 1) = DST(0, 3) = AVG3(B, C, D);
			DST(2, 1) = DST(1, 3) = AVG3(C, D, E);
			DST(3, 1) = DST(2, 3) = AVG3(D, E, F);
						DST(3, 2) = AVG3(E, F, G);
						DST(3, 3) = AVG3(F, G, H);
		}

		static void HU4(byte* dst, byte* top) {
			int I = top[-2];
			int J = top[-3];
			int K = top[-4];
			int L = top[-5];
			DST(0, 0) =             AVG2(I, J);
			DST(2, 0) = DST(0, 1) = AVG2(J, K);
			DST(2, 1) = DST(0, 2) = AVG2(K, L);
			DST(1, 0) =             AVG3(I, J, K);
			DST(3, 0) = DST(1, 1) = AVG3(J, K, L);
			DST(3, 1) = DST(1, 2) = AVG3(K, L, L);
			DST(3, 2) = DST(2, 2) =
			DST(0, 3) = DST(1, 3) = DST(2, 3) = DST(3, 3) = L;
		}

		static void HD4(byte* dst, byte* top) {
			int X = top[-1];
			int I = top[-2];
			int J = top[-3];
			int K = top[-4];
			int L = top[-5];
			int A = top[0];
			int B = top[1];
			int C = top[2];

			DST(0, 0) = DST(2, 1) = AVG2(I, X);
			DST(0, 1) = DST(2, 2) = AVG2(J, I);
			DST(0, 2) = DST(2, 3) = AVG2(K, J);
			DST(0, 3)             = AVG2(L, K);

			DST(3, 0)             = AVG3(A, B, C);
			DST(2, 0)             = AVG3(X, A, B);
			DST(1, 0) = DST(3, 1) = AVG3(I, X, A);
			DST(1, 1) = DST(3, 2) = AVG3(J, I, X);
			DST(1, 2) = DST(3, 3) = AVG3(K, J, I);
			DST(1, 3)             = AVG3(L, K, J);
		}

		static void TM4(byte* dst, byte* top) {
			int x, y;
			byte* clip = clip1 + 255 - top[-1];
			for (y = 0; y < 4; ++y) {
			byte* clip_table = clip + top[-2 - y];
			for (x = 0; x < 4; ++x) {
				dst[x] = clip_table[top[x]];
			}
			dst += BPS;
			}
		}

		#undef DST
		#undef AVG3
		#undef AVG2

		// Left samples are top[-5 .. -2], top_left is top[-1], top are
		// located at top[0..3], and top right is top[4..7]
		static void Intra4Preds(byte* dst, byte* top) {
			DC4(I4DC4 + dst, top);
			TM4(I4TM4 + dst, top);
			VE4(I4VE4 + dst, top);
			HE4(I4HE4 + dst, top);
			RD4(I4RD4 + dst, top);
			VR4(I4VR4 + dst, top);
			LD4(I4LD4 + dst, top);
			VL4(I4VL4 + dst, top);
			HD4(I4HD4 + dst, top);
			HU4(I4HU4 + dst, top);
		}

		//------------------------------------------------------------------------------
		// Metric

		static int GetSSE(byte* a, byte* b,
										int w, int h) {
			int count = 0;
			int y, x;
			for (y = 0; y < h; ++y) {
			for (x = 0; x < w; ++x) {
				int diff = (int)a[x] - b[x];
				count += diff * diff;
			}
			a += BPS;
			b += BPS;
			}
			return count;
		}

		static int SSE16x16(byte* a, byte* b) {
			return GetSSE(a, b, 16, 16);
		}
		static int SSE16x8(byte* a, byte* b) {
			return GetSSE(a, b, 16, 8);
		}
		static int SSE8x8(byte* a, byte* b) {
			return GetSSE(a, b, 8, 8);
		}
		static int SSE4x4(byte* a, byte* b) {
			return GetSSE(a, b, 4, 4);
		}

		//------------------------------------------------------------------------------
		// Texture distortion
		//
		// We try to match the spectral content (weighted) between source and
		// reconstructed samples.

		// Hadamard transform
		// Returns the weighted sum of the absolute value of transformed coefficients.
		static int TTransform(byte* in, ushort* w) {
			int sum = 0;
			int tmp[16];
			int i;
			// horizontal pass
			for (i = 0; i < 4; ++i, in += BPS) {
			int a0 = (in[0] + in[2]) << 2;
			int a1 = (in[1] + in[3]) << 2;
			int a2 = (in[1] - in[3]) << 2;
			int a3 = (in[0] - in[2]) << 2;
			tmp[0 + i * 4] = a0 + a1 + (a0 != 0);
			tmp[1 + i * 4] = a3 + a2;
			tmp[2 + i * 4] = a3 - a2;
			tmp[3 + i * 4] = a0 - a1;
			}
			// vertical pass
			for (i = 0; i < 4; ++i, ++w) {
			int a0 = (tmp[0 + i] + tmp[8 + i]);
			int a1 = (tmp[4 + i] + tmp[12+ i]);
			int a2 = (tmp[4 + i] - tmp[12+ i]);
			int a3 = (tmp[0 + i] - tmp[8 + i]);
			int b0 = a0 + a1;
			int b1 = a3 + a2;
			int b2 = a3 - a2;
			int b3 = a0 - a1;
			// abs((b + (b<0) + 3) >> 3) = (abs(b) + 3) >> 3
			sum += w[ 0] * ((abs(b0) + 3) >> 3);
			sum += w[ 4] * ((abs(b1) + 3) >> 3);
			sum += w[ 8] * ((abs(b2) + 3) >> 3);
			sum += w[12] * ((abs(b3) + 3) >> 3);
			}
			return sum;
		}

		static int Disto4x4(byte* a, byte* b,
							ushort* w) {
			int sum1 = TTransform(a, w);
			int sum2 = TTransform(b, w);
			return (abs(sum2 - sum1) + 8) >> 4;
		}

		static int Disto16x16(byte* a, byte* b,
								ushort* w) {
			int D = 0;
			int x, y;
			for (y = 0; y < 16 * BPS; y += 4 * BPS) {
			for (x = 0; x < 16; x += 4) {
				D += Disto4x4(a + x + y, b + x + y, w);
			}
			}
			return D;
		}

		//------------------------------------------------------------------------------
		// Quantization
		//

		static byte kZigzag[16] = {
			0, 1, 4, 8, 5, 2, 3, 6, 9, 12, 13, 10, 7, 11, 14, 15
		};

		// Simple quantization
		static int QuantizeBlock(short in[16], short out[16],
									int n, VP8Matrix* mtx) {
			int last = -1;
			for (; n < 16; ++n) {
			int j = kZigzag[n];
			int sign = (in[j] < 0);
			int coeff = (sign ? -in[j] : in[j]) + mtx.sharpen_[j];
			if (coeff > 2047) coeff = 2047;
			if (coeff > mtx.zthresh_[j]) {
				int Q = mtx.q_[j];
				int iQ = mtx.iq_[j];
				int B = mtx.bias_[j];
				out[n] = QUANTDIV(coeff, iQ, B);
				if (sign) out[n] = -out[n];
				in[j] = out[n] * Q;
				if (out[n]) last = n;
			} else {
				out[n] = 0;
				in[j] = 0;
			}
			}
			return (last >= 0);
		}

		//------------------------------------------------------------------------------
		// Block copy

		static void Copy(byte* src, byte* dst, int size) {
			int y;
			for (y = 0; y < size; ++y) {
			memcpy(dst, src, size);
			src += BPS;
			dst += BPS;
			}
		}

		static void Copy4x4(byte* src, byte* dst) { Copy(src, dst, 4); }

		//------------------------------------------------------------------------------
		// Initialization

		// Speed-critical function pointers. We have to initialize them to the default
		// implementations within VP8EncDspInit().
		VP8CHisto VP8CollectHistogram;
		VP8Idct VP8ITransform;
		VP8Fdct VP8FTransform;
		VP8WHT VP8ITransformWHT;
		VP8WHT VP8FTransformWHT;
		VP8Intra4Preds VP8EncPredLuma4;
		VP8IntraPreds VP8EncPredLuma16;
		VP8IntraPreds VP8EncPredChroma8;
		VP8Metric VP8SSE16x16;
		VP8Metric VP8SSE8x8;
		VP8Metric VP8SSE16x8;
		VP8Metric VP8SSE4x4;
		VP8WMetric VP8TDisto4x4;
		VP8WMetric VP8TDisto16x16;
		VP8QuantizeBlock VP8EncQuantizeBlock;
		VP8BlockCopy VP8Copy4x4;

		extern void VP8EncDspInitSSE2(void);

		void VP8EncDspInit(void) {
			InitTables();

			// default C implementations
			VP8CollectHistogram = CollectHistogram;
			VP8ITransform = ITransform;
			VP8FTransform = FTransform;
			VP8ITransformWHT = ITransformWHT;
			VP8FTransformWHT = FTransformWHT;
			VP8EncPredLuma4 = Intra4Preds;
			VP8EncPredLuma16 = Intra16Preds;
			VP8EncPredChroma8 = IntraChromaPreds;
			VP8SSE16x16 = SSE16x16;
			VP8SSE8x8 = SSE8x8;
			VP8SSE16x8 = SSE16x8;
			VP8SSE4x4 = SSE4x4;
			VP8TDisto4x4 = Disto4x4;
			VP8TDisto16x16 = Disto16x16;
			VP8EncQuantizeBlock = QuantizeBlock;
			VP8Copy4x4 = Copy4x4;

			// If defined, use CPUInfo() to overwrite some pointers with faster versions.
			if (VP8GetCPUInfo) {
		#if WEBP_USE_SSE2
			if (VP8GetCPUInfo(kSSE2)) {
				VP8EncDspInitSSE2();
			}
		#endif
			}
		}


	}
}
#endif

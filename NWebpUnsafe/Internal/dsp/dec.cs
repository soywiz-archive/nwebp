﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#if false
namespace NWebp.Internal
{
	unsafe class dec
	{
		// run-time tables (~4k)
		static byte[] abs0 = new byte[255 + 255 + 1];     // abs(i)
		static byte[] abs1 = new byte[255 + 255 + 1];     // abs(i)>>1
		static sbyte[] sclip1 = new sbyte[1020 + 1020 + 1];  // clips [-1020, 1020] to [-128, 127]
		static sbyte[] sclip2 = new sbyte[112 + 112 + 1];    // clips [-112, 112] to [-16, 15]
		static byte[] clip1 = new byte[255 + 510 + 1];    // clips [-255,510] to [0,255]

		// We declare this variable 'volatile' to prevent instruction reordering
		// and make sure it's set to true _last_ (so as to be thread-safe)
		// @CHECK!
		static volatile bool tables_ok = false;

		static void DspInitTables() {
		  if (!tables_ok) {
			int i;
			for (i = -255; i <= 255; ++i) {
			  abs0[255 + i] = (byte)((i < 0) ? -i : i);
			  abs1[255 + i] = (byte)(abs0[255 + i] >> 1);
			}
			for (i = -1020; i <= 1020; ++i) {
			  sclip1[1020 + i] = (sbyte)((i < -128) ? -128 : (i > 127) ? 127 : i);
			}
			for (i = -112; i <= 112; ++i) {
			  sclip2[112 + i] = (sbyte)((i < -16) ? -16 : (i > 15) ? 15 : i);
			}
			for (i = -255; i <= 255 + 255; ++i) {
			  clip1[255 + i] = (byte)((i < 0) ? 0 : (i > 255) ? 255 : i);
			}
			tables_ok = true;
		  }
		}

		static byte clip_8b(int v) {
		  return (byte)((0 == (v & ~0xff)) ? v : (v < 0) ? 0 : 255);
		}

		//------------------------------------------------------------------------------
		// Transforms (Paragraph 14.4)

		void STORE(x, y, v) { dst[x + y * BPS] = clip_8b(dst[x + y * BPS] + ((v) >> 3)); }

		static int kC1 = 20091 + (1 << 16);
		static int kC2 = 35468;

		static int MUL(int a, int b) { return (((a) * (b)) >> 16); }

		unsafe static void TransformOne(short* _in, byte* dst) {
		  int[] C = new int[4 * 4];
			fixed (int* CPtr = C)
			{
		int *tmp;
		  int i;
		  tmp = CPtr;
		  for (i = 0; i < 4; ++i) {    // vertical pass
			int a = _in[0] + _in[8];    // [-4096, 4094]
			int b = _in[0] - _in[8];    // [-4095, 4095]
			int c = MUL(_in[4], kC2) - MUL(_in[12], kC1);   // [-3783, 3783]
			int d = MUL(_in[4], kC1) + MUL(_in[12], kC2);   // [-3785, 3781]
			tmp[0] = a + d;   // [-7881, 7875]
			tmp[1] = b + c;   // [-7878, 7878]
			tmp[2] = b - c;   // [-7878, 7878]
			tmp[3] = a - d;   // [-7877, 7879]
			tmp += 4;
			_in++;
		  }
		  // Each pass is expanding the dynamic range by ~3.85 (upper bound).
		  // The exact value is (2. + (kC1 + kC2) / 65536).
		  // After the second pass, maximum interval is [-3794, 3794], assuming
		  // an input in [-2048, 2047] interval. We then need to add a dst value
		  // in the [0, 255] range.
		  // In the worst case scenario, the input to clip_8b() can be as large as
		  // [-60713, 60968].
		  tmp = CPtr;
		  for (i = 0; i < 4; ++i) {    // horizontal pass
			int dc = tmp[0] + 4;
			int a =  dc +  tmp[8];
			int b =  dc -  tmp[8];
			int c = MUL(tmp[4], kC2) - MUL(tmp[12], kC1);
			int d = MUL(tmp[4], kC1) + MUL(tmp[12], kC2);
			STORE(0, 0, a + d);
			STORE(1, 0, b + c);
			STORE(2, 0, b - c);
			STORE(3, 0, a - d);
			tmp++;
			dst += BPS;
		  }
			}
		}
		#undef MUL

		static void TransformTwo(short* _in, byte* dst, bool do_two) {
		  TransformOne(_in, dst);
		  if (do_two) {
			TransformOne(_in + 16, dst + 4);
		  }
		}

		static void TransformUV(short* _in, byte* dst) {
		  VP8Transform(_in + 0 * 16, dst, 1);
		  VP8Transform(_in + 2 * 16, dst + 4 * BPS, 1);
		}

		static void TransformDC(short *_in, byte* dst) {
		  int DC = _in[0] + 4;
		  int i, j;
		  for (j = 0; j < 4; ++j) {
			for (i = 0; i < 4; ++i) {
			  STORE(i, j, DC);
			}
		  }
		}

		static void TransformDCUV(short* _in, byte* dst) {
		  if (_in[0 * 16]) TransformDC(_in + 0 * 16, dst);
		  if (_in[1 * 16]) TransformDC(_in + 1 * 16, dst + 4);
		  if (_in[2 * 16]) TransformDC(_in + 2 * 16, dst + 4 * BPS);
		  if (_in[3 * 16]) TransformDC(_in + 3 * 16, dst + 4 * BPS + 4);
		}

		//------------------------------------------------------------------------------
		// Paragraph 14.3

		static void TransformWHT(short* _in, short* _out) {
		  var tmp = new int[16];
		  int i;
		  for (i = 0; i < 4; ++i) {
			int a0 = _in[0 + i] + _in[12 + i];
			int a1 = _in[4 + i] + _in[ 8 + i];
			int a2 = _in[4 + i] - _in[ 8 + i];
			int a3 = _in[0 + i] - _in[12 + i];
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
			_out[ 0] = (short)((a0 + a1) >> 3);
			_out[16] = (short)((a3 + a2) >> 3);
			_out[32] = (short)((a0 - a1) >> 3);
			_out[48] = (short)((a3 - a2) >> 3);
			_out += 64;
		  }
		}

		//delegate void VP8TransformWHT(short* _in, short* _out) = TransformWHT;

		//------------------------------------------------------------------------------
		// Intra predictions

		static byte DSTi(int x, int y) {
			return (x) + (y) * BPS;
		}

		static byte DST(byte* dst, int x, int y) {
			return dst[DSTi(x, y)];
		}

		static void TrueMotion(byte *dst, int size) {
		  byte* top = dst - BPS;
		  byte* clip0 = clip1 + 255 - top[-1];
		  int y;
		  for (y = 0; y < size; ++y) {
			byte* clip = clip0 + dst[-1];
			int x;
			for (x = 0; x < size; ++x) {
			  dst[x] = clip[top[x]];
			}
			dst += BPS;
		  }
		}
		static void TM4(byte *dst)   { TrueMotion(dst, 4); }
		static void TM8uv(byte *dst) { TrueMotion(dst, 8); }
		static void TM16(byte *dst)  { TrueMotion(dst, 16); }

		//------------------------------------------------------------------------------
		// 16x16

		static void VE16(byte *dst) {     // vertical
		  int j;
		  for (j = 0; j < 16; ++j) {
			Global.memcpy(dst + j * BPS, dst - BPS, 16);
		  }
		}

		static void HE16(byte *dst) {     // horizontal
		  int j;
		  for (j = 16; j > 0; --j) {
			Global.memset(dst, dst[-1], 16);
			dst += BPS;
		  }
		}

		static void Put16(int v, byte* dst) {
		  int j;
		  for (j = 0; j < 16; ++j) {
			Global.memset(dst + j * BPS, v, 16);
		  }
		}

		static void DC16(byte *dst) {    // DC
		  int DC = 16;
		  int j;
		  for (j = 0; j < 16; ++j) {
			DC += dst[-1 + j * BPS] + dst[j - BPS];
		  }
		  Put16(DC >> 5, dst);
		}

		static void DC16NoTop(byte *dst) {   // DC with top samples not available
		  int DC = 8;
		  int j;
		  for (j = 0; j < 16; ++j) {
			DC += dst[-1 + j * BPS];
		  }
		  Put16(DC >> 4, dst);
		}

		static void DC16NoLeft(byte *dst) {  // DC with left samples not available
		  int DC = 8;
		  int i;
		  for (i = 0; i < 16; ++i) {
			DC += dst[i - BPS];
		  }
		  Put16(DC >> 4, dst);
		}

		static void DC16NoTopLeft(byte *dst) {  // DC with no top and left samples
		  Put16(0x80, dst);
		}

		//------------------------------------------------------------------------------
		// 4x4

		static int AVG3(int a, int b, int c) {
			return (((a) + 2 * (b) + (c) + 2) >> 2);
		}
		static int AVG2(int a, int b) {
			return (((a) + (b) + 1) >> 1);
		}

		static void VE4(byte *dst) {    // vertical
		  byte* top = dst - BPS;
		  var vals = new byte[4] {
			AVG3(top[-1], top[0], top[1]),
			AVG3(top[ 0], top[1], top[2]),
			AVG3(top[ 1], top[2], top[3]),
			AVG3(top[ 2], top[3], top[4])
		  };
		  int i;
		  for (i = 0; i < 4; ++i) {
			Global.memcpy(dst + i * BPS, vals, sizeof(vals));
		  }
		}

		static void HE4(byte *dst) {    // horizontal
		  int A = dst[-1 - BPS];
		  int B = dst[-1];
		  int C = dst[-1 + BPS];
		  int D = dst[-1 + 2 * BPS];
		  int E = dst[-1 + 3 * BPS];
		  *(uint*)(dst + 0 * BPS) = 0x01010101U * AVG3(A, B, C);
		  *(uint*)(dst + 1 * BPS) = 0x01010101U * AVG3(B, C, D);
		  *(uint*)(dst + 2 * BPS) = 0x01010101U * AVG3(C, D, E);
		  *(uint*)(dst + 3 * BPS) = 0x01010101U * AVG3(D, E, E);
		}

		static void DC4(byte *dst) {   // DC
		  uint dc = 4;
		  int i;
		  for (i = 0; i < 4; ++i) dc += dst[i - BPS] + dst[-1 + i * BPS];
		  dc >>= 3;
		  for (i = 0; i < 4; ++i) memset(dst + i * BPS, dc, 4);
		}

		static void RD4(byte *dst) {   // Down-right
		  int I = dst[-1 + 0 * BPS];
		  int J = dst[-1 + 1 * BPS];
		  int K = dst[-1 + 2 * BPS];
		  int L = dst[-1 + 3 * BPS];
		  int X = dst[-1 - BPS];
		  int A = dst[0 - BPS];
		  int B = dst[1 - BPS];
		  int C = dst[2 - BPS];
		  int D = dst[3 - BPS];
		  dst[DSTi(0, 3)]                                     = (byte)AVG3(J, K, L);
		  dst[DSTi(0, 2)] = dst[DSTi(1, 3)]                         = (byte)AVG3(I, J, K);
		  dst[DSTi(0, 1)] = dst[DSTi(1, 2)] = dst[DSTi(2, 3)]             = (byte)AVG3(X, I, J);
		  dst[DSTi(0, 0)] = dst[DSTi(1, 1)] = dst[DSTi(2, 2)] = dst[DSTi(3, 3)) = (byte)AVG3(A, X, I);
		  dst[DSTi(1, 0)] = dst[DSTi(2, 1)] = dst[DSTi(3, 2)]             = (byte)AVG3(B, A, X);
		  dst[DSTi(2, 0)] = dst[DSTi(3, 1)]                         = (byte)AVG3(C, B, A);
		  dst[DSTi(3, 0)]                                     = (byte)AVG3(D, C, B);
		}

		static void LD4(byte *dst) {   // Down-Left
		  int A = dst[0 - BPS];
		  int B = dst[1 - BPS];
		  int C = dst[2 - BPS];
		  int D = dst[3 - BPS];
		  int E = dst[4 - BPS];
		  int F = dst[5 - BPS];
		  int G = dst[6 - BPS];
		  int H = dst[7 - BPS];
		  dst[DSTi(0, 0)]                                     = (byte)AVG3(A, B, C);
		  dst[DSTi(1, 0)] = dst[DSTi(0, 1)]                         = (byte)AVG3(B, C, D);
		  dst[DSTi(2, 0)] = dst[DSTi(1, 1)] = dst[DSTi(0, 2)]             = (byte)AVG3(C, D, E);
		  dst[DSTi(3, 0)] = dst[DSTi(2, 1)] = dst[DSTi(1, 2)] = dst[DSTi(0, 3)] = (byte)AVG3(D, E, F);
		  dst[DSTi(3, 1)] = dst[DSTi(2, 2)] = dst[DSTi(1, 3)]             = (byte)AVG3(E, F, G);
		  dst[DSTi(3, 2)] = dst[DSTi(2, 3)]                         = (byte)AVG3(F, G, H);
		  dst[DSTi(3, 3)]                                     = (byte)AVG3(G, H, H);
		}

		static void VR4(byte *dst) {   // Vertical-Right
		  int I = dst[-1 + 0 * BPS];
		  int J = dst[-1 + 1 * BPS];
		  int K = dst[-1 + 2 * BPS];
		  int X = dst[-1 - BPS];
		  int A = dst[0 - BPS];
		  int B = dst[1 - BPS];
		  int C = dst[2 - BPS];
		  int D = dst[3 - BPS];
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

		static void VL4(byte *dst) {   // Vertical-Left
		  int A = dst[0 - BPS];
		  int B = dst[1 - BPS];
		  int C = dst[2 - BPS];
		  int D = dst[3 - BPS];
		  int E = dst[4 - BPS];
		  int F = dst[5 - BPS];
		  int G = dst[6 - BPS];
		  int H = dst[7 - BPS];
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

		static void HU4(byte *dst) {   // Horizontal-Up
		  int I = dst[-1 + 0 * BPS];
		  int J = dst[-1 + 1 * BPS];
		  int K = dst[-1 + 2 * BPS];
		  int L = dst[-1 + 3 * BPS];
		  DST(0, 0) =             AVG2(I, J);
		  DST(2, 0) = DST(0, 1) = AVG2(J, K);
		  DST(2, 1) = DST(0, 2) = AVG2(K, L);
		  DST(1, 0) =             AVG3(I, J, K);
		  DST(3, 0) = DST(1, 1) = AVG3(J, K, L);
		  DST(3, 1) = DST(1, 2) = AVG3(K, L, L);
		  DST(3, 2) = DST(2, 2) =
			DST(0, 3) = DST(1, 3) = DST(2, 3) = DST(3, 3) = L;
		}

		static void HD4(byte *dst) {  // Horizontal-Down
		  int I = dst[-1 + 0 * BPS];
		  int J = dst[-1 + 1 * BPS];
		  int K = dst[-1 + 2 * BPS];
		  int L = dst[-1 + 3 * BPS];
		  int X = dst[-1 - BPS];
		  int A = dst[0 - BPS];
		  int B = dst[1 - BPS];
		  int C = dst[2 - BPS];

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

		//------------------------------------------------------------------------------
		// Chroma

		static void VE8uv(byte *dst) {    // vertical
		  int j;
		  for (j = 0; j < 8; ++j) {
			Global.memcpy(dst + j * BPS, dst - BPS, 8);
		  }
		}

		static void HE8uv(byte *dst) {    // horizontal
		  int j;
		  for (j = 0; j < 8; ++j) {
			Global.memset(dst, dst[-1], 8);
			dst += BPS;
		  }
		}

		// helper for chroma-DC predictions
		static void Put8x8uv(ulong v, byte* dst) {
		  int j;
		  for (j = 0; j < 8; ++j) {
			*(ulong*)(dst + j * BPS) = v;
		  }
		}

		static void DC8uv(byte *dst) {     // DC
		  int dc0 = 8;
		  int i;
		  for (i = 0; i < 8; ++i) {
			dc0 += dst[i - BPS] + dst[-1 + i * BPS];
		  }
		  Put8x8uv((ulong)((dc0 >> 4) * 0x0101010101010101UL), dst);
		}

		static void DC8uvNoLeft(byte *dst) {   // DC with no left samples
		  int dc0 = 4;
		  int i;
		  for (i = 0; i < 8; ++i) {
			dc0 += dst[i - BPS];
		  }
		  Put8x8uv((ulong)((dc0 >> 3) * 0x0101010101010101UL), dst);
		}

		static void DC8uvNoTop(byte *dst) {  // DC with no top samples
		  int dc0 = 4;
		  int i;
		  for (i = 0; i < 8; ++i) {
			dc0 += dst[-1 + i * BPS];
		  }
		  Put8x8uv((ulong)((dc0 >> 3) * 0x0101010101010101UL), dst);
		}

		static void DC8uvNoTopLeft(byte *dst) {    // DC with nothing
		  Put8x8uv(0x8080808080808080UL, dst);
		}

		//------------------------------------------------------------------------------
		// default C implementations

		VP8PredFunc VP8PredLuma4[NUM_BMODES] = {
		  DC4, TM4, VE4, HE4, RD4, VR4, LD4, VL4, HD4, HU4
		};

		VP8PredFunc VP8PredLuma16[NUM_B_DC_MODES] = {
		  DC16, TM16, VE16, HE16,
		  DC16NoTop, DC16NoLeft, DC16NoTopLeft
		};

		VP8PredFunc VP8PredChroma8[NUM_B_DC_MODES] = {
		  DC8uv, TM8uv, VE8uv, HE8uv,
		  DC8uvNoTop, DC8uvNoLeft, DC8uvNoTopLeft
		};

		//------------------------------------------------------------------------------
		// Edge filtering functions

		// 4 pixels in, 2 pixels out
		static void do_filter2(byte* p, int step) {
		  int p1 = p[-2*step], p0 = p[-step], q0 = p[0], q1 = p[step];
		  int a = 3 * (q0 - p0) + sclip1[1020 + p1 - q1];
		  int a1 = sclip2[112 + ((a + 4) >> 3)];
		  int a2 = sclip2[112 + ((a + 3) >> 3)];
		  p[-step] = clip1[255 + p0 + a2];
		  p[    0] = clip1[255 + q0 - a1];
		}

		// 4 pixels in, 4 pixels out
		static void do_filter4(byte* p, int step) {
		  int p1 = p[-2*step], p0 = p[-step], q0 = p[0], q1 = p[step];
		  int a = 3 * (q0 - p0);
		  int a1 = sclip2[112 + ((a + 4) >> 3)];
		  int a2 = sclip2[112 + ((a + 3) >> 3)];
		  int a3 = (a1 + 1) >> 1;
		  p[-2*step] = clip1[255 + p1 + a3];
		  p[-  step] = clip1[255 + p0 + a2];
		  p[      0] = clip1[255 + q0 - a1];
		  p[   step] = clip1[255 + q1 - a3];
		}

		// 6 pixels in, 6 pixels out
		static void do_filter6(byte* p, int step) {
		  int p2 = p[-3*step], p1 = p[-2*step], p0 = p[-step];
		  int q0 = p[0], q1 = p[step], q2 = p[2*step];
		  int a = sclip1[1020 + 3 * (q0 - p0) + sclip1[1020 + p1 - q1]];
		  int a1 = (27 * a + 63) >> 7;  // eq. to ((3 * a + 7) * 9) >> 7
		  int a2 = (18 * a + 63) >> 7;  // eq. to ((2 * a + 7) * 9) >> 7
		  int a3 = (9  * a + 63) >> 7;  // eq. to ((1 * a + 7) * 9) >> 7
		  p[-3*step] = clip1[255 + p2 + a3];
		  p[-2*step] = clip1[255 + p1 + a2];
		  p[-  step] = clip1[255 + p0 + a1];
		  p[      0] = clip1[255 + q0 - a1];
		  p[   step] = clip1[255 + q1 - a2];
		  p[ 2*step] = clip1[255 + q2 - a3];
		}

		static int hev(byte* p, int step, int thresh) {
		  int p1 = p[-2*step], p0 = p[-step], q0 = p[0], q1 = p[step];
		  return (abs0[255 + p1 - p0] > thresh) || (abs0[255 + q1 - q0] > thresh);
		}

		static int needs_filter(byte* p, int step, int thresh) {
		  int p1 = p[-2*step], p0 = p[-step], q0 = p[0], q1 = p[step];
		  return (2 * abs0[255 + p0 - q0] + abs1[255 + p1 - q1]) <= thresh;
		}

		static int needs_filter2(byte* p,
											 int step, int t, int it) {
		  int p3 = p[-4*step], p2 = p[-3*step], p1 = p[-2*step], p0 = p[-step];
		  int q0 = p[0], q1 = p[step], q2 = p[2*step], q3 = p[3*step];
		  if ((2 * abs0[255 + p0 - q0] + abs1[255 + p1 - q1]) > t)
			return 0;
		  return abs0[255 + p3 - p2] <= it && abs0[255 + p2 - p1] <= it &&
				 abs0[255 + p1 - p0] <= it && abs0[255 + q3 - q2] <= it &&
				 abs0[255 + q2 - q1] <= it && abs0[255 + q1 - q0] <= it;
		}

		//------------------------------------------------------------------------------
		// Simple In-loop filtering (Paragraph 15.2)

		static void SimpleVFilter16(byte* p, int stride, int thresh) {
		  int i;
		  for (i = 0; i < 16; ++i) {
			if (needs_filter(p + i, stride, thresh)) {
			  do_filter2(p + i, stride);
			}
		  }
		}

		static void SimpleHFilter16(byte* p, int stride, int thresh) {
		  int i;
		  for (i = 0; i < 16; ++i) {
			if (needs_filter(p + i * stride, 1, thresh)) {
			  do_filter2(p + i * stride, 1);
			}
		  }
		}

		static void SimpleVFilter16i(byte* p, int stride, int thresh) {
		  int k;
		  for (k = 3; k > 0; --k) {
			p += 4 * stride;
			SimpleVFilter16(p, stride, thresh);
		  }
		}

		static void SimpleHFilter16i(byte* p, int stride, int thresh) {
		  int k;
		  for (k = 3; k > 0; --k) {
			p += 4;
			SimpleHFilter16(p, stride, thresh);
		  }
		}

		//------------------------------------------------------------------------------
		// Complex In-loop filtering (Paragraph 15.3)

		static void FilterLoop26(byte* p,
											 int hstride, int vstride, int size,
											 int thresh, int ithresh, int hev_thresh) {
		  while (size-- > 0) {
			if (needs_filter2(p, hstride, thresh, ithresh)) {
			  if (hev(p, hstride, hev_thresh)) {
				do_filter2(p, hstride);
			  } else {
				do_filter6(p, hstride);
			  }
			}
			p += vstride;
		  }
		}

		static void FilterLoop24(byte* p,
											 int hstride, int vstride, int size,
											 int thresh, int ithresh, int hev_thresh) {
		  while (size-- > 0) {
			if (needs_filter2(p, hstride, thresh, ithresh)) {
			  if (hev(p, hstride, hev_thresh)) {
				do_filter2(p, hstride);
			  } else {
				do_filter4(p, hstride);
			  }
			}
			p += vstride;
		  }
		}

		// on macroblock edges
		static void VFilter16(byte* p, int stride,
							  int thresh, int ithresh, int hev_thresh) {
		  FilterLoop26(p, stride, 1, 16, thresh, ithresh, hev_thresh);
		}

		static void HFilter16(byte* p, int stride,
							  int thresh, int ithresh, int hev_thresh) {
		  FilterLoop26(p, 1, stride, 16, thresh, ithresh, hev_thresh);
		}

		// on three inner edges
		static void VFilter16i(byte* p, int stride,
							   int thresh, int ithresh, int hev_thresh) {
		  int k;
		  for (k = 3; k > 0; --k) {
			p += 4 * stride;
			FilterLoop24(p, stride, 1, 16, thresh, ithresh, hev_thresh);
		  }
		}

		static void HFilter16i(byte* p, int stride,
							   int thresh, int ithresh, int hev_thresh) {
		  int k;
		  for (k = 3; k > 0; --k) {
			p += 4;
			FilterLoop24(p, 1, stride, 16, thresh, ithresh, hev_thresh);
		  }
		}

		// 8-pixels wide variant, for chroma filtering
		static void VFilter8(byte* u, byte* v, int stride,
							 int thresh, int ithresh, int hev_thresh) {
		  FilterLoop26(u, stride, 1, 8, thresh, ithresh, hev_thresh);
		  FilterLoop26(v, stride, 1, 8, thresh, ithresh, hev_thresh);
		}

		static void HFilter8(byte* u, byte* v, int stride,
							 int thresh, int ithresh, int hev_thresh) {
		  FilterLoop26(u, 1, stride, 8, thresh, ithresh, hev_thresh);
		  FilterLoop26(v, 1, stride, 8, thresh, ithresh, hev_thresh);
		}

		static void VFilter8i(byte* u, byte* v, int stride,
							  int thresh, int ithresh, int hev_thresh) {
		  FilterLoop24(u + 4 * stride, stride, 1, 8, thresh, ithresh, hev_thresh);
		  FilterLoop24(v + 4 * stride, stride, 1, 8, thresh, ithresh, hev_thresh);
		}

		static void HFilter8i(byte* u, byte* v, int stride,
							  int thresh, int ithresh, int hev_thresh) {
		  FilterLoop24(u + 4, 1, stride, 8, thresh, ithresh, hev_thresh);
		  FilterLoop24(v + 4, 1, stride, 8, thresh, ithresh, hev_thresh);
		}

		//------------------------------------------------------------------------------

		VP8DecIdct2 VP8Transform;
		VP8DecIdct VP8TransformUV;
		VP8DecIdct VP8TransformDC;
		VP8DecIdct VP8TransformDCUV;

		VP8LumaFilterFunc VP8VFilter16;
		VP8LumaFilterFunc VP8HFilter16;
		VP8ChromaFilterFunc VP8VFilter8;
		VP8ChromaFilterFunc VP8HFilter8;
		VP8LumaFilterFunc VP8VFilter16i;
		VP8LumaFilterFunc VP8HFilter16i;
		VP8ChromaFilterFunc VP8VFilter8i;
		VP8ChromaFilterFunc VP8HFilter8i;
		VP8SimpleFilterFunc VP8SimpleVFilter16;
		VP8SimpleFilterFunc VP8SimpleHFilter16;
		VP8SimpleFilterFunc VP8SimpleVFilter16i;
		VP8SimpleFilterFunc VP8SimpleHFilter16i;

		extern void VP8DspInitSSE2(void);
		extern void VP8DspInitNEON(void);

		void VP8DspInit(void) {
		  DspInitTables();

		  VP8Transform = TransformTwo;
		  VP8TransformUV = TransformUV;
		  VP8TransformDC = TransformDC;
		  VP8TransformDCUV = TransformDCUV;

		  VP8VFilter16 = VFilter16;
		  VP8HFilter16 = HFilter16;
		  VP8VFilter8 = VFilter8;
		  VP8HFilter8 = HFilter8;
		  VP8VFilter16i = VFilter16i;
		  VP8HFilter16i = HFilter16i;
		  VP8VFilter8i = VFilter8i;
		  VP8HFilter8i = HFilter8i;
		  VP8SimpleVFilter16 = SimpleVFilter16;
		  VP8SimpleHFilter16 = SimpleHFilter16;
		  VP8SimpleVFilter16i = SimpleVFilter16i;
		  VP8SimpleHFilter16i = SimpleHFilter16i;

		  // If defined, use CPUInfo() to overwrite some pointers with faster versions.
		  if (VP8GetCPUInfo) {
		#if WEBP_USE_SSE2
			if (VP8GetCPUInfo(kSSE2)) {
			  VP8DspInitSSE2();
			}
		#elif __GNUC__ARM_NEON__
			if (VP8GetCPUInfo(kNEON)) {
			  VP8DspInitNEON();
			}
		#endif
		  }
		}


	}
}
#endif

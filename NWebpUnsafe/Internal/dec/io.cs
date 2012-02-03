using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#if false
namespace NWebp.Internal
{
	public partial class Internal
	{
		//------------------------------------------------------------------------------
		// Main YUV<.RGB conversion functions

		static int EmitYUV(VP8Io* io, WebPDecParams* p) {
		  WebPDecBuffer* output = p.output;
		  WebPYUVABuffer* buf = &output.u.YUVA;
		  byte* y_dst = buf.y + io.mb_y * buf.y_stride;
		  byte* u_dst = buf.u + (io.mb_y >> 1) * buf.u_stride;
		  byte* v_dst = buf.v + (io.mb_y >> 1) * buf.v_stride;
		  int mb_w = io.mb_w;
		  int mb_h = io.mb_h;
		  int uv_w = (mb_w + 1) / 2;
		  int uv_h = (mb_h + 1) / 2;
		  int j;
		  for (j = 0; j < mb_h; ++j) {
			memcpy(y_dst + j * buf.y_stride, io.y + j * io.y_stride, mb_w);
		  }
		  for (j = 0; j < uv_h; ++j) {
			memcpy(u_dst + j * buf.u_stride, io.u + j * io.uv_stride, uv_w);
			memcpy(v_dst + j * buf.v_stride, io.v + j * io.uv_stride, uv_w);
		  }
		  return io.mb_h;
		}

		// Point-sampling U/V sampler.
		static int EmitSampledRGB(VP8Io* io, WebPDecParams* p) {
		  WebPDecBuffer* output = p.output;
		  WebPRGBABuffer* buf = &output.u.RGBA;
		  byte* dst = buf.rgba + io.mb_y * buf.stride;
		  byte* y_src = io.y;
		  byte* u_src = io.u;
		  byte* v_src = io.v;
		  WebPSampleLinePairFunc sample =
			  io.a ? WebPSamplersKeepAlpha[output.colorspace]
					: WebPSamplers[output.colorspace];
		  int mb_w = io.mb_w;
		  int last = io.mb_h - 1;
		  int j;
		  for (j = 0; j < last; j += 2) {
			sample(y_src, y_src + io.y_stride, u_src, v_src,
				   dst, dst + buf.stride, mb_w);
			y_src += 2 * io.y_stride;
			u_src += io.uv_stride;
			v_src += io.uv_stride;
			dst += 2 * buf.stride;
		  }
		  if (j == last) {  // Just do the last line twice
			sample(y_src, y_src, u_src, v_src, dst, dst, mb_w);
		  }
		  return io.mb_h;
		}

		//------------------------------------------------------------------------------
		// YUV444 . RGB conversion

		#if false   // TODO(skal): this is for future rescaling.
		static int EmitRGB(VP8Io* io, WebPDecParams* p) {
		  WebPDecBuffer* output = p.output;
		  WebPRGBABuffer* buf = &output.u.RGBA;
		  byte* dst = buf.rgba + io.mb_y * buf.stride;
		  byte* y_src = io.y;
		  byte* u_src = io.u;
		  byte* v_src = io.v;
		  WebPYUV444Converter convert = WebPYUV444Converters[output.colorspace];
		  int mb_w = io.mb_w;
		  int last = io.mb_h;
		  int j;
		  for (j = 0; j < last; ++j) {
			convert(y_src, u_src, v_src, dst, mb_w);
			y_src += io.y_stride;
			u_src += io.uv_stride;
			v_src += io.uv_stride;
			dst += buf.stride;
		  }
		  return io.mb_h;
		}
		#endif

		//------------------------------------------------------------------------------
		// Fancy upsampling

		#if FANCY_UPSAMPLING
		static int EmitFancyRGB(VP8Io* io, WebPDecParams* p) {
		  int num_lines_out = io.mb_h;   // a priori guess
		  WebPRGBABuffer* buf = &p.output.u.RGBA;
		  byte* dst = buf.rgba + io.mb_y * buf.stride;
		  WebPUpsampleLinePairFunc upsample =
			  io.a ? WebPUpsamplersKeepAlpha[p.output.colorspace]
					: WebPUpsamplers[p.output.colorspace];
		  byte* cur_y = io.y;
		  byte* cur_u = io.u;
		  byte* cur_v = io.v;
		  byte* top_u = p.tmp_u;
		  byte* top_v = p.tmp_v;
		  int y = io.mb_y;
		  int y_end = io.mb_y + io.mb_h;
		  int mb_w = io.mb_w;
		  int uv_w = (mb_w + 1) / 2;

		  if (y == 0) {
			// First line is special cased. We mirror the u/v samples at boundary.
			upsample(null, cur_y, cur_u, cur_v, cur_u, cur_v, null, dst, mb_w);
		  } else {
			// We can finish the left-over line from previous call.
			// Warning! Don't overwrite the alpha values (if any), as they
			// are not lagging one line behind but are already written.
			upsample(p.tmp_y, cur_y, top_u, top_v, cur_u, cur_v,
					 dst - buf.stride, dst, mb_w);
			++num_lines_out;
		  }
		  // Loop over each output pairs of row.
		  for (; y + 2 < y_end; y += 2) {
			top_u = cur_u;
			top_v = cur_v;
			cur_u += io.uv_stride;
			cur_v += io.uv_stride;
			dst += 2 * buf.stride;
			cur_y += 2 * io.y_stride;
			upsample(cur_y - io.y_stride, cur_y,
					 top_u, top_v, cur_u, cur_v,
					 dst - buf.stride, dst, mb_w);
		  }
		  // move to last row
		  cur_y += io.y_stride;
		  if (io.crop_top + y_end < io.crop_bottom) {
			// Save the unfinished samples for next call (as we're not done yet).
			memcpy(p.tmp_y, cur_y, mb_w * sizeof(*p.tmp_y));
			memcpy(p.tmp_u, cur_u, uv_w * sizeof(*p.tmp_u));
			memcpy(p.tmp_v, cur_v, uv_w * sizeof(*p.tmp_v));
			// The fancy upsampler leaves a row unfinished behind
			// (except for the very last row)
			num_lines_out--;
		  } else {
			// Process the very last row of even-sized picture
			if (!(y_end & 1)) {
			  upsample(cur_y, null, cur_u, cur_v, cur_u, cur_v,
					  dst + buf.stride, null, mb_w);
			}
		  }
		  return num_lines_out;
		}

		#endif    
		// FANCY_UPSAMPLING 

		//------------------------------------------------------------------------------

		static int EmitAlphaYUV(VP8Io* io, WebPDecParams* p) {
		  byte* alpha = io.a;
		  if (alpha != null) {
			int j;
			int mb_w = io.mb_w;
			int mb_h = io.mb_h;
			WebPYUVABuffer* buf = &p.output.u.YUVA;
			byte* dst = buf.a + io.mb_y * buf.a_stride;
			for (j = 0; j < mb_h; ++j) {
			  memcpy(dst, alpha, mb_w * sizeof(*dst));
			  alpha += io.width;
			  dst += buf.a_stride;
			}
		  }
		  return 0;
		}

		static int EmitAlphaRGB(VP8Io* io, WebPDecParams* p) {
		  int mb_w = io.mb_w;
		  int mb_h = io.mb_h;
		  int i, j;
		  WebPRGBABuffer* buf = &p.output.u.RGBA;
		  byte* dst = buf.rgba + io.mb_y * buf.stride +
						 (p.output.colorspace == MODE_ARGB ? 0 : 3);
		  byte* alpha = io.a;
		  if (alpha) {
			for (j = 0; j < mb_h; ++j) {
			  for (i = 0; i < mb_w; ++i) {
				dst[4 * i] = alpha[i];
			  }
			  alpha += io.width;
			  dst += buf.stride;
			}
		  }
		  return 0;
		}

		static uint clip(uint v, uint max_value) {
		  return (v > max_value) ? max_value : v;
		}

		static int EmitAlphaRGBA4444(VP8Io* io, WebPDecParams* p) {
		  int mb_w = io.mb_w;
		  int mb_h = io.mb_h;
		  int i, j;
		  WebPRGBABuffer* buf = &p.output.u.RGBA;
		  byte* dst = buf.rgba + io.mb_y * buf.stride + 1;
		  byte* alpha = io.a;
		  if (alpha) {
			for (j = 0; j < mb_h; ++j) {
			  for (i = 0; i < mb_w; ++i) {
				// Fill in the alpha value (converted to 4 bits).
				uint alpha_val = clip((alpha[i] + 8) >> 4, 15);
				dst[2 * i] = (dst[2 * i] & 0xf0) | alpha_val;
			  }
			  alpha += io.width;
			  dst += buf.stride;
			}
		  }
		  return 0;
		}

		//------------------------------------------------------------------------------
		// Simple picture rescaler

		// TODO(skal): start a common library for encoder and decoder, and factorize
		// this code in.

		const int RFIX = 30;
		void MULT(x,y) { return (((long)(x) * (y) + (1 << (RFIX - 1))) >> RFIX); }

		static void InitRescaler(WebPRescaler* wrk,
								 int src_width, int src_height,
								 byte* dst,
								 int dst_width, int dst_height, int dst_stride,
								 int x_add, int x_sub, int y_add, int y_sub,
								 int* work) {
		  wrk.x_expand = (src_width < dst_width);
		  wrk.src_width = src_width;
		  wrk.src_height = src_height;
		  wrk.dst_width = dst_width;
		  wrk.dst_height = dst_height;
		  wrk.dst = dst;
		  wrk.dst_stride = dst_stride;
		  // for 'x_expand', we use bilinear interpolation
		  wrk.x_add = wrk.x_expand ? (x_sub - 1) : x_add - x_sub;
		  wrk.x_sub = wrk.x_expand ? (x_add - 1) : x_sub;
		  wrk.y_accum = y_add;
		  wrk.y_add = y_add;
		  wrk.y_sub = y_sub;
		  wrk.fx_scale = (1 << RFIX) / x_sub;
		  wrk.fy_scale = (1 << RFIX) / y_sub;
		  wrk.fxy_scale = wrk.x_expand ?
			  ((long)dst_height << RFIX) / (x_sub * src_height) :
			  ((long)dst_height << RFIX) / (x_add * src_height);
		  wrk.irow = work;
		  wrk.frow = work + dst_width;
		}

		static void ImportRow(byte* src,
										  WebPRescaler* wrk) {
		  int x_in = 0;
		  int x_out;
		  int accum = 0;
		  if (!wrk.x_expand) {
			int sum = 0;
			for (x_out = 0; x_out < wrk.dst_width; ++x_out) {
			  accum += wrk.x_add;
			  for (; accum > 0; accum -= wrk.x_sub) {
				sum += src[x_in++];
			  }
			  {        // Emit next horizontal pixel.
				int base = src[x_in++];
				int frac = base * (-accum);
				wrk.frow[x_out] = (sum + base) * wrk.x_sub - frac;
				// fresh fractional start for next pixel
				sum = MULT(frac, wrk.fx_scale);
			  }
			}
		  } else {        // simple bilinear interpolation
			int left = src[0], right = src[0];
			for (x_out = 0; x_out < wrk.dst_width; ++x_out) {
			  if (accum < 0) {
				left = right;
				right = src[++x_in];
				accum += wrk.x_add;
			  }
			  wrk.frow[x_out] = right * wrk.x_add + (left - right) * accum;
			  accum -= wrk.x_sub;
			}
		  }
		  // Accumulate the new row's contribution
		  for (x_out = 0; x_out < wrk.dst_width; ++x_out) {
			wrk.irow[x_out] += wrk.frow[x_out];
		  }
		}

		static void ExportRow(WebPRescaler* wrk) {
		  int x_out;
		  int yscale = wrk.fy_scale * (-wrk.y_accum);
		  assert(wrk.y_accum <= 0);
		  for (x_out = 0; x_out < wrk.dst_width; ++x_out) {
			int frac = MULT(wrk.frow[x_out], yscale);
			int v = (int)MULT(wrk.irow[x_out] - frac, wrk.fxy_scale);
			wrk.dst[x_out] = (!(v & ~0xff)) ? v : (v < 0) ? 0 : 255;
			wrk.irow[x_out] = frac;   // new fractional start
		  }
		  wrk.y_accum += wrk.y_add;
		  wrk.dst += wrk.dst_stride;
		}

		#undef MULT
		#undef RFIX

		//------------------------------------------------------------------------------
		// YUV rescaling (no final RGB conversion needed)

		static int Rescale(byte* src, int src_stride,
						   int new_lines, WebPRescaler* wrk) {
		  int num_lines_out = 0;
		  while (new_lines-- > 0) {    // import new contribution of one source row.
			ImportRow(src, wrk);
			src += src_stride;
			wrk.y_accum -= wrk.y_sub;
			while (wrk.y_accum <= 0) {      // emit output row(s)
			  ExportRow(wrk);
			  ++num_lines_out;
			}
		  }
		  return num_lines_out;
		}

		static int EmitRescaledYUV(VP8Io* io, WebPDecParams* p) {
		  int mb_h = io.mb_h;
		  int uv_mb_h = (mb_h + 1) >> 1;
		  int num_lines_out = Rescale(io.y, io.y_stride, mb_h, &p.scaler_y);
		  Rescale(io.u, io.uv_stride, uv_mb_h, &p.scaler_u);
		  Rescale(io.v, io.uv_stride, uv_mb_h, &p.scaler_v);
		  return num_lines_out;
		}

		static int EmitRescaledAlphaYUV(VP8Io* io, WebPDecParams* p) {
		  if (io.a != null) {
			Rescale(io.a, io.width, io.mb_h, &p.scaler_a);
		  }
		  return 0;
		}

		static int IsAlphaMode(WEBP_CSP_MODE mode) {
		  return (mode == MODE_RGBA || mode == MODE_BGRA || mode == MODE_ARGB ||
				  mode == MODE_RGBA_4444 || mode == MODE_YUVA);
		}

		static int InitYUVRescaler(VP8Io* io, WebPDecParams* p) {
		  int has_alpha = IsAlphaMode(p.output.colorspace);
		  WebPYUVABuffer* buf = &p.output.u.YUVA;
		  int out_width  = io.scaled_width;
		  int out_height = io.scaled_height;
		  int uv_out_width  = (out_width + 1) >> 1;
		  int uv_out_height = (out_height + 1) >> 1;
		  int uv_in_width  = (io.mb_w + 1) >> 1;
		  int uv_in_height = (io.mb_h + 1) >> 1;
		  uint work_size = 2 * out_width;   // scratch memory for luma rescaler
		  uint uv_work_size = 2 * uv_out_width;  // and for each u/v ones
		  uint tmp_size;
		  int* work;

		  tmp_size = work_size + 2 * uv_work_size;
		  if (has_alpha) {
			tmp_size += work_size;
		  }
		  p.memory = calloc(1, tmp_size * sizeof(*work));
		  if (p.memory == null) {
			return 0;   // memory error
		  }
		  work = (int*)p.memory;
		  InitRescaler(&p.scaler_y, io.mb_w, io.mb_h,
					   buf.y, out_width, out_height, buf.y_stride,
					   io.mb_w, out_width, io.mb_h, out_height,
					   work);
		  InitRescaler(&p.scaler_u, uv_in_width, uv_in_height,
					   buf.u, uv_out_width, uv_out_height, buf.u_stride,
					   uv_in_width, uv_out_width,
					   uv_in_height, uv_out_height,
					   work + work_size);
		  InitRescaler(&p.scaler_v, uv_in_width, uv_in_height,
					   buf.v, uv_out_width, uv_out_height, buf.v_stride,
					   uv_in_width, uv_out_width,
					   uv_in_height, uv_out_height,
					   work + work_size + uv_work_size);
		  p.emit = EmitRescaledYUV;
		  if (has_alpha) {
			InitRescaler(&p.scaler_a, io.mb_w, io.mb_h,
						 buf.a, out_width, out_height, buf.a_stride,
						 io.mb_w, out_width, io.mb_h, out_height,
						 work + work_size + 2 * uv_work_size);
			p.emit_alpha = EmitRescaledAlphaYUV;
		  }
		  return 1;
		}

		//------------------------------------------------------------------------------
		// RGBA rescaling

		// import new contributions until one row is ready to be output, or all input
		// is consumed.
		static int Import(byte* src, int src_stride,
						  int new_lines, WebPRescaler* wrk) {
		  int num_lines_in = 0;
		  while (num_lines_in < new_lines && wrk.y_accum > 0) {
			ImportRow(src, wrk);
			src += src_stride;
			++num_lines_in;
			wrk.y_accum -= wrk.y_sub;
		  }
		  return num_lines_in;
		}

		static int ExportRGB(WebPDecParams* p, int y_pos) {
		  WebPYUV444Converter convert =
			  WebPYUV444Converters[p.output.colorspace];
		  WebPRGBABuffer* buf = &p.output.u.RGBA;
		  byte* dst = buf.rgba + (p.last_y + y_pos) * buf.stride;
		  int num_lines_out = 0;
		  // For RGB rescaling, because of the YUV420, current scan position
		  // U/V can be +1/-1 line from the Y one.  Hence the double test.
		  while (p.scaler_y.y_accum <= 0 && p.scaler_u.y_accum <= 0) {
			assert(p.last_y + y_pos + num_lines_out < p.output.height);
			assert(p.scaler_u.y_accum == p.scaler_v.y_accum);
			ExportRow(&p.scaler_y);
			ExportRow(&p.scaler_u);
			ExportRow(&p.scaler_v);
			convert(p.scaler_y.dst, p.scaler_u.dst, p.scaler_v.dst,
					dst, p.scaler_y.dst_width);
			dst += buf.stride;
			++num_lines_out;
		  }
		  return num_lines_out;
		}

		static int EmitRescaledRGB(VP8Io* io, WebPDecParams* p) {
		  int mb_h = io.mb_h;
		  int uv_mb_h = (mb_h + 1) >> 1;
		  int j = 0, uv_j = 0;
		  int num_lines_out = 0;
		  while (j < mb_h) {
			int y_lines_in = Import(io.y + j * io.y_stride, io.y_stride,
										  mb_h - j, &p.scaler_y);
			int u_lines_in = Import(io.u + uv_j * io.uv_stride, io.uv_stride,
										  uv_mb_h - uv_j, &p.scaler_u);
			int v_lines_in = Import(io.v + uv_j * io.uv_stride, io.uv_stride,
										  uv_mb_h - uv_j, &p.scaler_v);
			(void)v_lines_in;   // remove a gcc warning
			assert(u_lines_in == v_lines_in);
			j += y_lines_in;
			uv_j += u_lines_in;
			num_lines_out += ExportRGB(p, num_lines_out);
		  }
		  return num_lines_out;
		}

		static int ExportAlpha(WebPDecParams* p, int y_pos) {
		  WebPRGBABuffer* buf = &p.output.u.RGBA;
		  byte* dst = buf.rgba + (p.last_y + y_pos) * buf.stride +
						 (p.output.colorspace == MODE_ARGB ? 0 : 3);
		  int num_lines_out = 0;
		  while (p.scaler_a.y_accum <= 0) {
			int i;
			assert(p.last_y + y_pos + num_lines_out < p.output.height);
			ExportRow(&p.scaler_a);
			for (i = 0; i < p.scaler_a.dst_width; ++i) {
			  dst[4 * i] = p.scaler_a.dst[i];
			}
			dst += buf.stride;
			++num_lines_out;
		  }
		  return num_lines_out;
		}

		static int ExportAlphaRGBA4444(WebPDecParams* p, int y_pos) {
		  WebPRGBABuffer* buf = &p.output.u.RGBA;
		  byte* dst = buf.rgba + (p.last_y + y_pos) * buf.stride + 1;
		  int num_lines_out = 0;
		  while (p.scaler_a.y_accum <= 0) {
			int i;
			assert(p.last_y + y_pos + num_lines_out < p.output.height);
			ExportRow(&p.scaler_a);
			for (i = 0; i < p.scaler_a.dst_width; ++i) {
			  // Fill in the alpha value (converted to 4 bits).
			  uint alpha_val = clip((p.scaler_a.dst[i] + 8) >> 4, 15);
			  dst[2 * i] = (dst[2 * i] & 0xf0) | alpha_val;
			}
			dst += buf.stride;
			++num_lines_out;
		  }
		  return num_lines_out;
		}

		static int EmitRescaledAlphaRGB(VP8Io* io, WebPDecParams* p) {
		  if (io.a != null) {
			int (* output_func)(WebPDecParams* const, int) =
				(p.output.colorspace == MODE_RGBA_4444) ? ExportAlphaRGBA4444
														  : ExportAlpha;
			WebPRescaler* scaler = &p.scaler_a;
			int j = 0, pos = 0;
			while (j < io.mb_h) {
			  j += Import(io.a + j * io.width, io.width, io.mb_h - j, scaler);
			  pos += output_func(p, pos);
			}
		  }
		  return 0;
		}

		static int InitRGBRescaler(VP8Io* io, WebPDecParams* p) {
		  int has_alpha = IsAlphaMode(p.output.colorspace);
		  int out_width  = io.scaled_width;
		  int out_height = io.scaled_height;
		  int uv_in_width  = (io.mb_w + 1) >> 1;
		  int uv_in_height = (io.mb_h + 1) >> 1;
		  uint work_size = 2 * out_width;   // scratch memory for one rescaler
		  int* work;  // rescalers work area
		  byte* tmp;   // tmp storage for scaled YUV444 samples before RGB conversion
		  uint tmp_size1, tmp_size2;

		  tmp_size1 = 3 * work_size;
		  tmp_size2 = 3 * out_width;
		  if (has_alpha) {
			tmp_size1 += work_size;
			tmp_size2 += out_width;
		  }
		  p.memory =
			  calloc(1, tmp_size1 * sizeof(*work) + tmp_size2 * sizeof(*tmp));
		  if (p.memory == null) {
			return 0;   // memory error
		  }
		  work = (int*)p.memory;
		  tmp = (byte*)(work + tmp_size1);
		  InitRescaler(&p.scaler_y, io.mb_w, io.mb_h,
					   tmp + 0 * out_width, out_width, out_height, 0,
					   io.mb_w, out_width, io.mb_h, out_height,
					   work + 0 * work_size);
		  InitRescaler(&p.scaler_u, uv_in_width, uv_in_height,
					   tmp + 1 * out_width, out_width, out_height, 0,
					   io.mb_w, 2 * out_width, io.mb_h, 2 * out_height,
					   work + 1 * work_size);
		  InitRescaler(&p.scaler_v, uv_in_width, uv_in_height,
					   tmp + 2 * out_width, out_width, out_height, 0,
					   io.mb_w, 2 * out_width, io.mb_h, 2 * out_height,
					   work + 2 * work_size);
		  p.emit = EmitRescaledRGB;

		  if (has_alpha) {
			InitRescaler(&p.scaler_a, io.mb_w, io.mb_h,
						 tmp + 3 * out_width, out_width, out_height, 0,
						 io.mb_w, out_width, io.mb_h, out_height,
						 work + 3 * work_size);
			p.emit_alpha = EmitRescaledAlphaRGB;
		  }
		  return 1;
		}

		//------------------------------------------------------------------------------
		// Default custom functions

		// Setup crop_xxx fields, mb_w and mb_h
		static int InitFromOptions(WebPDecoderOptions* options,
								   VP8Io* io) {
		  int W = io.width;
		  int H = io.height;
		  int x = 0, y = 0, w = W, h = H;

		  // Cropping
		  io.use_cropping = (options != null) && (options.use_cropping > 0);
		  if (io.use_cropping) {
			w = options.crop_width;
			h = options.crop_height;
			// TODO(skal): take colorspace into account. Don't assume YUV420.
			x = options.crop_left & ~1;
			y = options.crop_top & ~1;
			if (x < 0 || y < 0 || w <= 0 || h <= 0 || x + w > W || y + h > H) {
			  return 0;  // out of frame boundary error
			}
		  }
		  io.crop_left   = x;
		  io.crop_top    = y;
		  io.crop_right  = x + w;
		  io.crop_bottom = y + h;
		  io.mb_w = w;
		  io.mb_h = h;

		  // Scaling
		  io.use_scaling = (options != null) && (options.use_scaling > 0);
		  if (io.use_scaling) {
			if (options.scaled_width <= 0 || options.scaled_height <= 0) {
			  return 0;
			}
			io.scaled_width = options.scaled_width;
			io.scaled_height = options.scaled_height;
		  }

		  // Filter
		  io.bypass_filtering = options && options.bypass_filtering;

		  // Fancy upsampler
		#if FANCY_UPSAMPLING
		  io.fancy_upsampling = (options == null) || (!options.no_fancy_upsampling);
		#endif

		  if (io.use_scaling) {
			// disable filter (only for large downscaling ratio).
			io.bypass_filtering = (io.scaled_width < W * 3 / 4) &&
								   (io.scaled_height < H * 3 / 4);
			io.fancy_upsampling = 0;
		  }
		  return 1;
		}

		static int CustomSetup(VP8Io* io) {
		  WebPDecParams* p = (WebPDecParams*)io.opaque;
		  int is_rgb = (p.output.colorspace < MODE_YUV);

		  p.memory = null;
		  p.emit = null;
		  p.emit_alpha = null;
		  if (!InitFromOptions(p.options, io)) {
			return 0;
		  }

		  if (io.use_scaling) {
			int ok = is_rgb ? InitRGBRescaler(io, p) : InitYUVRescaler(io, p);
			if (!ok) {
			  return 0;    // memory error
			}
		  } else {
			if (is_rgb) {
			  p.emit = EmitSampledRGB;   // default
		#if FANCY_UPSAMPLING
			  if (io.fancy_upsampling) {
				int uv_width = (io.mb_w + 1) >> 1;
				p.memory = malloc(io.mb_w + 2 * uv_width);
				if (p.memory == null) {
				  return 0;   // memory error.
				}
				p.tmp_y = (byte*)p.memory;
				p.tmp_u = p.tmp_y + io.mb_w;
				p.tmp_v = p.tmp_u + uv_width;
				p.emit = EmitFancyRGB;
				WebPInitUpsamplers();
			  }
		#endif
			} else {
			  p.emit = EmitYUV;
			}
			if (IsAlphaMode(p.output.colorspace)) {
			  // We need transparency output
			  p.emit_alpha =
				  is_rgb ? (p.output.colorspace == MODE_RGBA_4444 ?
					  EmitAlphaRGBA4444 : EmitAlphaRGB) : EmitAlphaYUV;
			}
		  }

		  if (is_rgb) {
			VP8YUVInit();
		  }
		  return 1;
		}

		//------------------------------------------------------------------------------

		static int CustomPut(VP8Io* io) {
		  WebPDecParams* p = (WebPDecParams*)io.opaque;
		  int mb_w = io.mb_w;
		  int mb_h = io.mb_h;
		  int num_lines_out;
		  assert(!(io.mb_y & 1));

		  if (mb_w <= 0 || mb_h <= 0) {
			return 0;
		  }
		  num_lines_out = p.emit(io, p);
		  if (p.emit_alpha) {
			p.emit_alpha(io, p);
		  }
		  p.last_y += num_lines_out;
		  return 1;
		}

		//------------------------------------------------------------------------------

		static void CustomTeardown(VP8Io* io) {
		  WebPDecParams* p = (WebPDecParams*)io.opaque;
		  free(p.memory);
		  p.memory = null;
		}

		//------------------------------------------------------------------------------
		// Main entry point

		void WebPInitCustomIo(WebPDecParams* params, VP8Io* io) {
		  io.put      = CustomPut;
		  io.setup    = CustomSetup;
		  io.teardown = CustomTeardown;
		  io.opaque   = params;
		}

		//------------------------------------------------------------------------------


	}
}
#endif

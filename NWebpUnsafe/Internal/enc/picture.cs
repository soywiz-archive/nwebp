﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#if false
namespace NWebp.Internal
{
	class picture
	{

		int HALVE(x) { return (((x) + 1) >> 1) }

		//------------------------------------------------------------------------------
		// WebPPicture
		//------------------------------------------------------------------------------

		int WebPPictureAlloc(ref WebPPicture picture) {
		  if (picture != null) {
			WebPEncCSP uv_csp = picture.colorspace & WEBP_CSP_UV_MASK;
			int has_alpha = picture.colorspace & WEBP_CSP_ALPHA_BIT;
			int width = picture.width;
			int height = picture.height;
			int y_stride = width;
			int uv_width = HALVE(width);
			int uv_height = HALVE(height);
			int uv_stride = uv_width;
			int uv0_stride = 0;
			int a_width, a_stride;
			ulong y_size, uv_size, uv0_size, a_size, total_size;
			byte* mem;

			// U/V
			switch (uv_csp) {
			  case WEBP_YUV420:
				break;
		#if WEBP_EXPERIMENTAL_FEATURES
			  case WEBP_YUV400:    // for now, we'll just reset the U/V samples
				break;
			  case WEBP_YUV422:
				uv0_stride = uv_width;
				break;
			  case WEBP_YUV444:
				uv0_stride = width;
				break;
		#endif
			  default:
				return 0;
			}
			uv0_size = height * uv0_stride;

			// alpha
			a_width = has_alpha ? width : 0;
			a_stride = a_width;
			y_size = (ulong)y_stride * height;
			uv_size = (ulong)uv_stride * uv_height;
			a_size =  (ulong)a_stride * height;

			total_size = y_size + a_size + 2 * uv_size + 2 * uv0_size;

			// Security and validation checks
			if (width <= 0 || height <= 0 ||       // check for luma/alpha param error
				uv_width < 0 || uv_height < 0 ||   // check for u/v param error
				y_size >= (1ULL << 40) ||            // check for reasonable global size
				(uint)total_size != total_size) {  // check for overflow on 32bit
			  return 0;
			}
			picture.y_stride  = y_stride;
			picture.uv_stride = uv_stride;
			picture.a_stride  = a_stride;
			picture.uv0_stride  = uv0_stride;
			WebPPictureFree(picture);   // erase previous buffer
			mem = (byte*)malloc((uint)total_size);
			if (mem == null) return 0;

			picture.y = mem;
			mem += y_size;

			picture.u = mem;
			mem += uv_size;
			picture.v = mem;
			mem += uv_size;

			if (a_size) {
			  picture.a = mem;
			  mem += a_size;
			}
			if (uv0_size) {
			  picture.u0 = mem;
			  mem += uv0_size;
			  picture.v0 = mem;
			  mem += uv0_size;
			}
		  }
		  return 1;
		}

		// Grab the 'specs' (writer, *opaque, width, height...) from 'src' and copy them
		// into 'dst'. Mark 'dst' as not owning any memory. 'src' can be null.
		static void WebPPictureGrabSpecs(WebPPicture* src,
										 WebPPicture* dst) {
		  if (src != null) *dst = *src;
		  dst.y = dst.u = dst.v = null;
		  dst.u0 = dst.v0 = null;
		  dst.a = null;
		}

		// Release memory owned by 'picture'.
		void WebPPictureFree(WebPPicture* picture) {
		  if (picture != null) {
			free(picture.y);
			WebPPictureGrabSpecs(null, picture);
		  }
		}

		//------------------------------------------------------------------------------
		// Picture copying

		// Not worth moving to dsp/enc.c (only used here).
		static void CopyPlane(byte* src, int src_stride,
							  byte* dst, int dst_stride, int width, int height) {
		  while (height-- > 0) {
			memcpy(dst, src, width);
			src += src_stride;
			dst += dst_stride;
		  }
		}

		int WebPPictureCopy(WebPPicture* src, WebPPicture* dst) {
		  if (src == null || dst == null) return 0;
		  if (src == dst) return 1;

		  WebPPictureGrabSpecs(src, dst);
		  if (!WebPPictureAlloc(dst)) return 0;

		  CopyPlane(src.y, src.y_stride,
					dst.y, dst.y_stride, dst.width, dst.height);
		  CopyPlane(src.u, src.uv_stride,
					dst.u, dst.uv_stride, HALVE(dst.width), HALVE(dst.height));
		  CopyPlane(src.v, src.uv_stride,
					dst.v, dst.uv_stride, HALVE(dst.width), HALVE(dst.height));
		  if (dst.a != null)  {
			CopyPlane(src.a, src.a_stride,
					  dst.a, dst.a_stride, dst.width, dst.height);
		  }
		#if WEBP_EXPERIMENTAL_FEATURES
		  if (dst.u0 != null)  {
			int uv0_width = src.width;
			if ((dst.colorspace & WEBP_CSP_UV_MASK) == WEBP_YUV422) {
			  uv0_width = HALVE(uv0_width);
			}
			CopyPlane(src.u0, src.uv0_stride,
					  dst.u0, dst.uv0_stride, uv0_width, dst.height);
			CopyPlane(src.v0, src.uv0_stride,
					  dst.v0, dst.uv0_stride, uv0_width, dst.height);
		  }
		#endif
		  return 1;
		}

		//------------------------------------------------------------------------------
		// Picture cropping

		int WebPPictureCrop(WebPPicture* pic,
							int left, int top, int width, int height) {
		  WebPPicture tmp;

		  if (pic == null) return 0;
		  if (width <= 0 || height <= 0) return 0;
		  if (left < 0 || ((left + width + 1) & ~1) > pic.width) return 0;
		  if (top < 0 || ((top + height + 1) & ~1) > pic.height) return 0;

		  WebPPictureGrabSpecs(pic, &tmp);
		  tmp.width = width;
		  tmp.height = height;
		  if (!WebPPictureAlloc(&tmp)) return 0;

		  {
			int y_offset = top * pic.y_stride + left;
			int uv_offset = (top / 2) * pic.uv_stride + left / 2;
			CopyPlane(pic.y + y_offset, pic.y_stride,
					  tmp.y, tmp.y_stride, width, height);
			CopyPlane(pic.u + uv_offset, pic.uv_stride,
					  tmp.u, tmp.uv_stride, HALVE(width), HALVE(height));
			CopyPlane(pic.v + uv_offset, pic.uv_stride,
					  tmp.v, tmp.uv_stride, HALVE(width), HALVE(height));
		  }

		  if (tmp.a != null) {
			int a_offset = top * pic.a_stride + left;
			CopyPlane(pic.a + a_offset, pic.a_stride,
					  tmp.a, tmp.a_stride, width, height);
		  }
		#if WEBP_EXPERIMENTAL_FEATURES
		  if (tmp.u0 != null) {
			int w = width;
			int l = left;
			if (tmp.colorspace == WEBP_YUV422) {
			  w = HALVE(w);
			  l = HALVE(l);
			}
			CopyPlane(pic.u0 + top * pic.uv0_stride + l, pic.uv0_stride,
					  tmp.u0, tmp.uv0_stride, w, l);
			CopyPlane(pic.v0 + top * pic.uv0_stride + l, pic.uv0_stride,
					  tmp.v0, tmp.uv0_stride, w, l);
		  }
		#endif

		  WebPPictureFree(pic);
		  *pic = tmp;
		  return 1;
		}

		//------------------------------------------------------------------------------
		// Simple picture rescaler

		const int RFIX 30
		void MULT(x,y) { return (((long)(x) * (y) + (1 << (RFIX - 1))) >> RFIX); }
		static void ImportRow(byte* src, int src_width,
										  int* frow, int* irow, int dst_width) {
		  int x_expand = (src_width < dst_width);
		  int fx_scale = (1 << RFIX) / dst_width;
		  int x_in = 0;
		  int x_out;
		  int x_accum = 0;
		  if (!x_expand) {
			int sum = 0;
			for (x_out = 0; x_out < dst_width; ++x_out) {
			  x_accum += src_width - dst_width;
			  for (; x_accum > 0; x_accum -= dst_width) {
				sum += src[x_in++];
			  }
			  {        // Emit next horizontal pixel.
				int base = src[x_in++];
				int frac = base * (-x_accum);
				frow[x_out] = (sum + base) * dst_width - frac;
				sum = MULT(frac, fx_scale);    // fresh fractional start for next pixel
			  }
			}
		  } else {        // simple bilinear interpolation
			int left = src[0], right = src[0];
			for (x_out = 0; x_out < dst_width; ++x_out) {
			  if (x_accum < 0) {
				left = right;
				right = src[++x_in];
				x_accum += dst_width - 1;
			  }
			  frow[x_out] = right * (dst_width - 1) + (left - right) * x_accum;
			  x_accum -= src_width - 1;
			}
		  }
		  // Accumulate the new row's contribution
		  for (x_out = 0; x_out < dst_width; ++x_out) {
			irow[x_out] += frow[x_out];
		  }
		}

		static void ExportRow(int* frow, int* irow, byte* dst, int dst_width,
							  int yscale, long fxy_scale) {
		  int x_out;
		  for (x_out = 0; x_out < dst_width; ++x_out) {
			int frac = MULT(frow[x_out], yscale);
			int v = (int)(MULT(irow[x_out] - frac, fxy_scale));
			dst[x_out] = (!(v & ~0xff)) ? v : (v < 0) ? 0 : 255;
			irow[x_out] = frac;   // new fractional start
		  }
		}

		static void RescalePlane(byte* src,
								 int src_width, int src_height, int src_stride,
								 byte* dst,
								 int dst_width, int dst_height, int dst_stride,
								 int* work) {
		  int x_expand = (src_width < dst_width);
		  int fy_scale = (1 << RFIX) / dst_height;
		  long fxy_scale = x_expand ?
			  ((long)dst_height << RFIX) / (dst_width * src_height) :
			  ((long)dst_height << RFIX) / (src_width * src_height);
		  int y_accum = src_height;
		  int y;
		  int* irow = work;              // integral contribution
		  int* frow = work + dst_width;  // fractional contribution

		  memset(work, 0, 2 * dst_width * sizeof(*work));
		  for (y = 0; y < src_height; ++y) {
			// import new contribution of one source row.
			ImportRow(src, src_width, frow, irow, dst_width);
			src += src_stride;
			// emit output row(s)
			y_accum -= dst_height;
			for (; y_accum <= 0; y_accum += src_height) {
			  int yscale = fy_scale * (-y_accum);
			  ExportRow(frow, irow, dst, dst_width, yscale, fxy_scale);
			  dst += dst_stride;
			}
		  }
		}
		#undef MULT
		#undef RFIX

		int WebPPictureRescale(WebPPicture* pic, int width, int height) {
		  WebPPicture tmp;
		  int prev_width, prev_height;
		  int* work;

		  if (pic == null) return 0;
		  prev_width = pic.width;
		  prev_height = pic.height;
		  // if width is unspecified, scale original proportionally to height ratio.
		  if (width == 0) {
			width = (prev_width * height + prev_height / 2) / prev_height;
		  }
		  // if height is unspecified, scale original proportionally to width ratio.
		  if (height == 0) {
			height = (prev_height * width + prev_width / 2) / prev_width;
		  }
		  // Check if the overall dimensions still make sense.
		  if (width <= 0 || height <= 0) return 0;

		  WebPPictureGrabSpecs(pic, &tmp);
		  tmp.width = width;
		  tmp.height = height;
		  if (!WebPPictureAlloc(&tmp)) return 0;

		  work = (int*)malloc(2 * width * sizeof(int));
		  if (work == null) {
			WebPPictureFree(&tmp);
			return 0;
		  }

		  RescalePlane(pic.y, prev_width, prev_height, pic.y_stride,
					   tmp.y, width, height, tmp.y_stride, work);
		  RescalePlane(pic.u,
					   HALVE(prev_width), HALVE(prev_height), pic.uv_stride,
					   tmp.u,
					   HALVE(width), HALVE(height), tmp.uv_stride, work);
		  RescalePlane(pic.v,
					   HALVE(prev_width), HALVE(prev_height), pic.uv_stride,
					   tmp.v,
					   HALVE(width), HALVE(height), tmp.uv_stride, work);

		  if (tmp.a != null) {
			RescalePlane(pic.a, prev_width, prev_height, pic.a_stride,
						 tmp.a, width, height, tmp.a_stride, work);
		  }
		#if WEBP_EXPERIMENTAL_FEATURES
		  if (tmp.u0 != null) {
			int s = 1;
			if ((tmp.colorspace & WEBP_CSP_UV_MASK) == WEBP_YUV422) {
			  s = 2;
			}
			RescalePlane(
				pic.u0, (prev_width + s / 2) / s, prev_height, pic.uv0_stride,
				tmp.u0, (width + s / 2) / s, height, tmp.uv0_stride, work);
			RescalePlane(
				pic.v0, (prev_width + s / 2) / s, prev_height, pic.uv0_stride,
				tmp.v0, (width + s / 2) / s, height, tmp.uv0_stride, work);
		  }
		#endif

		  WebPPictureFree(pic);
		  free(work);
		  *pic = tmp;
		  return 1;
		}

		//------------------------------------------------------------------------------
		// Write-to-memory

		typedef struct {
		  byte** mem;
		  uint    max_size;
		  uint*   size;
		} WebPMemoryWriter;

		static void WebPMemoryWriterInit(WebPMemoryWriter* writer) {
		  *writer.mem = null;
		  *writer.size = 0;
		  writer.max_size = 0;
		}

		static int WebPMemoryWrite(byte* data, uint data_size,
								   WebPPicture* picture) {
		  WebPMemoryWriter* w = (WebPMemoryWriter*)picture.custom_ptr;
		  uint next_size;
		  if (w == null) {
			return 1;
		  }
		  next_size = (*w.size) + data_size;
		  if (next_size > w.max_size) {
			byte* new_mem;
			uint next_max_size = w.max_size * 2;
			if (next_max_size < next_size) next_max_size = next_size;
			if (next_max_size < 8192) next_max_size = 8192;
			new_mem = (byte*)malloc(next_max_size);
			if (new_mem == null) {
			  return 0;
			}
			if ((*w.size) > 0) {
			  memcpy(new_mem, *w.mem, *w.size);
			}
			free(*w.mem);
			*w.mem = new_mem;
			w.max_size = next_max_size;
		  }
		  if (data_size > 0) {
			memcpy((*w.mem) + (*w.size), data, data_size);
			*w.size += data_size;
		  }
		  return 1;
		}

		//------------------------------------------------------------------------------
		// RGB . YUV conversion
		// The exact naming is Y'CbCr, following the ITU-R BT.601 standard.
		// More information at: http://en.wikipedia.org/wiki/YCbCr
		// Y = 0.2569 * R + 0.5044 * G + 0.0979 * B + 16
		// U = -0.1483 * R - 0.2911 * G + 0.4394 * B + 128
		// V = 0.4394 * R - 0.3679 * G - 0.0715 * B + 128
		// We use 16bit fixed point operations.

		enum { YUV_FRAC = 16 };

		static int clip_uv(int v) {
		   v = (v + (257 << (YUV_FRAC + 2 - 1))) >> (YUV_FRAC + 2);
		   return ((v & ~0xff) == 0) ? v : (v < 0) ? 0 : 255;
		}

		static int rgb_to_y(int r, int g, int b) {
		  int kRound = (1 << (YUV_FRAC - 1)) + (16 << YUV_FRAC);
		  int luma = 16839 * r + 33059 * g + 6420 * b;
		  return (luma + kRound) >> YUV_FRAC;  // no need to clip
		}

		static int rgb_to_u(int r, int g, int b) {
		  return clip_uv(-9719 * r - 19081 * g + 28800 * b);
		}

		static int rgb_to_v(int r, int g, int b) {
		  return clip_uv(+28800 * r - 24116 * g - 4684 * b);
		}

		// TODO: we can do better than simply 2x2 averaging on U/V samples.
		void SUM4(ptr) { return ((ptr)[0] + (ptr)[step] + (ptr)[rgb_stride] + (ptr)[rgb_stride + step]) }
		void SUM2H(ptr) { return (2 * (ptr)[0] + 2 * (ptr)[step]) }
		void SUM2V(ptr) { return (2 * (ptr)[0] + 2 * (ptr)[rgb_stride]) }
		void SUM1(ptr)  { return (4 * (ptr)[0]) }
		void RGB_TO_UV(x, y, SUM) {                          
		  int src = (2 * (step * (x) + (y) * rgb_stride));
		  int dst = (x) + (y) * picture.uv_stride;        
		  int r = SUM(r_ptr + src);                       
		  int g = SUM(g_ptr + src);                       
		  int b = SUM(b_ptr + src);                       
		  picture.u[dst] = rgb_to_u(r, g, b);             
		  picture.v[dst] = rgb_to_v(r, g, b);             
		}

		void RGB_TO_UV0(x_in, x_out, y, SUM) {              
		  int src = (step * (x_in) + (y) * rgb_stride);    
		  int dst = (x_out) + (y) * picture.uv0_stride;   
		  int r = SUM(r_ptr + src);                       
		  int g = SUM(g_ptr + src);                       
		  int b = SUM(b_ptr + src);                       
		  picture.u0[dst] = rgb_to_u(r, g, b);            
		  picture.v0[dst] = rgb_to_v(r, g, b);            
		}

		static void MakeGray(WebPPicture* picture) {
		  int y;
		  int uv_width = HALVE(picture.width);
		  int uv_height = HALVE(picture.height);
		  for (y = 0; y < uv_height; ++y) {
			memset(picture.u + y * picture.uv_stride, 128, uv_width);
			memset(picture.v + y * picture.uv_stride, 128, uv_width);
		  }
		}

		static int Import(WebPPicture* picture,
						  byte* rgb, int rgb_stride,
						  int step, int swap_rb, int import_alpha) {
		  WebPEncCSP uv_csp = picture.colorspace & WEBP_CSP_UV_MASK;
		  int x, y;
		  byte* r_ptr = rgb + (swap_rb ? 2 : 0);
		  byte* g_ptr = rgb + 1;
		  byte* b_ptr = rgb + (swap_rb ? 0 : 2);
		  int width = picture.width;
		  int height = picture.height;

		  // Import luma plane
		  for (y = 0; y < height; ++y) {
			for (x = 0; x < width; ++x) {
			  int offset = step * x + y * rgb_stride;
			  picture.y[x + y * picture.y_stride] =
				rgb_to_y(r_ptr[offset], g_ptr[offset], b_ptr[offset]);
			}
		  }

		  // Downsample U/V plane
		  if (uv_csp != WEBP_YUV400) {
			for (y = 0; y < (height >> 1); ++y) {
			  for (x = 0; x < (width >> 1); ++x) {
				RGB_TO_UV(x, y, SUM4);
			  }
			  if (picture.width & 1) {
				RGB_TO_UV(x, y, SUM2V);
			  }
			}
			if (height & 1) {
			  for (x = 0; x < (width >> 1); ++x) {
				RGB_TO_UV(x, y, SUM2H);
			  }
			  if (width & 1) {
				RGB_TO_UV(x, y, SUM1);
			  }
			}

		#if WEBP_EXPERIMENTAL_FEATURES
			// Store original U/V samples too
			if (uv_csp == WEBP_YUV422) {
			  for (y = 0; y < height; ++y) {
				for (x = 0; x < (width >> 1); ++x) {
				  RGB_TO_UV0(2 * x, x, y, SUM2H);
				}
				if (width & 1) {
				  RGB_TO_UV0(2 * x, x, y, SUM1);
				}
			  }
			} else if (uv_csp == WEBP_YUV444) {
			  for (y = 0; y < height; ++y) {
				for (x = 0; x < width; ++x) {
				  RGB_TO_UV0(x, x, y, SUM1);
				}
			  }
			}
		#endif
		  } else {
			MakeGray(picture);
		  }

		  if (import_alpha) {
			byte* a_ptr = rgb + 3;
			assert(step >= 4);
			for (y = 0; y < height; ++y) {
			  for (x = 0; x < width; ++x) {
				picture.a[x + y * picture.a_stride] =
				  a_ptr[step * x + y * rgb_stride];
			  }
			}
		  }
		  return 1;
		}
		#undef SUM4
		#undef SUM2V
		#undef SUM2H
		#undef SUM1
		#undef RGB_TO_UV

		int WebPPictureImportRGB(WebPPicture* picture,
								 byte* rgb, int rgb_stride) {
		  picture.colorspace &= ~WEBP_CSP_ALPHA_BIT;
		  if (!WebPPictureAlloc(picture)) return 0;
		  return Import(picture, rgb, rgb_stride, 3, 0, 0);
		}

		int WebPPictureImportBGR(WebPPicture* picture,
								 byte* rgb, int rgb_stride) {
		  picture.colorspace &= ~WEBP_CSP_ALPHA_BIT;
		  if (!WebPPictureAlloc(picture)) return 0;
		  return Import(picture, rgb, rgb_stride, 3, 1, 0);
		}

		int WebPPictureImportRGBA(WebPPicture* picture,
								  byte* rgba, int rgba_stride) {
		  picture.colorspace |= WEBP_CSP_ALPHA_BIT;
		  if (!WebPPictureAlloc(picture)) return 0;
		  return Import(picture, rgba, rgba_stride, 4, 0, 1);
		}

		int WebPPictureImportBGRA(WebPPicture* picture,
								  byte* rgba, int rgba_stride) {
		  picture.colorspace |= WEBP_CSP_ALPHA_BIT;
		  if (!WebPPictureAlloc(picture)) return 0;
		  return Import(picture, rgba, rgba_stride, 4, 1, 1);
		}

		//------------------------------------------------------------------------------
		// Helper: clean up fully transparent area to help compressibility.

		const int SIZE = 8;
		const int SIZE2 = (SIZE / 2);
		static int is_transparent_area(byte* ptr, int stride, int size) {
		  int y, x;
		  for (y = 0; y < size; ++y) {
			for (x = 0; x < size; ++x) {
			  if (ptr[x]) {
				return 0;
			  }
			}
			ptr += stride;
		  }
		  return 1;
		}

		static void flatten(byte* ptr, int v, int stride, int size) {
		  int y;
		  for (y = 0; y < size; ++y) {
			memset(ptr, v, size);
			ptr += stride;
		  }
		}

		void WebPCleanupTransparentArea(WebPPicture* pic) {
		  int x, y, w, h;
		  byte* a_ptr;
		  int values[3] = { 0 };

		  if (pic == null) return;

		  a_ptr = pic.a;
		  if (a_ptr == null) return;    // nothing to do

		  w = pic.width / SIZE;
		  h = pic.height / SIZE;
		  for (y = 0; y < h; ++y) {
			int need_reset = 1;
			for (x = 0; x < w; ++x) {
			  int off_a = (y * pic.a_stride + x) * SIZE;
			  int off_y = (y * pic.y_stride + x) * SIZE;
			  int off_uv = (y * pic.uv_stride + x) * SIZE2;
			  if (is_transparent_area(a_ptr + off_a, pic.a_stride, SIZE)) {
				if (need_reset) {
				  values[0] = pic.y[off_y];
				  values[1] = pic.u[off_uv];
				  values[2] = pic.v[off_uv];
				  need_reset = 0;
				}
				flatten(pic.y + off_y, values[0], pic.y_stride, SIZE);
				flatten(pic.u + off_uv, values[1], pic.uv_stride, SIZE2);
				flatten(pic.v + off_uv, values[2], pic.uv_stride, SIZE2);
			  } else {
				need_reset = 1;
			  }
			}
			// ignore the left-overs on right/bottom
		  }
		}

		#undef SIZE
		#undef SIZE2

		//------------------------------------------------------------------------------
		// Distortion

		// Max value returned in case of exact similarity.
		static double kMinDistortion_dB = 99.;

		int WebPPictureDistortion(WebPPicture* pic1,
								  WebPPicture* pic2,
								  int type, float result[5]) {
		  int c;
		  DistoStats stats[5];

		  if (pic1.width != pic2.width ||
			  pic1.height != pic2.height ||
			  (pic1.a == null) != (pic2.a == null)) {
			return 0;
		  }

		  memset(stats, 0, sizeof(stats));
		  VP8SSIMAccumulatePlane(pic1.y, pic1.y_stride,
								 pic2.y, pic2.y_stride,
								 pic1.width, pic1.height, &stats[0]);
		  VP8SSIMAccumulatePlane(pic1.u, pic1.uv_stride,
								 pic2.u, pic2.uv_stride,
								 (pic1.width + 1) >> 1, (pic1.height + 1) >> 1,
								 &stats[1]);
		  VP8SSIMAccumulatePlane(pic1.v, pic1.uv_stride,
								 pic2.v, pic2.uv_stride,
								 (pic1.width + 1) >> 1, (pic1.height + 1) >> 1,
								 &stats[2]);
		  if (pic1.a != null) {
			VP8SSIMAccumulatePlane(pic1.a, pic1.a_stride,
								   pic2.a, pic2.a_stride,
								   pic1.width, pic1.height, &stats[3]);
		  }
		  for (c = 0; c <= 4; ++c) {
			if (type == 1) {
			  double v = VP8SSIMGet(&stats[c]);
			  result[c] = (float)((v < 1.) ? -10.0 * log10(1. - v)
										   : kMinDistortion_dB);
			} else {
			  double v = VP8SSIMGetSquaredError(&stats[c]);
			  result[c] = (float)((v > 0.) ? -4.3429448 * log(v / (255 * 255.))
										   : kMinDistortion_dB);
			}
			// Accumulate forward
			if (c < 4) VP8SSIMAddStats(&stats[c], &stats[4]);
		  }
		  return 1;
		}

		//------------------------------------------------------------------------------
		// Simplest high-level calls:

		typedef int (*Importer)(WebPPicture* const, byte* const, int);

		static uint Encode(byte* rgba, int width, int height, int stride,
							 Importer import, float quality_factor, byte** output) {
		  uint output_size = 0;
		  WebPPicture pic;
		  WebPConfig config;
		  WebPMemoryWriter wrt;
		  int ok;

		  if (!WebPConfigPreset(&config, WEBP_PRESET_DEFAULT, quality_factor) ||
			  !WebPPictureInit(&pic)) {
			return 0;  // shouldn't happen, except if system installation is broken
		  }

		  pic.width = width;
		  pic.height = height;
		  pic.writer = WebPMemoryWrite;
		  pic.custom_ptr = &wrt;

		  wrt.mem = output;
		  wrt.size = &output_size;
		  WebPMemoryWriterInit(&wrt);

		  ok = import(&pic, rgba, stride) && WebPEncode(&config, &pic);
		  WebPPictureFree(&pic);
		  if (!ok) {
			free(*output);
			*output = null;
			return 0;
		  }
		  return output_size;
		}

		MACRO ENCODE_FUNC(NAME, IMPORTER)  {
		uint NAME(byte* in, int w, int h, int bps, float q,
					byte** out) {
		  return Encode(in, w, h, bps, IMPORTER, q, out); 
		}
		}

		ENCODE_FUNC(WebPEncodeRGB, WebPPictureImportRGB);
		ENCODE_FUNC(WebPEncodeBGR, WebPPictureImportBGR);
		ENCODE_FUNC(WebPEncodeRGBA, WebPPictureImportRGBA);
		ENCODE_FUNC(WebPEncodeBGRA, WebPPictureImportBGRA);

		#undef ENCODE_FUNC

		//------------------------------------------------------------------------------


	}
}
#endif
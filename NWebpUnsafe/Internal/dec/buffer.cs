using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#if false
namespace NWebp.Internal
{
	unsafe public partial class WebPDecBuffer
	{
		//------------------------------------------------------------------------------
		// WebPDecBuffer

		// Number of bytes per pixel for the different color-spaces.
		static int[] kModeBpp = new int[(int)WEBP_CSP_MODE.MODE_LAST] { 3, 4, 3, 4, 4, 2, 2, 1, 1 };

		// Check that webp_csp_mode is within the bounds of WEBP_CSP_MODE.
		// Convert to an integer to handle both the unsigned/signed enum cases
		// without the need for casting to remove type limit warnings.
		static bool IsValidColorspace(WEBP_CSP_MODE webp_csp_mode) {
		  return (((int)webp_csp_mode >= (int)WEBP_CSP_MODE.MODE_RGB) && ((int)webp_csp_mode < (int)WEBP_CSP_MODE.MODE_LAST));
		}

		VP8StatusCode CheckDecBuffer() {
		  bool ok = true;
		  WEBP_CSP_MODE mode = this.colorspace;
		  int width = this.width;
		  int height = this.height;
		  if (!IsValidColorspace(mode)) {
			ok = 0;
		  } else if (mode >= WEBP_CSP_MODE.MODE_YUV) {   // YUV checks
			WebPYUVABuffer buf = this.YUVA;
			int size = buf.y_stride * height;
			int u_size = buf.u_stride * ((height + 1) / 2);
			int v_size = buf.v_stride * ((height + 1) / 2);
			int a_size = buf.a_stride * height;
			ok &= (size <= buf.y_size);
			ok &= (u_size <= buf.u_size);
			ok &= (v_size <= buf.v_size);
			ok &= (a_size <= buf.a_size);
			ok &= (buf.y_stride >= width);
			ok &= (buf.u_stride >= (width + 1) / 2);
			ok &= (buf.v_stride >= (width + 1) / 2);
			if (buf.a != null) {
			  ok &= (buf.a_stride >= width);
			}
		  } else {    // RGB checks
			WebPRGBABuffer buf = this.RGBA;
			ok &= (buf.stride * height <= buf.size);
			ok &= (buf.stride >= width * kModeBpp[(int)mode]);
		  }
		  return ok ? VP8StatusCode.VP8_STATUS_OK : VP8StatusCode.VP8_STATUS_INVALID_PARAM;
		}

		VP8StatusCode AllocateBuffer() {
		  int w = this.width;
		  int h = this.height;
		  WEBP_CSP_MODE mode = this.colorspace;

		  if (w <= 0 || h <= 0 || !IsValidColorspace(mode)) {
			return VP8StatusCode.VP8_STATUS_INVALID_PARAM;
		  }

		  if (!this.is_external_memory && (this.private_memory == null)) {
			byte* output;
			int uv_stride = 0, a_stride = 0;
			int uv_size = 0;
			ulong a_size = 0, total_size;
			// We need memory and it hasn't been allocated yet.
			// => initialize output buffer, now that dimensions are known.
			int stride = w * kModeBpp[mode];
			ulong size = (ulong)stride * h;

			if (mode >= WEBP_CSP_MODE.MODE_YUV) {
			  uv_stride = (w + 1) / 2;
			  uv_size = (ulong)uv_stride * ((h + 1) / 2);
			  if (mode == WEBP_CSP_MODE.MODE_YUVA) {
				a_stride = w;
				a_size = (ulong)a_stride * h;
			  }
			}
			total_size = size + 2 * uv_size + a_size;

			// Security/sanity checks
			if (((uint)total_size != total_size) || (total_size >= (1UL << 40))) {
			  return VP8StatusCode.VP8_STATUS_INVALID_PARAM;
			}

			this.private_memory = output = (byte*)Global.malloc((uint)total_size);
			if (output == null) {
			  return VP8StatusCode.VP8_STATUS_OUT_OF_MEMORY;
			}

			// YUVA initialization
			if (mode >= WEBP_CSP_MODE.MODE_YUV)
			{
			  WebPYUVABuffer buf = this.YUVA;
			  buf.y = output;
			  buf.y_stride = stride;
			  buf.y_size = (int)size;
			  buf.u = output + size;
			  buf.u_stride = uv_stride;
			  buf.u_size = uv_size;
			  buf.v = output + size + uv_size;
			  buf.v_stride = uv_stride;
			  buf.v_size = uv_size;
			  if (mode == WEBP_CSP_MODE.MODE_YUVA) {
				buf.a = output + size + 2 * uv_size;
			  }
			  buf.a_size = (int)a_size;
			  buf.a_stride = a_stride;
			} else {  // RGBA initialization
			  WebPRGBABuffer buf = this.RGBA;
			  buf.rgba = output;
			  buf.stride = stride;
			  buf.size = (int)size;
			}
		  }
		  return this.CheckDecBuffer();
		}

		VP8StatusCode WebPAllocateDecBuffer(int w, int h, WebPDecoderOptions options, WebPDecBuffer _out) {
		  if (_out == null || w <= 0 || h <= 0) {
			return VP8_STATUS_INVALID_PARAM;
		  }
		  if (options != null) {    // First, apply options if there is any.
			if (options.use_cropping) {
			  int cw = options.crop_width;
			  int ch = options.crop_height;
			  int x = options.crop_left & ~1;
			  int y = options.crop_top & ~1;
			  if (x < 0 || y < 0 || cw <= 0 || ch <= 0 || x + cw > w || y + ch > h) {
				return VP8_STATUS_INVALID_PARAM;   // out of frame boundary.
			  }
			  w = cw;
			  h = ch;
			}
			if (options.use_scaling) {
			  if (options.scaled_width <= 0 || options.scaled_height <= 0) {
				return VP8_STATUS_INVALID_PARAM;
			  }
			  w = options.scaled_width;
			  h = options.scaled_height;
			}
		  }
		  _out.width = w;
		  _out.height = h;

		  // Then, allocate buffer for real
		  return _out.AllocateBuffer();
		}

		//------------------------------------------------------------------------------
		// constructors / destructors

		int WebPInitDecBufferInternal(int version) {
		  if (version != WEBP_DECODER_ABI_VERSION) return 0;  // version mismatch
		  if (buffer == null) return 0;
		  Global.memset(buffer, 0, sizeof(*buffer));
		  return 1;
		}

		void WebPFreeDecBuffer() {
		  if (buffer != null) {
			if (!this.is_external_memory)
				Global.free(this.private_memory);
			this.private_memory = null;
		  }
		}

		static void WebPCopyDecBuffer(WebPDecBuffer src, WebPDecBuffer dst) {
			throw(new NotImplementedException("Should copy every field"));
			/*
			if (src != null && dst != null) {
				*dst = *src;
				if (src.private_memory != null) {
					dst.is_external_memory = 1;   // dst buffer doesn't own the memory.
					dst.private_memory = null;
				}
			}
			*/
		}

		/// <summary>
		/// Copy and transfer ownership from src to dst (beware of parameter order!)
		/// </summary>
		/// <param name="src"></param>
		/// <param name="dst"></param>
		void WebPGrabDecBuffer(WebPDecBuffer src, WebPDecBuffer dst) {
			throw (new NotImplementedException("Should copy every field"));
			/*
			if (src != null && dst != null)
			{
			*dst = *src;
			if (src.private_memory != null) {
			  src.is_external_memory = 1;   // src relinquishes ownership
			  src.private_memory = null;
			}
			*/
		  }
		
	}
}
#endif
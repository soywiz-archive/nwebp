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
		// WebPDecParams: Decoding output parameters. Transient internal object.

		//typedef int (*OutputFunc)(VP8Io* io, WebPDecParams* p);

		// Structure use for on-the-fly rescaling
		struct WebPRescaler
		{
		  int x_expand;               // true if we're expanding in the x direction
		  int fy_scale, fx_scale;     // fixed-point scaling factor
		  long fxy_scale;          // ''
		  // we need hpel-precise add/sub increments, for the downsampled U/V planes.
		  int y_accum;                // vertical accumulator
		  int y_add, y_sub;           // vertical increments (add ~= src, sub ~= dst)
		  int x_add, x_sub;           // horizontal increments (add ~= src, sub ~= dst)
		  int src_width, src_height;  // source dimensions
		  int dst_width, dst_height;  // destination dimensions
		  byte* dst;
		  int dst_stride;
		  int* irow, *frow;       // work buffer
		}

		struct WebPDecParams
		{
		  WebPDecBuffer* output;             // output buffer.
		  byte* tmp_y, *tmp_u, *tmp_v;    // cache for the fancy upsampler
											 // or used for tmp rescaling

		  int last_y;                 // coordinate of the line that was last output
		  WebPDecoderOptions* options;  // if not null, use alt decoding features
		  // rescalers
		  WebPRescaler scaler_y, scaler_u, scaler_v, scaler_a;
		  void* memory;               // overall scratch memory for the output work.
		  OutputFunc emit;            // output RGB or YUV samples
		  OutputFunc emit_alpha;      // output alpha channel
		};

		// Should be called first, before any use of the WebPDecParams object.
		void WebPResetDecParams(WebPDecParams* params);

		//------------------------------------------------------------------------------
		// Header parsing helpers

		int TAG_SIZE = 4;
		int CHUNK_HEADER_SIZE = 8;
		int RIFF_HEADER_SIZE = 12;
		int FRAME_CHUNK_SIZE = 20;
		int LOOP_CHUNK_SIZE = 4;
		int TILE_CHUNK_SIZE = 8;
		int VP8X_CHUNK_SIZE = 12;
		int VP8_FRAME_HEADER_SIZE = 10;  // Size of the frame header within VP8 data.

		//------------------------------------------------------------------------------


	}
}
#endif

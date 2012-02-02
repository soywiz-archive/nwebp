using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NWebp.Internal.dec
{
	public partial class Internal
	{
		const byte* VP8DecompressAlphaRows(VP8Decoder* dec, int row, int num_rows) {
		  const int stride = dec->pic_hdr_.width_;

		  if (row < 0 || num_rows < 0 || row + num_rows > dec->pic_hdr_.height_) {
			return NULL;    // sanity check.
		  }

		  if (row == 0) {
			// Decode everything during the first call.
			if (!DecodeAlpha(dec->alpha_data_, (uint)dec->alpha_data_size_,
							 dec->pic_hdr_.width_, dec->pic_hdr_.height_, stride,
							 dec->alpha_plane_)) {
			  return NULL;  // Error.
			}
		  }

		  // Return a pointer to the current decoded row.
		  return dec->alpha_plane_ + row * stride;
		}
	}
}

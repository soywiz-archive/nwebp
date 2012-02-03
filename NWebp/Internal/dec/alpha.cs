using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NWebp.Internal
{
	unsafe partial class VP8Decoder
	{
		byte* VP8DecompressAlphaRows(int row, int num_rows) {
		  int stride = this.pic_hdr_.width_;

		  if (row < 0 || num_rows < 0 || row + num_rows > this.pic_hdr_.height_) {
			return null;    // sanity check.
		  }

		  if (row == 0) {
			// Decode everything during the first call.
			if (!DecodeAlpha(
				this.alpha_data_, (uint)this.alpha_data_size_,
				this.pic_hdr_.width_, this.pic_hdr_.height_, stride,
				this.alpha_plane_
			)) {
			  return null;  // Error.
			}
		  }

		  // Return a pointer to the current decoded row.
		  return this.alpha_plane_ + row * stride;
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NWebp.Internal.dec
{
	public partial class Internal
	{
		int VP8DecodeLayer(VP8Decoder* const dec) {
		  assert(dec);
		  assert(dec->layer_data_size_ > 0);
		  (void)dec;

		  // TODO: handle enhancement layer here.

		  return 1;
		}
	}
}

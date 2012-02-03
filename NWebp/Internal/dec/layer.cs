using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NWebp.Internal
{
	public partial class Internal
	{
		int VP8DecodeLayer(VP8Decoder* dec) {
		  assert(dec);
		  assert(dec.layer_data_size_ > 0);
		  (void)dec;

		  // TODO: handle enhancement layer here.

		  return 1;
		}
	}
}

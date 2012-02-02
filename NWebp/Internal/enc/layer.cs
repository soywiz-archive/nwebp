using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NWebp.Internal.enc
{
	public partial class VP8Encoder
	{
		void VP8EncInitLayer()
		{
		  this.use_layer_ = (this.pic_->u0 != NULL);
		  this.layer_data_size_ = 0;
		  this.layer_data_ = NULL;
		  if (this.use_layer_) {
			VP8BitWriterInit(&this.layer_bw_, this.mb_w_ * this.mb_h_* 3);
		  }
		}

		int VP8EncFinishLayer()
		{
		  if (this.use_layer_) {
			this.layer_data_ = VP8BitWriterFinish(&this.layer_bw_);
			this.layer_data_size_ = VP8BitWriterSize(&this.layer_bw_);
		  }
		  return 1;
		}

		void VP8EncDeleteLayer() {
		  free(this.layer_data_);
		}
	}

	public partial class VP8EncIterator
	{
		void VP8EncCodeLayerBlock() {
		  (void)this;   // remove a warning
		}
	}
}

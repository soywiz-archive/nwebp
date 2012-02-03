using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NWebp.Internal
{
	public partial class VP8Encoder
	{
		void VP8EncInitAlpha()
		{
			this.has_alpha_ = (this.pic_.a != null);
			this.alpha_data_ = null;
			this.alpha_data_size_ = 0;
		}

		int VP8EncFinishAlpha()
		{
			if (this.has_alpha_)
			{
				WebPConfig* config = this.config_;
				WebPPicture* pic = this.pic_;
				byte* tmp_data = null;
				uint tmp_size = 0;
				WEBP_FILTER_TYPE filter =
					(config.alpha_filtering == 0) ? WEBP_FILTER_NONE :
					(config.alpha_filtering == 1) ? WEBP_FILTER_FAST :
													 WEBP_FILTER_BEST;

				assert(pic.a);
				if (!EncodeAlpha(pic.a, pic.width, pic.height, pic.a_stride,
								 config.alpha_quality, config.alpha_compression,
								 filter, &tmp_data, &tmp_size))
				{
					return 0;
				}
				if (tmp_size != (uint)tmp_size)
				{  // Sanity check.
					free(tmp_data);
					return 0;
				}
				this.alpha_data_size_ = (uint)tmp_size;
				this.alpha_data_ = tmp_data;
			}
			return this.WebPReportProgress(this.percent_ + 20);
		}

		void VP8EncDeleteAlpha()
		{
			free(this.alpha_data_);
			this.alpha_data_ = null;
			this.alpha_data_size_ = 0;
			this.has_alpha_ = 0;
		}
	}
}

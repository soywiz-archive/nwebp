﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NWebp.Internal.enc
{
	class alpha
	{

		//------------------------------------------------------------------------------

		void VP8EncInitAlpha(VP8Encoder* enc)
		{
			enc->has_alpha_ = (enc->pic_->a != NULL);
			enc->alpha_data_ = NULL;
			enc->alpha_data_size_ = 0;
		}

		int VP8EncFinishAlpha(VP8Encoder* enc)
		{
			if (enc->has_alpha_)
			{
				const WebPConfig* config = enc->config_;
				const WebPPicture* pic = enc->pic_;
				byte* tmp_data = NULL;
				size_t tmp_size = 0;
				const WEBP_FILTER_TYPE filter =
					(config->alpha_filtering == 0) ? WEBP_FILTER_NONE :
					(config->alpha_filtering == 1) ? WEBP_FILTER_FAST :
													 WEBP_FILTER_BEST;

				assert(pic->a);
				if (!EncodeAlpha(pic->a, pic->width, pic->height, pic->a_stride,
								 config->alpha_quality, config->alpha_compression,
								 filter, &tmp_data, &tmp_size))
				{
					return 0;
				}
				if (tmp_size != (uint)tmp_size)
				{  // Sanity check.
					free(tmp_data);
					return 0;
				}
				enc->alpha_data_size_ = (uint)tmp_size;
				enc->alpha_data_ = tmp_data;
			}
			return WebPReportProgress(enc, enc->percent_ + 20);
		}

		void VP8EncDeleteAlpha(VP8Encoder* enc)
		{
			free(enc->alpha_data_);
			enc->alpha_data_ = NULL;
			enc->alpha_data_size_ = 0;
			enc->has_alpha_ = 0;
		}


	}
}
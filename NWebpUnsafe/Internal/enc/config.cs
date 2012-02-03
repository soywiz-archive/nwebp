using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#if false
namespace NWebp.Internal
{
	public partial class WebPConfig
	{
		public int WebPConfigInitInternal(WebPPreset preset, float quality, int version)
		{
			if (version != Global.WEBP_ENCODER_ABI_VERSION)
			{
				return 0;   // caller/system version mismatch!
			}

			this.quality = quality;
			this.target_size = 0;
			this.target_PSNR = 0.0f;
			this.method = 4;
			this.sns_strength = 50;
			this.filter_strength = 20;   // default: light filtering
			this.filter_sharpness = 0;
			this.filter_type = 0;        // default: simple
			this.partitions = 0;
			this.segments = 4;
			this.pass = 1;
			this.show_compressed = 0;
			this.preprocessing = 0;
			this.autofilter = 0;
			this.partition_limit = 0;
			this.alpha_compression = 1;
			this.alpha_filtering = 1;
			this.alpha_quality = 100;

			// TODO(skal): tune.
			switch (preset)
			{
				case WebPPreset.WEBP_PRESET_PICTURE:
					this.sns_strength = 80;
					this.filter_sharpness = 4;
					this.filter_strength = 35;
					break;
				case WebPPreset.WEBP_PRESET_PHOTO:
					this.sns_strength = 80;
					this.filter_sharpness = 3;
					this.filter_strength = 30;
					break;
				case WebPPreset.WEBP_PRESET_DRAWING:
					this.sns_strength = 25;
					this.filter_sharpness = 6;
					this.filter_strength = 10;
					break;
				case WebPPreset.WEBP_PRESET_ICON:
					this.sns_strength = 0;
					this.filter_strength = 0;   // disable filtering to retain sharpness
					break;
				case WebPPreset.WEBP_PRESET_TEXT:
					this.sns_strength = 0;
					this.filter_strength = 0;   // disable filtering to retain sharpness
					this.segments = 2;
					break;
				case WebPPreset.WEBP_PRESET_DEFAULT:
				default:
					break;
			}
			return this.WebPValidateConfig();
		}

		public int WebPValidateConfig()
		{
			if (this.quality < 0 || this.quality > 100) return 0;
			if (this.target_size < 0) return 0;
			if (this.target_PSNR < 0) return 0;
			if (this.method < 0 || this.method > 6) return 0;
			if (this.segments < 1 || this.segments > 4) return 0;
			if (this.sns_strength < 0 || this.sns_strength > 100) return 0;
			if (this.filter_strength < 0 || this.filter_strength > 100) return 0;
			if (this.filter_sharpness < 0 || this.filter_sharpness > 7) return 0;
			if (this.filter_type < 0 || this.filter_type > 1) return 0;
			if (this.autofilter < 0 || this.autofilter > 1) return 0;
			if (this.pass < 1 || this.pass > 10) return 0;
			if (this.show_compressed < 0 || this.show_compressed > 1) return 0;
			if (this.preprocessing < 0 || this.preprocessing > 1) return 0;
			if (this.partitions < 0 || this.partitions > 3) return 0;
			if (this.partition_limit < 0 || this.partition_limit > 100) return 0;
			if (this.alpha_compression < 0) return 0;
			if (this.alpha_filtering < 0) return 0;
			if (this.alpha_quality < 0 || this.alpha_quality > 100) return 0;
			return 1;
		}
	}
}
#endif

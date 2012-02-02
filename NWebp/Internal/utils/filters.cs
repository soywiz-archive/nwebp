using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NWebp.Internal
{
	unsafe class filters
	{

		// Filters.
		enum WEBP_FILTER_TYPE
		{
			WEBP_FILTER_NONE = 0,
			WEBP_FILTER_HORIZONTAL,
			WEBP_FILTER_VERTICAL,
			WEBP_FILTER_GRADIENT,
			WEBP_FILTER_LAST = WEBP_FILTER_GRADIENT + 1,  // end marker
			WEBP_FILTER_BEST,
			WEBP_FILTER_FAST
		}

		delegate void WebPFilterFunc(byte* _in, int width, int height, int bpp, int stride, byte* _out);

		/*
		// Filter the given data using the given predictor.
		// 'in' corresponds to a 2-dimensional pixel array of size (stride * height)
		// in raster order.
		// 'bpp' is number of bytes per pixel, and
		// 'stride' is number of bytes per scan line (with possible padding).
		// 'out' should be pre-allocated.
		extern const WebPFilterFunc WebPFilters[(int)WEBP_FILTER_TYPE.WEBP_FILTER_LAST];

		// Reconstruct the original data from the given filtered data.
		extern const WebPFilterFunc WebPUnfilters[(int)WEBP_FILTER_TYPE.WEBP_FILTER_LAST];

		// Fast estimate of a potentially good filter.
		extern WEBP_FILTER_TYPE EstimateBestFilter(byte* data, int width, int height, int stride);
		*/


		//------------------------------------------------------------------------------
		// Helpful macro.

		/*
		# define SANITY_CHECK(in, out)                              \
		*/

		static void SANITY_CHECK(byte* _in, byte* _out, int width, int height, int bpp, int stride)
		{
			Global.assert(_in != null);
			Global.assert(_out != null);
			Global.assert(width > 0);
			Global.assert(height > 0);
			Global.assert(bpp > 0);
			Global.assert(stride >= width * bpp);
		}

		static void PredictLine(byte* src, byte* pred, byte* dst, int length, bool inverse)
		{
			int i;
			if (inverse)
			{
				for (i = 0; i < length; ++i) dst[i] = (byte)(src[i] + pred[i]);
			}
			else
			{
				for (i = 0; i < length; ++i) dst[i] = (byte)(src[i] - pred[i]);
			}
		}

		//------------------------------------------------------------------------------
		// Horizontal filter.

		static void DoHorizontalFilter(byte* _in, int width, int height, int bpp, int stride, bool inverse, byte* _out)
		{
			int h;
			byte* preds = (inverse ? _out : _in);
			SANITY_CHECK(_in, _out, width, height, bpp, stride);

			// Filter line-by-line.
			for (h = 0; h < height; ++h)
			{
				// Leftmost pixel is predicted from above (except for topmost scanline).
				if (h == 0)
				{
					Global.memcpy((void*)_out, (void*)_in, bpp);
				}
				else
				{
					PredictLine(_in, preds - stride, _out, bpp, inverse);
				}
				PredictLine(_in + bpp, preds, _out + bpp, bpp * (width - 1), inverse);
				preds += stride;
				_in += stride;
				_out += stride;
			}
		}

		static void HorizontalFilter(byte* data, int width, int height, int bpp, int stride, byte* filtered_data)
		{
			DoHorizontalFilter(data, width, height, bpp, stride, false, filtered_data);
		}

		static void HorizontalUnfilter(byte* data, int width, int height, int bpp, int stride, byte* recon_data)
		{
			DoHorizontalFilter(data, width, height, bpp, stride, true, recon_data);
		}

		//------------------------------------------------------------------------------
		// Vertical filter.

		static void DoVerticalFilter(byte* _in, int width, int height, int bpp, int stride, bool inverse, byte* _out)
		{
			int h;
			byte* preds = (inverse ? _out : _in);
			SANITY_CHECK(_in, _out, width, height, bpp, stride);

			// Very first top-left pixel is copied.
			Global.memcpy((void*)_out, (void*)_in, bpp);
			// Rest of top scan-line is left-predicted.
			PredictLine(_in + bpp, preds, _out + bpp, bpp * (width - 1), inverse);

			// Filter line-by-line.
			for (h = 1; h < height; ++h)
			{
				_in += stride;
				_out += stride;
				PredictLine(_in, preds, _out, bpp * width, inverse);
				preds += stride;
			}
		}

		static void VerticalFilter(byte* data, int width, int height, int bpp, int stride, byte* filtered_data)
		{
			DoVerticalFilter(data, width, height, bpp, stride, false, filtered_data);
		}

		static void VerticalUnfilter(byte* data, int width, int height, int bpp, int stride, byte* recon_data)
		{
			DoVerticalFilter(data, width, height, bpp, stride, true, recon_data);
		}

		//------------------------------------------------------------------------------
		// Gradient filter.

		static int GradientPredictor(byte a, byte b, byte c)
		{
			int g = a + b - c;
			return (g < 0) ? 0 : (g > 255) ? 255 : g;
		}

		static void DoGradientFilter(byte* _in, int width, int height, int bpp, int stride, bool inverse, byte* _out)
		{
			byte* preds = (inverse ? _out : _in);
			int h;
			SANITY_CHECK(_in, _out, width, height, bpp, stride);

			// left prediction for top scan-line
			Global.memcpy((void*)_out, (void*)_in, bpp);
			PredictLine(_in + bpp, preds, _out + bpp, bpp * (width - 1), inverse);

			// Filter line-by-line.
			for (h = 1; h < height; ++h)
			{
				int w;
				preds += stride;
				_in += stride;
				_out += stride;
				// leftmost pixel: predict from above.
				PredictLine(_in, preds - stride, _out, bpp, inverse);
				for (w = bpp; w < width * bpp; ++w)
				{
					int pred = GradientPredictor(preds[w - bpp], preds[w - stride], preds[w - stride - bpp]);
					_out[w] = (byte)(_in[w] + (inverse ? pred : -pred));
				}
			}
		}

		static void GradientFilter(byte* data, int width, int height, int bpp, int stride, byte* filtered_data)
		{
			DoGradientFilter(data, width, height, bpp, stride, false, filtered_data);
		}

		static void GradientUnfilter(byte* data, int width, int height, int bpp, int stride, byte* recon_data)
		{
			DoGradientFilter(data, width, height, bpp, stride, true, recon_data);
		}

		// -----------------------------------------------------------------------------
		// Quick estimate of a potentially interesting filter mode to try, in addition
		// to the default NONE.

		const int SMAX = 16;
		//#define SDIFF(a, b) (abs((a) - (b)) >> 4)   // Scoring diff, in [0..SMAX)
		static int SDIFF(int a, int b)
		{
			return (Math.Abs((a) - (b)) >> 4);
		}

		/*
		static int SDIFF(byte a, int b)
		{
			return (Math.Abs((a) - (b)) >> 4);
		}
		*/

		WEBP_FILTER_TYPE EstimateBestFilter(byte* data, int width, int height, int stride)
		{
			int i, j;
			int[,] bins = new int[(int)WEBP_FILTER_TYPE.WEBP_FILTER_LAST, SMAX];
			// We only sample every other pixels. That's enough.
			for (j = 2; j < height - 1; j += 2)
			{
				byte* p = data + j * stride;
				int mean = p[0];
				for (i = 2; i < width - 1; i += 2)
				{
					int diff0 = SDIFF(p[i], mean);
					int diff1 = SDIFF(p[i], p[i - 1]);
					int diff2 = SDIFF(p[i], p[i - width]);
					int grad_pred = GradientPredictor(p[i - 1], p[i - width], p[i - width - 1]);
					int diff3 = SDIFF(p[i], grad_pred);
					bins[(int)WEBP_FILTER_TYPE.WEBP_FILTER_NONE, diff0] = 1;
					bins[(int)WEBP_FILTER_TYPE.WEBP_FILTER_HORIZONTAL, diff1] = 1;
					bins[(int)WEBP_FILTER_TYPE.WEBP_FILTER_VERTICAL, diff2] = 1;
					bins[(int)WEBP_FILTER_TYPE.WEBP_FILTER_GRADIENT, diff3] = 1;
					mean = (3 * mean + p[i] + 2) >> 2;
				}
			}
			{
				WEBP_FILTER_TYPE filter, best_filter = WEBP_FILTER_TYPE.WEBP_FILTER_NONE;
				int best_score = 0x7fffffff;
				for (filter = WEBP_FILTER_TYPE.WEBP_FILTER_NONE; filter < WEBP_FILTER_TYPE.WEBP_FILTER_LAST; ++filter)
				{
					int score = 0;
					for (i = 0; i < SMAX; ++i)
					{
						if (bins[(int)filter, i] > 0)
						{
							score += i;
						}
					}
					if (score < best_score)
					{
						best_score = score;
						best_filter = filter;
					}
				}
				return best_filter;
			}
		}

		//------------------------------------------------------------------------------

		WebPFilterFunc[] WebPFilters = new WebPFilterFunc[(int)WEBP_FILTER_TYPE.WEBP_FILTER_LAST]
		{
		  null,              // WEBP_FILTER_NONE
		  HorizontalFilter,  // WEBP_FILTER_HORIZONTAL
		  VerticalFilter,    // WEBP_FILTER_VERTICAL
		  GradientFilter     // WEBP_FILTER_GRADIENT
		};

		WebPFilterFunc[] WebPUnfilters = new WebPFilterFunc[(int)WEBP_FILTER_TYPE.WEBP_FILTER_LAST]
		{
		  null,                // WEBP_FILTER_NONE
		  HorizontalUnfilter,  // WEBP_FILTER_HORIZONTAL
		  VerticalUnfilter,    // WEBP_FILTER_VERTICAL
		  GradientUnfilter     // WEBP_FILTER_GRADIENT
		};

		//------------------------------------------------------------------------------



	}
}

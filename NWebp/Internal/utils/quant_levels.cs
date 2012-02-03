using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NWebp.Internal
{
	unsafe class quant_levels
	{

		int NUM_SYMBOLS = 256;

		/// <summary>
		/// Maximum number of convergence steps.
		/// </summary>
		int MAX_ITER = 6;

		/// <summary>
		/// MSE stopping criterion.
		/// </summary>
		double ERROR_THRESHOLD = 1e-4;

		// Replace the input 'data' of size 'width'x'height' with 'num-levels'
		// quantized values. If not null, 'mse' will contain the mean-squared error.
		// Valid range for 'num_levels' is [2, 256].
		// Returns false in case of error (data is null, or parameters are invalid).
		/// <summary>
		/// Quantize levels.
		/// </summary>
		/// <param name="data"></param>
		/// <param name="width"></param>
		/// <param name="height"></param>
		/// <param name="num_levels"></param>
		/// <param name="mse"></param>
		/// <returns></returns>
		static int QuantizeLevels(byte* data, int width, int height, int num_levels, float* mse)
		{
			var freq = new int[NUM_SYMBOLS];
			var q_level = new int[NUM_SYMBOLS];
			var inv_q_level = new double[NUM_SYMBOLS];
			int min_s = 255, max_s = 0;
			uint data_size = (uint)(height * width);
			uint n = 0;
			int s, num_levels_in, iter;
			double last_err = 1.0e38, err = 0.0;

			if (data == null)
			{
				return 0;
			}

			if (width <= 0 || height <= 0)
			{
				return 0;
			}

			if (num_levels < 2 || num_levels > 256)
			{
				return 0;
			}

			num_levels_in = 0;
			for (n = 0; n < data_size; ++n)
			{
				num_levels_in += (freq[data[n]] == 0) ? 1 : 0;
				if (min_s > data[n]) min_s = data[n];
				if (max_s < data[n]) max_s = data[n];
				++freq[data[n]];
			}

			if (num_levels_in <= num_levels)
			{
				if (mse != null) *mse = 0.0F;
				return 1;   // nothing to do !
			}

			// Start with uniformly spread centroids.
			for (s = 0; s < num_levels; ++s)
			{
				inv_q_level[s] = min_s + (double)(max_s - min_s) * s / (num_levels - 1);
			}

			// Fixed values. Won't be changed.
			q_level[min_s] = 0;
			q_level[max_s] = num_levels - 1;
			Global.assert(inv_q_level[0] == min_s);
			Global.assert(inv_q_level[num_levels - 1] == max_s);

			// k-Means iterations.
			for (iter = 0; iter < MAX_ITER; ++iter)
			{
				double err_count;
				var q_sum = new double[NUM_SYMBOLS];
				var q_count = new double[NUM_SYMBOLS];
				int slot = 0;

				// Assign classes to representatives.
				for (s = min_s; s <= max_s; ++s)
				{
					// Keep track of the nearest neighbour 'slot'
					while (slot < num_levels - 1 && 2 * s > inv_q_level[slot] + inv_q_level[slot + 1])
					{
						++slot;
					}
					if (freq[s] > 0)
					{
						q_sum[slot] += s * freq[s];
						q_count[slot] += freq[s];
					}
					q_level[s] = slot;
				}

				// Assign new representatives to classes.
				if (num_levels > 2)
				{
					for (slot = 1; slot < num_levels - 1; ++slot)
					{
						double count = q_count[slot];
						if (count > 0.0)
						{
							inv_q_level[slot] = q_sum[slot] / count;
						}
					}
				}

				// Compute convergence error.
				err = 0.0;
				err_count = 0.0;
				for (s = min_s; s <= max_s; ++s)
				{
					double error = s - inv_q_level[q_level[s]];
					err += freq[s] * error * error;
					err_count += freq[s];
				}
				if (err_count > 0.0) err /= err_count;

				// Check for convergence: we stop as soon as the error is no
				// longer improving.
				if (last_err - err < ERROR_THRESHOLD) break;
				last_err = err;
			}

			// Remap the alpha plane to quantized values.
			{
				// double.int rounding operation can be costly, so we do it
				// once for all before remapping. We also perform the data[] . slot
				// mapping, while at it (avoid one indirection in the final loop).
				var map = new byte[NUM_SYMBOLS];
				int s2;
				for (s2 = min_s; s2 <= max_s; ++s2)
				{
					int slot = q_level[s2];
					map[s2] = (byte)(inv_q_level[slot] + .5);
				}
				// Final pass.
				for (n = 0; n < data_size; ++n)
				{
					data[n] = map[data[n]];
				}
			}

			// Compute final mean squared error if needed.
			if (mse != null)
			{
				*mse = (float)Math.Sqrt(err);
			}

			return 1;
		}


	}
}

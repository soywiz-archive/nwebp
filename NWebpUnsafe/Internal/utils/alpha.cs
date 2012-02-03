using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#if false
namespace NWebp.Internal
{
	unsafe class alpha
	{
		const int MAX_SYMBOLS      = 255;
		const int ALPHA_HEADER_LEN = 2;

		/// <summary>
		/// Zlib-like encoding using TCoder
		/// </summary>
		struct Token
		{
			/// <summary>
			/// backward distance (=0 means: literal)
			/// </summary>
			int dist;
		  
			/// <summary>
			/// literal value (if dist = 0)
			/// </summary>
			int literal;

			/// <summary>
			/// length of matched string for non-literal
			/// </summary>
			uint len;     
		}

		/// <summary>
		/// 
		/// </summary>
		int MIN_LEN = 2;
		
		/// <summary>
		/// for deferred evaluation (0 = off)
		/// </summary>
		int DEFER_SKIP = 1;

		/*
		void CACHED_COST(coder, c) {
			return ((cost_cache[(c)] == 0.) ?      
		  (cost_cache[(c)] = lit_mode_cost + TCoderSymbolCost((coder), (c)))
		  : cost_cache[(c)]);
		}

		// Record symbol
		void RECORD(TOKEN) {
		  TCoderEncode(coderd, (TOKEN).dist, null);         
		  if ((TOKEN).dist == 0) {                          
			TCoderEncode(coder, (TOKEN).literal, null);     
		  } else {                                          
			TCoderEncode(coderl, (TOKEN).len - MIN_LEN, null);
		  }                                                   
		}
*/

		static uint GetLongestMatch(byte* data, byte* _ref, uint max_len) {
		  uint n;
		  for (n = 0; (n < max_len) && (data[n] == _ref[n]); ++n) { /* do nothing */ }
		  return n;
		}

		static int EncodeZlibTCoder(byte* data, int width, int height, VP8BitWriter bw) {
		  int ok = 0;
		  uint data_size = (uint)(width * height);
		  uint MAX_DIST = (uint)(3 * width);
		  uint MAX_LEN = (uint)(2 * width);
		  Token* msg = (Token*)malloc(data_size * sizeof(*msg));
		  int num_tokens;
		  TCoder* coder = TCoderNew(MAX_SYMBOLS);
		  TCoder* coderd = TCoderNew(MAX_DIST);
		  TCoder* coderl = TCoderNew(MAX_LEN - MIN_LEN);

		  if (coder == null || coderd == null || coderl == null) {
			goto End;
		  }
		  if (msg == null) {
			goto End;
		  }

		  {
			int deferred_eval = 0;
			uint n = 0;
			num_tokens = 0;
			while (n < data_size) {
			  double lit_mode_cost = TCoderSymbolCost(coderd, 0);
			  double cost_cache[MAX_SYMBOLS + 1] = { 0. };
			  Token best;
			  uint dist = 0;
			  double best_cost = CACHED_COST(coder, data[n]);
			  uint max_len = MAX_LEN;
			  if (max_len > data_size - n) {
				max_len = data_size - n;
			  }
			  best.dist = 0;
			  best.literal = data[n];
			  best.len = 1;
			  for (dist = 1; dist <= MAX_DIST && dist <= n; ++dist) {
				uint pos = n - dist;
				uint min_len = best.len - 1;
				uint len;

				// Early out: we probe at two locations for a quick match check
				if (data[pos] != data[n] ||
					data[pos + min_len] != data[n + min_len]) {
				  continue;
				}

				len = GetLongestMatch(data + pos, data + n, max_len);
				if (len >= MIN_LEN && len >= best.len) {
				  // This is the cost of the coding proposal
				  double cost = TCoderSymbolCost(coderl, len - MIN_LEN) + TCoderSymbolCost(coderd, dist);
				  // We're gaining an extra len-best.len coded message over the last
				  // known best. Compute how this would have cost if coded all literal.
				  // (TODO: we should fully re-evaluate at position best.len and not
				  // assume all is going be coded as literals. But it's at least an
				  // upper-bound (worst-case coding). Deferred evaluation used below
				  // partially addresses this.
				  double lit_cost = 0;
				  uint i;
				  for (i = best.len; i < len; ++i) {
					lit_cost += CACHED_COST(coder, data[n + i]);
				  }
				  // So, is it worth ?
				  if (best_cost + lit_cost >= cost) {
					best_cost = cost;
					best.len = len;
					best.dist = dist;
				  }
				}
				if (len >= max_len) {
				  break;  // No need to search further. We already got a max-long match
				}
			  }
			  // Deferred evaluation: before finalizing a choice we try to find
			  // best cost at position n + 1 and see if we get a longer
			  // match then current best. If so, we transform the current match
			  // into a literal, go to position n + 1, and try again.
			  {
				Token* cur = &msg[num_tokens];
				int forget = 0;
				if (deferred_eval) {
				  --cur;
				  // If the next match isn't longer, keep previous match
				  if (best.len <= cur.len) {
					deferred_eval = 0;
					n += cur.len - DEFER_SKIP;
					forget = 1;   // forget the new match
					RECORD(cur)
				  } else {   // else transform previous match into a shorter one
					cur.len = DEFER_SKIP;
					if (DEFER_SKIP == 1) {
					  cur.dist = 0;    // literal
					}
					// TODO(later): RECORD() macro should be changed to take an extra
					// "is_final" param, so that we could write the bitstream at once.
					RECORD(cur)
					++cur;
				  }
				}
				if (!forget) {
				  *cur = best;
				  ++num_tokens;
				  if (DEFER_SKIP > 0) {
					deferred_eval = (cur.len > 2) && (cur.len < MAX_LEN / 2);
				  }
				  if (deferred_eval) {
					// will probe at a later position before finalizing.
					n += DEFER_SKIP;
				  } else {
					// Keep the current choice.
					n += cur.len;
					RECORD(cur)
				  }
				}
			  }
			}
		  }

		  // Final bitstream assembly.
		  {
			int n;
			TCoderInit(coder);
			TCoderInit(coderd);
			TCoderInit(coderl);
			for (n = 0; n < num_tokens; ++n) {
			  Token* t = &msg[n];
			  int is_literal = (t.dist == 0);
			  TCoderEncode(coderd, t.dist, bw);
			  if (is_literal) {  // literal
				TCoderEncode(coder, t.literal, bw);
			  } else {
				TCoderEncode(coderl, t.len - MIN_LEN, bw);
			  }
			}
			ok = 1;
		  }

		 End:
		  if (coder) TCoderDelete(coder);
		  if (coderl) TCoderDelete(coderl);
		  if (coderd) TCoderDelete(coderd);
		  free(msg);
		  return ok && !bw.error_;
		}

		// -----------------------------------------------------------------------------

		static int EncodeAlphaInternal(byte* data, int width, int height,
									   int method, int filter, uint data_size,
									   byte* tmp_alpha, VP8BitWriter* bw) {
		  int ok = 0;
		  byte* alpha_src;
		  WebPFilterFunc filter_func;
		  byte header[ALPHA_HEADER_LEN];
		  uint expected_size = (method == 0) ?
				  (ALPHA_HEADER_LEN + data_size) : (data_size >> 5);
		  header[0] = (filter << 4) | method;
		  header[1] = 0;                // reserved byte for later use
		  VP8BitWriterInit(bw, expected_size);
		  VP8BitWriterAppend(bw, header, sizeof(header));

		  filter_func = WebPFilters[filter];
		  if (filter_func) {
			filter_func(data, width, height, 1, width, tmp_alpha);
			alpha_src = tmp_alpha;
		  }  else {
			alpha_src = data;
		  }

		  if (method == 0) {
			ok = VP8BitWriterAppend(bw, alpha_src, width * height);
			ok = ok && !bw.error_;
		  } else {
			ok = EncodeZlibTCoder(alpha_src, width, height, bw);
			VP8BitWriterFinish(bw);
		  }
		  return ok;
		}

		// -----------------------------------------------------------------------------

		// TODO(skal): move to dsp/ ?
		static void CopyPlane(byte* src, int src_stride,
							  byte* dst, int dst_stride, int width, int height) {
		  while (height-- > 0) {
			memcpy(dst, src, width);
			src += src_stride;
			dst += dst_stride;
		  }
		}

		// Encodes the given alpha data 'data' of size 'stride'x'height' via specified
		// compression method 'method'. The pre-processing (Quantization) is
		// performed if 'quality' is less than 100. For such cases, the encoding is
		// lossy. Valid ranges for 'quality' is [0, 100] and 'method' is [0, 1]:
		//   'method = 0' - No compression;
		//   'method = 1' - Backward reference counts encoded with arithmetic encoder;
		// 'filter' values [0, 4] correspond to prediction modes none, horizontal,
		// vertical & gradient filters. The prediction mode 4 will try all the
		// prediction modes (0 to 3) and pick the best prediction mode.

		// 'output' corresponds to the buffer containing compressed alpha data.
		//          This buffer is allocated by this method and caller should call
		//          free(*output) when done.
		// 'output_size' corresponds to size of this compressed alpha buffer.
		//
		// Returns 1 on successfully encoding the alpha and
		//         0 if either:
		//           invalid quality or method, or
		//           memory allocation for the compressed data fails.
		int EncodeAlpha(byte* data, int width, int height, int stride, int quality, int method, int filter, byte** output, uint* output_size)
		{
		  byte* quant_alpha = null;
		  uint data_size = height * width;
		  int ok = 1;

		  // quick sanity checks
		  assert(data != null && output != null && output_size != null);
		  assert(width > 0 && height > 0);
		  assert(stride >= width);
		  assert(filter >= WEBP_FILTER_NONE && filter <= WEBP_FILTER_FAST);

		  if (quality < 0 || quality > 100) {
			return 0;
		  }

		  if (method < 0 || method > 1) {
			return 0;
		  }

		  quant_alpha = (byte*)malloc(data_size);
		  if (quant_alpha == null) {
			return 0;
		  }

		  // Extract alpha data (width x height) from raw_data (stride x height).
		  CopyPlane(data, stride, quant_alpha, width, width, height);

		  if (quality < 100) {  // No Quantization required for 'quality = 100'.
			// 16 alpha levels gives quite a low MSE w.r.t original alpha plane hence
			// mapped to moderate quality 70. Hence Quality:[0, 70] . Levels:[2, 16]
			// and Quality:]70, 100] . Levels:]16, 256].
			int alpha_levels = (quality <= 70) ? (2 + quality / 5)
													 : (16 + (quality - 70) * 8);
			ok = QuantizeLevels(quant_alpha, width, height, alpha_levels, null);
		  }

		  if (ok) {
			VP8BitWriter bw;
			uint best_score;
			int test_filter;
			byte* filtered_alpha = null;

			// We always test WEBP_FILTER_NONE first.
			ok = EncodeAlphaInternal(quant_alpha, width, height, method,
									 WEBP_FILTER_NONE, data_size, null, &bw);
			if (!ok) {
			  VP8BitWriterWipeOut(&bw);
			  goto End;
			}
			best_score = VP8BitWriterSize(&bw);

			if (filter == WEBP_FILTER_FAST) {  // Quick estimate of a second candidate?
			  filter = EstimateBestFilter(quant_alpha, width, height, width);
			}
			// Stop?
			if (filter == WEBP_FILTER_NONE) {
			  goto Ok;
			}

			filtered_alpha = (byte*)malloc(data_size);
			ok = (filtered_alpha != null);
			if (!ok) {
			  goto End;
			}

			// Try the other mode(s).
			for (test_filter = WEBP_FILTER_HORIZONTAL;
				 ok && (test_filter <= WEBP_FILTER_GRADIENT);
				 ++test_filter) {
			  VP8BitWriter tmp_bw;
			  if (filter != WEBP_FILTER_BEST && test_filter != filter) {
				continue;
			  }

			  ok = EncodeAlphaInternal(quant_alpha, width, height, method, test_filter,
									   data_size, filtered_alpha, &tmp_bw);
			  if (ok) {
				uint score = VP8BitWriterSize(&tmp_bw);
				if (score < best_score) {
				  // swap bitwriter objects.
				  VP8BitWriter tmp = tmp_bw;
				  tmp_bw = bw;
				  bw = tmp;
				  best_score = score;
				}
			  } else {
				VP8BitWriterWipeOut(&bw);
			  }
			  VP8BitWriterWipeOut(&tmp_bw);
			}
		 Ok:
			if (ok) {
			  *output_size = VP8BitWriterSize(&bw);
			  *output = VP8BitWriterBuf(&bw);
			}
			free(filtered_alpha);
		  }
		 End:
		  free(quant_alpha);
		  return ok;
		}

		// -----------------------------------------------------------------------------
		// Alpha Decode.

		static int DecompressZlibTCoder(VP8BitReader* br, int width, byte* output, uint output_size)
		{
		  int ok = 1;
		  uint MAX_DIST = 3 * width;
		  uint MAX_LEN = 2 * width;
		  TCoder* coder = TCoderNew(MAX_SYMBOLS);
		  TCoder* coderd = TCoderNew(MAX_DIST);
		  TCoder* coderl = TCoderNew(MAX_LEN - MIN_LEN);

		  if (coder == null || coderd == null || coderl == null) {
			goto End;
		  }

		  {
			uint pos = 0;
			assert(br != null);
			while (pos < output_size && !br.eof_) {
			  uint dist = TCoderDecode(coderd, br);
			  if (dist == 0) {
				output[pos] = TCoderDecode(coder, br);
				++pos;
			  } else {
				uint len = MIN_LEN + TCoderDecode(coderl, br);
				uint k;
				if (pos + len > output_size || pos < dist) goto End;
				for (k = 0; k < len; ++k) {
				  output[pos + k] = output[pos + k - dist];
				}
				pos += len;
			  }
			}
			ok = !br.eof_;
		  }

		 End:
		  if (coder) TCoderDelete(coder);
		  if (coderl) TCoderDelete(coderl);
		  if (coderd) TCoderDelete(coderd);
		  return ok;
		}

		// -----------------------------------------------------------------------------


		// Decodes the compressed data 'data' of size 'data_size' into the 'output'.
		// The 'output' buffer should be pre-allocated and must be of the same
		// dimension 'height'x'stride', as that of the image.
		//
		// Returns 1 on successfully decoding the compressed alpha and
		//         0 if either:
		//           error in bit-stream header (invalid compression mode or filter), or
		//           error returned by appropriate compression method.
		int DecodeAlpha(byte* data, uint data_size, int width, int height, int stride, byte* output)
		{
		  byte* decoded_data = null;
		  uint decoded_size = height * width;
		  byte* unfiltered_data = null;
		  WEBP_FILTER_TYPE filter;
		  int ok = 0;
		  int method;

		  assert(width > 0 && height > 0 && stride >= width);
		  assert(data != null && output != null);

		  if (data_size <= ALPHA_HEADER_LEN) {
			return 0;
		  }

		  method = data[0] & 0x0f;
		  filter = data[0] >> 4;
		  ok = (data[1] == 0);
		  if (method < 0 || method > 1 ||
			  filter > WEBP_FILTER_GRADIENT || !ok) {
			return 0;
		  }

		  if (method == 0) {
			ok = (data_size >= decoded_size);
			decoded_data = (byte*)data + ALPHA_HEADER_LEN;
		  } else if (method == 1) {
			VP8BitReader br;
			decoded_data = (byte*)malloc(decoded_size);
			if (decoded_data == null) {
			  return 0;
			}
			VP8InitBitReader(&br, data + ALPHA_HEADER_LEN, data + data_size);
			ok = DecompressZlibTCoder(&br, width, decoded_data, decoded_size);
		  }
		  if (ok) {
			WebPFilterFunc unfilter_func = WebPUnfilters[filter];
			if (unfilter_func) {
			  unfiltered_data = (byte*)malloc(decoded_size);
			  if (unfiltered_data == null) {
				if (method == 1) free(decoded_data);
				return 0;
			  }
			  // TODO(vikas): Implement on-the-fly decoding & filter mechanism to decode
			  // and apply filter per image-row.
			  unfilter_func(decoded_data, width, height, 1, width, unfiltered_data);
			  // Construct raw_data (height x stride) from alpha data (height x width).
			  CopyPlane(unfiltered_data, width, output, stride, width, height);
			  free(unfiltered_data);
			} else {
			  // Construct raw_data (height x stride) from alpha data (height x width).
			  CopyPlane(decoded_data, width, output, stride, width, height);
			}
		  }
		  if (method == 1) {
			free(decoded_data);
		  }
		  return ok;
		}


	}
}
#endif

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#if false
namespace NWebp.Internal
{
	class webpenc
	{

		//------------------------------------------------------------------------------

		/// <summary>
		/// Return the encoder's version number, packed in hexadecimal using 8bits for
		/// each of major/minor/revision. E.g: v2.5.7 is 0x020507.
		/// </summary>
		/// <returns></returns>
		int WebPGetEncoderVersion() {
		  return (ENC_MAJ_VERSION << 16) | (ENC_MIN_VERSION << 8) | ENC_REV_VERSION;
		}

		//------------------------------------------------------------------------------
		// WebPPicture
		//------------------------------------------------------------------------------

		static int DummyWriter(byte* data, uint data_size,
							   WebPPicture* picture) {
		  // The following are to prevent 'unused variable' error message.
		  (void)data;
		  (void)data_size;
		  (void)picture;
		  return 1;
		}

		int WebPPictureInitInternal(WebPPicture* picture, int version) {
		  if (version != WEBP_ENCODER_ABI_VERSION) {
			return 0;   // caller/system version mismatch!
		  }
		  if (picture) {
			memset(picture, 0, sizeof(*picture));
			picture.writer = DummyWriter;
			WebPEncodingSetError(picture, VP8_ENC_OK);
		  }
		  return 1;
		}

		//------------------------------------------------------------------------------
		// VP8Encoder
		//------------------------------------------------------------------------------

		static void ResetSegmentHeader(VP8Encoder* enc) {
		  VP8SegmentHeader* hdr = &enc.segment_hdr_;
		  hdr.num_segments_ = enc.config_.segments;
		  hdr.update_map_  = (hdr.num_segments_ > 1);
		  hdr.size_ = 0;
		}

		static void ResetFilterHeader(VP8Encoder* enc) {
		  VP8FilterHeader* hdr = &enc.filter_hdr_;
		  hdr.simple_ = 1;
		  hdr.level_ = 0;
		  hdr.sharpness_ = 0;
		  hdr.i4x4_lf_delta_ = 0;
		}

		static void ResetBoundaryPredictions(VP8Encoder* enc) {
		  // init boundary values once for all
		  // Note: actually, initializing the preds_[] is only needed for intra4.
		  int i;
		  byte* top = enc.preds_ - enc.preds_w_;
		  byte* left = enc.preds_ - 1;
		  for (i = -1; i < 4 * enc.mb_w_; ++i) {
			top[i] = B_DC_PRED;
		  }
		  for (i = 0; i < 4 * enc.mb_h_; ++i) {
			left[i * enc.preds_w_] = B_DC_PRED;
		  }
		  enc.nz_[-1] = 0;   // constant
		}

		// Map configured quality level to coding tools used.
		//-------------+---+---+---+---+---+---+
		//   Quality   | 0 | 1 | 2 | 3 | 4 | 5 +
		//-------------+---+---+---+---+---+---+
		// dynamic prob| ~ | x | x | x | x | x |
		//-------------+---+---+---+---+---+---+
		// rd-opt modes|   |   | x | x | x | x |
		//-------------+---+---+---+---+---+---+
		// fast i4/i16 | x | x |   |   |   |   |
		//-------------+---+---+---+---+---+---+
		// rd-opt i4/16|   |   | x | x | x | x |
		//-------------+---+---+---+---+---+---+
		// Trellis     |   | x |   |   | x | x |
		//-------------+---+---+---+---+---+---+
		// full-SNS    |   |   |   |   |   | x |
		//-------------+---+---+---+---+---+---+

		static void MapConfigToTools(VP8Encoder* enc) {
		  int method = enc.config_.method;
		  int limit = 100 - enc.config_.partition_limit;
		  enc.method_ = method;
		  enc.rd_opt_level_ = (method >= 6) ? 3
							 : (method >= 5) ? 2
							 : (method >= 3) ? 1
							 : 0;
		  enc.max_i4_header_bits_ =
			  256 * 16 * 16 *                 // upper bound: up to 16bit per 4x4 block
			  (limit * limit) / (100 * 100);  // ... modulated with a quadratic curve.
		}

		// Memory scaling with dimensions:
		//  memory (bytes) ~= 2.25 * w + 0.0625 * w * h
		//
		// Typical memory footprint (768x510 picture)
		// Memory used:
		//              encoder: 33919
		//          block cache: 2880
		//                 info: 3072
		//                preds: 24897
		//          top samples: 1623
		//             non-zero: 196
		//             lf-stats: 2048
		//                total: 68635
		// Transcient object sizes:
		//       VP8EncIterator: 352
		//         VP8ModeScore: 912
		//       VP8SegmentInfo: 532
		//             VP8Proba: 31032
		//              LFStats: 2048
		// Picture size (yuv): 589824

		static VP8Encoder* InitEncoder(WebPConfig* config,
									   WebPPicture* picture) {
		  int use_filter =
			  (config.filter_strength > 0) || (config.autofilter > 0);
		  int mb_w = (picture.width + 15) >> 4;
		  int mb_h = (picture.height + 15) >> 4;
		  int preds_w = 4 * mb_w + 1;
		  int preds_h = 4 * mb_h + 1;
		  uint preds_size = preds_w * preds_h * sizeof(byte);
		  int top_stride = mb_w * 16;
		  uint nz_size = (mb_w + 1) * sizeof(uint);
		  uint cache_size = (3 * YUV_SIZE + PRED_SIZE) * sizeof(byte);
		  uint info_size = mb_w * mb_h * sizeof(VP8MBInfo);
		  uint samples_size = (2 * top_stride +         // top-luma/u/v
									   16 + 16 + 16 + 8 + 1 +   // left y/u/v
									   2 * ALIGN_CST)           // align all
									   * sizeof(byte);
		  uint lf_stats_size =
			  config.autofilter ? sizeof(LFStats) + ALIGN_CST : 0;
		  VP8Encoder* enc;
		  byte* mem;
		  uint size = sizeof(VP8Encoder) + ALIGN_CST  // main struct
					  + cache_size                      // working caches
					  + info_size                       // modes info
					  + preds_size                      // prediction modes
					  + samples_size                    // top/left samples
					  + nz_size                         // coeff context bits
					  + lf_stats_size;                  // autofilter stats

		#if PRINT_MEMORY_INFO
		  printf("===================================\n");
		  printf("Memory used:\n"
				 "             encoder: %ld\n"
				 "         block cache: %ld\n"
				 "                info: %ld\n"
				 "               preds: %ld\n"
				 "         top samples: %ld\n"
				 "            non-zero: %ld\n"
				 "            lf-stats: %ld\n"
				 "               total: %ld\n",
				 sizeof(VP8Encoder) + ALIGN_CST, cache_size, info_size,
				 preds_size, samples_size, nz_size, lf_stats_size, size);
		  printf("Transcient object sizes:\n"
				 "      VP8EncIterator: %ld\n"
				 "        VP8ModeScore: %ld\n"
				 "      VP8SegmentInfo: %ld\n"
				 "            VP8Proba: %ld\n"
				 "             LFStats: %ld\n",
				 sizeof(VP8EncIterator), sizeof(VP8ModeScore),
				 sizeof(VP8SegmentInfo), sizeof(VP8Proba),
				 sizeof(LFStats));
		  printf("Picture size (yuv): %ld\n",
				 mb_w * mb_h * 384 * sizeof(byte));
		  printf("===================================\n");
		#endif
		  mem = (byte*)malloc(size);
		  if (mem == null) {
			WebPEncodingSetError(picture, VP8_ENC_ERROR_OUT_OF_MEMORY);
			return null;
		  }
		  enc = (VP8Encoder*)mem;
		  mem = (byte*)DO_ALIGN(mem + sizeof(*enc));
		  memset(enc, 0, sizeof(*enc));
		  enc.num_parts_ = 1 << config.partitions;
		  enc.mb_w_ = mb_w;
		  enc.mb_h_ = mb_h;
		  enc.preds_w_ = preds_w;
		  enc.yuv_in_ = (byte*)mem;
		  mem += YUV_SIZE;
		  enc.yuv_out_ = (byte*)mem;
		  mem += YUV_SIZE;
		  enc.yuv_out2_ = (byte*)mem;
		  mem += YUV_SIZE;
		  enc.yuv_p_ = (byte*)mem;
		  mem += PRED_SIZE;
		  enc.mb_info_ = (VP8MBInfo*)mem;
		  mem += info_size;
		  enc.preds_ = ((byte*)mem) + 1 + enc.preds_w_;
		  mem += preds_w * preds_h * sizeof(byte);
		  enc.nz_ = 1 + (uint*)mem;
		  mem += nz_size;
		  enc.lf_stats_ = lf_stats_size ? (LFStats*)DO_ALIGN(mem) : null;
		  mem += lf_stats_size;

		  // top samples (all 16-aligned)
		  mem = (byte*)DO_ALIGN(mem);
		  enc.y_top_ = (byte*)mem;
		  enc.uv_top_ = enc.y_top_ + top_stride;
		  mem += 2 * top_stride;
		  mem = (byte*)DO_ALIGN(mem + 1);
		  enc.y_left_ = (byte*)mem;
		  mem += 16 + 16;
		  enc.u_left_ = (byte*)mem;
		  mem += 16;
		  enc.v_left_ = (byte*)mem;
		  mem += 8;

		  enc.config_ = config;
		  enc.profile_ = use_filter ? ((config.filter_type == 1) ? 0 : 1) : 2;
		  enc.pic_ = picture;
		  enc.percent_ = 0;

		  MapConfigToTools(enc);
		  VP8EncDspInit();
		  VP8DefaultProbas(enc);
		  ResetSegmentHeader(enc);
		  ResetFilterHeader(enc);
		  ResetBoundaryPredictions(enc);

		  VP8EncInitAlpha(enc);
		#if WEBP_EXPERIMENTAL_FEATURES
		  VP8EncInitLayer(enc);
		#endif

		  return enc;
		}

		static void DeleteEncoder(VP8Encoder* enc) {
		  if (enc) {
			VP8EncDeleteAlpha(enc);
		#if WEBP_EXPERIMENTAL_FEATURES
			VP8EncDeleteLayer(enc);
		#endif
			free(enc);
		  }
		}

		//------------------------------------------------------------------------------

		static double GetPSNR(ulong err, ulong size) {
		  return err ? 10. * log10(255. * 255. * size / err) : 99.;
		}

		static void FinalizePSNR(VP8Encoder* enc) {
		  WebPAuxStats* stats = enc.pic_.stats;
		  ulong size = enc.sse_count_;
		  ulong* sse = enc.sse_;
		  stats.PSNR[0] = (float)GetPSNR(sse[0], size);
		  stats.PSNR[1] = (float)GetPSNR(sse[1], size / 4);
		  stats.PSNR[2] = (float)GetPSNR(sse[2], size / 4);
		  stats.PSNR[3] = (float)GetPSNR(sse[0] + sse[1] + sse[2], size * 3 / 2);
		}

		static void StoreStats(VP8Encoder* enc) {
		  WebPAuxStats* stats = enc.pic_.stats;
		  if (stats) {
			int i, s;
			for (i = 0; i < NUM_MB_SEGMENTS; ++i) {
			  stats.segment_level[i] = enc.dqm_[i].fstrength_;
			  stats.segment_quant[i] = enc.dqm_[i].quant_;
			  for (s = 0; s <= 2; ++s) {
				stats.residual_bytes[s][i] = enc.residual_bytes_[s][i];
			  }
			}
			FinalizePSNR(enc);
			stats.coded_size = enc.coded_size_;
			for (i = 0; i < 3; ++i) {
			  stats.block_count[i] = enc.block_count_[i];
			}
		  }
		  WebPReportProgress(enc, 100);  // done!
		}

		int WebPEncodingSetError(WebPPicture* pic, WebPEncodingError error) {
		  assert((int)error < VP8_ENC_ERROR_LAST);
		  assert((int)error >= VP8_ENC_OK);
		  pic.error_code = error;
		  return 0;
		}

		int WebPReportProgress(VP8Encoder* enc, int percent) {
		  if (percent != enc.percent_) {
			WebPPicture* pic = enc.pic_;
			enc.percent_ = percent;
			if (pic.progress_hook && !pic.progress_hook(percent, pic)) {
			  // user abort requested
			  WebPEncodingSetError(pic, VP8_ENC_ERROR_USER_ABORT);
			  return 0;
			}
		  }
		  return 1;  // ok
		}
		//------------------------------------------------------------------------------

		int WebPEncode(WebPConfig* config, WebPPicture* pic) {
		  VP8Encoder* enc;
		  int ok;

		  if (pic == null)
			return 0;
		  WebPEncodingSetError(pic, VP8_ENC_OK);  // all ok so far
		  if (config == null)  // bad params
			return WebPEncodingSetError(pic, VP8_ENC_ERROR_NULL_PARAMETER);
		  if (!WebPValidateConfig(config))
			return WebPEncodingSetError(pic, VP8_ENC_ERROR_INVALID_CONFIGURATION);
		  if (pic.width <= 0 || pic.height <= 0)
			return WebPEncodingSetError(pic, VP8_ENC_ERROR_BAD_DIMENSION);
		  if (pic.y == null || pic.u == null || pic.v == null)
			return WebPEncodingSetError(pic, VP8_ENC_ERROR_NULL_PARAMETER);
		  if (pic.width > WEBP_MAX_DIMENSION || pic.height > WEBP_MAX_DIMENSION)
			return WebPEncodingSetError(pic, VP8_ENC_ERROR_BAD_DIMENSION);

		  enc = InitEncoder(config, pic);
		  if (enc == null) return 0;  // pic.error is already set.
		  // Note: each of the tasks below account for 20% in the progress report.
		  ok = VP8EncAnalyze(enc)
			&& VP8StatLoop(enc)
			&& VP8EncLoop(enc)
			&& VP8EncFinishAlpha(enc)
		#if WEBP_EXPERIMENTAL_FEATURES
			&& VP8EncFinishLayer(enc)
		#endif
			&& VP8EncWrite(enc);
		  StoreStats(enc);
		  if (!ok) {
			VP8EncFreeBitWriters(enc);
		  }
		  DeleteEncoder(enc);

		  return ok;
		}


	}
}
#endif

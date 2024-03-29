﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#if false
namespace NWebp.Internal
{
	class syntax
	{

		const int KSIGNATURE 0x9d012a
		const int MAX_PARTITION0_SIZE (1 << 19)   // max size of mode partition
		const int MAX_PARTITION_SIZE  (1 << 24)   // max size for token partition


		//------------------------------------------------------------------------------
		// Helper functions

		static void PutLE32(byte* data, uint val) {
		  data[0] = (val >>  0) & 0xff;
		  data[1] = (val >>  8) & 0xff;
		  data[2] = (val >> 16) & 0xff;
		  data[3] = (val >> 24) & 0xff;
		}

		static int IsVP8XNeeded(VP8Encoder* enc) {
		  return !!enc.has_alpha_;  // Currently the only case when VP8X is needed.
									 // This could change in the future.
		}

		static int PutPaddingByte(WebPPicture* pic) {

		  byte pad_byte[1] = { 0 };
		  return !!pic.writer(pad_byte, 1, pic);
		}

		//------------------------------------------------------------------------------
		// Writers for header's various pieces (in order of appearance)

		static WebPEncodingError PutRIFFHeader(VP8Encoder* enc,
											   uint riff_size) {
		  WebPPicture* pic = enc.pic_;
		  byte riff[RIFF_HEADER_SIZE] = {
			'R', 'I', 'F', 'F', 0, 0, 0, 0, 'W', 'E', 'B', 'P'
		  };
		  PutLE32(riff + TAG_SIZE, riff_size);
		  if (!pic.writer(riff, sizeof(riff), pic)) {
			return VP8_ENC_ERROR_BAD_WRITE;
		  }
		  return VP8_ENC_OK;
		}

		static WebPEncodingError PutVP8XHeader(VP8Encoder* enc) {
		  WebPPicture* pic = enc.pic_;
		  byte vp8x[CHUNK_HEADER_SIZE + VP8X_CHUNK_SIZE] = {
			'V', 'P', '8', 'X'
		  };
		  uint flags = 0;

		  assert(IsVP8XNeeded(enc));

		  if (enc.has_alpha_) {
			flags |= ALPHA_FLAG;
		  }

		  PutLE32(vp8x + TAG_SIZE,              VP8X_CHUNK_SIZE);
		  PutLE32(vp8x + CHUNK_HEADER_SIZE,     flags);
		  PutLE32(vp8x + CHUNK_HEADER_SIZE + 4, pic.width);
		  PutLE32(vp8x + CHUNK_HEADER_SIZE + 8, pic.height);
		  if(!pic.writer(vp8x, sizeof(vp8x), pic)) {
			return VP8_ENC_ERROR_BAD_WRITE;
		  }
		  return VP8_ENC_OK;
		}

		static WebPEncodingError PutAlphaChunk(VP8Encoder* enc) {
		  WebPPicture* pic = enc.pic_;
		  byte alpha_chunk_hdr[CHUNK_HEADER_SIZE] = {
			'A', 'L', 'P', 'H'
		  };

		  assert(enc.has_alpha_);

		  // Alpha chunk header.
		  PutLE32(alpha_chunk_hdr + TAG_SIZE, enc.alpha_data_size_);
		  if (!pic.writer(alpha_chunk_hdr, sizeof(alpha_chunk_hdr), pic)) {
			return VP8_ENC_ERROR_BAD_WRITE;
		  }

		  // Alpha chunk data.
		  if (!pic.writer(enc.alpha_data_, enc.alpha_data_size_, pic)) {
			return VP8_ENC_ERROR_BAD_WRITE;
		  }

		  // Padding.
		  if ((enc.alpha_data_size_ & 1) && !PutPaddingByte(pic)) {
			return VP8_ENC_ERROR_BAD_WRITE;
		  }
		  return VP8_ENC_OK;
		}

		static WebPEncodingError PutVP8Header(WebPPicture* pic,
											  uint vp8_size) {
		  byte vp8_chunk_hdr[CHUNK_HEADER_SIZE] = {
			'V', 'P', '8', ' '
		  };
		  PutLE32(vp8_chunk_hdr + TAG_SIZE, vp8_size);
		  if (!pic.writer(vp8_chunk_hdr, sizeof(vp8_chunk_hdr), pic)) {
			return VP8_ENC_ERROR_BAD_WRITE;
		  }
		  return VP8_ENC_OK;
		}

		static WebPEncodingError PutVP8FrameHeader(WebPPicture* pic,
												   int profile, uint size0) {
		  byte vp8_frm_hdr[VP8_FRAME_HEADER_SIZE];
		  uint bits;

		  if (size0 >= MAX_PARTITION0_SIZE) {  // partition #0 is too big to fit
			return VP8_ENC_ERROR_PARTITION0_OVERFLOW;
		  }

		  // Paragraph 9.1.
		  bits = 0                         // keyframe (1b)
			   | (profile << 1)            // profile (3b)
			   | (1 << 4)                  // visible (1b)
			   | ((uint)size0 << 5);   // partition length (19b)
		  vp8_frm_hdr[0] = (bits >>  0) & 0xff;
		  vp8_frm_hdr[1] = (bits >>  8) & 0xff;
		  vp8_frm_hdr[2] = (bits >> 16) & 0xff;
		  // signature
		  vp8_frm_hdr[3] = (KSIGNATURE >> 16) & 0xff;
		  vp8_frm_hdr[4] = (KSIGNATURE >>  8) & 0xff;
		  vp8_frm_hdr[5] = (KSIGNATURE >>  0) & 0xff;
		  // dimensions
		  vp8_frm_hdr[6] = pic.width & 0xff;
		  vp8_frm_hdr[7] = pic.width >> 8;
		  vp8_frm_hdr[8] = pic.height & 0xff;
		  vp8_frm_hdr[9] = pic.height >> 8;

		  if (!pic.writer(vp8_frm_hdr, sizeof(vp8_frm_hdr), pic)) {
			return VP8_ENC_ERROR_BAD_WRITE;
		  }
		  return VP8_ENC_OK;
		}

		// WebP Headers.
		static int PutWebPHeaders(VP8Encoder* enc, uint size0,
								  uint vp8_size, uint riff_size) {
		  WebPPicture* pic = enc.pic_;
		  WebPEncodingError err = VP8_ENC_OK;

		  // RIFF header.
		  err = PutRIFFHeader(enc, riff_size);
		  if (err != VP8_ENC_OK) goto Error;

		  // VP8X.
		  if (IsVP8XNeeded(enc)) {
			err = PutVP8XHeader(enc);
			if (err != VP8_ENC_OK) goto Error;
		  }

		  // Alpha.
		  if (enc.has_alpha_) {
			err = PutAlphaChunk(enc);
			if (err != VP8_ENC_OK) goto Error;
		  }

		  // VP8 header.
		  err = PutVP8Header(pic, vp8_size);
		  if (err != VP8_ENC_OK) goto Error;

		  // VP8 frame header.
		  err = PutVP8FrameHeader(pic, enc.profile_, size0);
		  if (err != VP8_ENC_OK) goto Error;

		  // All OK.
		  return 1;

		  // Error.
		 Error:
		  return WebPEncodingSetError(pic, err);
		}

		// Segmentation header
		static void PutSegmentHeader(VP8BitWriter* bw,
									 VP8Encoder* enc) {
		  VP8SegmentHeader* hdr = &enc.segment_hdr_;
		  VP8Proba* proba = &enc.proba_;
		  if (VP8PutBitUniform(bw, (hdr.num_segments_ > 1))) {
			// We always 'update' the quant and filter strength values
			int update_data = 1;
			int s;
			VP8PutBitUniform(bw, hdr.update_map_);
			if (VP8PutBitUniform(bw, update_data)) {
			  // we always use absolute values, not relative ones
			  VP8PutBitUniform(bw, 1);   // (segment_feature_mode = 1. Paragraph 9.3.)
			  for (s = 0; s < NUM_MB_SEGMENTS; ++s) {
				VP8PutSignedValue(bw, enc.dqm_[s].quant_, 7);
			  }
			  for (s = 0; s < NUM_MB_SEGMENTS; ++s) {
				VP8PutSignedValue(bw, enc.dqm_[s].fstrength_, 6);
			  }
			}
			if (hdr.update_map_) {
			  for (s = 0; s < 3; ++s) {
				if (VP8PutBitUniform(bw, (proba.segments_[s] != 255u))) {
				  VP8PutValue(bw, proba.segments_[s], 8);
				}
			  }
			}
		  }
		}

		// Filtering parameters header
		static void PutFilterHeader(VP8BitWriter* bw,
									VP8FilterHeader* hdr) {
		  int use_lf_delta = (hdr.i4x4_lf_delta_ != 0);
		  VP8PutBitUniform(bw, hdr.simple_);
		  VP8PutValue(bw, hdr.level_, 6);
		  VP8PutValue(bw, hdr.sharpness_, 3);
		  if (VP8PutBitUniform(bw, use_lf_delta)) {
			// '0' is the default value for i4x4_lf_delta_ at frame #0.
			int need_update = (hdr.i4x4_lf_delta_ != 0);
			if (VP8PutBitUniform(bw, need_update)) {
			  // we don't use ref_lf_delta => emit four 0 bits
			  VP8PutValue(bw, 0, 4);
			  // we use mode_lf_delta for i4x4
			  VP8PutSignedValue(bw, hdr.i4x4_lf_delta_, 6);
			  VP8PutValue(bw, 0, 3);    // all others unused
			}
		  }
		}

		// Nominal quantization parameters
		static void PutQuant(VP8BitWriter* bw,
							 VP8Encoder* enc) {
		  VP8PutValue(bw, enc.base_quant_, 7);
		  VP8PutSignedValue(bw, enc.dq_y1_dc_, 4);
		  VP8PutSignedValue(bw, enc.dq_y2_dc_, 4);
		  VP8PutSignedValue(bw, enc.dq_y2_ac_, 4);
		  VP8PutSignedValue(bw, enc.dq_uv_dc_, 4);
		  VP8PutSignedValue(bw, enc.dq_uv_ac_, 4);
		}

		// Partition sizes
		static int EmitPartitionsSize(VP8Encoder* enc,
									  WebPPicture* pic) {
		  byte buf[3 * (MAX_NUM_PARTITIONS - 1)];
		  int p;
		  for (p = 0; p < enc.num_parts_ - 1; ++p) {
			uint part_size = VP8BitWriterSize(enc.parts_ + p);
			if (part_size >= MAX_PARTITION_SIZE) {
			  return WebPEncodingSetError(pic, VP8_ENC_ERROR_PARTITION_OVERFLOW);
			}
			buf[3 * p + 0] = (part_size >>  0) & 0xff;
			buf[3 * p + 1] = (part_size >>  8) & 0xff;
			buf[3 * p + 2] = (part_size >> 16) & 0xff;
		  }
		  return p ? pic.writer(buf, 3 * p, pic) : 1;
		}

		//------------------------------------------------------------------------------

		#if WEBP_EXPERIMENTAL_FEATURES

		const int KTRAILER_SIZE 8

		static void PutLE24(byte* buf, uint value) {
		  buf[0] = (value >>  0) & 0xff;
		  buf[1] = (value >>  8) & 0xff;
		  buf[2] = (value >> 16) & 0xff;
		}

		static int WriteExtensions(VP8Encoder* enc) {
		  byte buffer[KTRAILER_SIZE];
		  VP8BitWriter* bw = &enc.bw_;
		  WebPPicture* pic = enc.pic_;

		  // Layer (bytes 0..3)
		  PutLE24(buffer + 0, enc.layer_data_size_);
		  buffer[3] = enc.pic_.colorspace & WEBP_CSP_UV_MASK;
		  if (enc.layer_data_size_ > 0) {
			assert(enc.use_layer_);
			// append layer data to last partition
			if (!VP8BitWriterAppend(&enc.parts_[enc.num_parts_ - 1],
									enc.layer_data_, enc.layer_data_size_)) {
			  return WebPEncodingSetError(pic, VP8_ENC_ERROR_BITSTREAM_OUT_OF_MEMORY);
			}
		  }

		  buffer[KTRAILER_SIZE - 1] = 0x01;  // marker
		  if (!VP8BitWriterAppend(bw, buffer, KTRAILER_SIZE)) {
			return WebPEncodingSetError(pic, VP8_ENC_ERROR_BITSTREAM_OUT_OF_MEMORY);
		  }
		  return 1;
		}

		#endif
		// WEBP_EXPERIMENTAL_FEATURES

		//------------------------------------------------------------------------------

		static uint GeneratePartition0(VP8Encoder* enc) {
		  VP8BitWriter* bw = &enc.bw_;
		  int mb_size = enc.mb_w_ * enc.mb_h_;
		  ulong pos1, pos2, pos3;
		#if WEBP_EXPERIMENTAL_FEATURES
		  int need_extensions = enc.use_layer_;
		#endif

		  pos1 = VP8BitWriterPos(bw);
		  VP8BitWriterInit(bw, mb_size * 7 / 8);        // ~7 bits per macroblock
		#if WEBP_EXPERIMENTAL_FEATURES
		  VP8PutBitUniform(bw, need_extensions);   // extensions
		#else
		  VP8PutBitUniform(bw, 0);   // colorspace
		#endif
		  VP8PutBitUniform(bw, 0);   // clamp type

		  PutSegmentHeader(bw, enc);
		  PutFilterHeader(bw, &enc.filter_hdr_);
		  VP8PutValue(bw, enc.config_.partitions, 2);
		  PutQuant(bw, enc);
		  VP8PutBitUniform(bw, 0);   // no proba update
		  VP8WriteProbas(bw, &enc.proba_);
		  pos2 = VP8BitWriterPos(bw);
		  VP8CodeIntraModes(enc);
		  VP8BitWriterFinish(bw);

		#if WEBP_EXPERIMENTAL_FEATURES
		  if (need_extensions && !WriteExtensions(enc)) {
			return 0;
		  }
		#endif

		  pos3 = VP8BitWriterPos(bw);

		  if (enc.pic_.stats) {
			enc.pic_.stats.header_bytes[0] = (int)((pos2 - pos1 + 7) >> 3);
			enc.pic_.stats.header_bytes[1] = (int)((pos3 - pos2 + 7) >> 3);
			enc.pic_.stats.alpha_data_size = (int)enc.alpha_data_size_;
			enc.pic_.stats.layer_data_size = (int)enc.layer_data_size_;
		  }
		  return !bw.error_;
		}

		void VP8EncFreeBitWriters(VP8Encoder* enc) {
		  int p;
		  VP8BitWriterWipeOut(&enc.bw_);
		  for (p = 0; p < enc.num_parts_; ++p) {
			VP8BitWriterWipeOut(enc.parts_ + p);
		  }
		}

		int VP8EncWrite(VP8Encoder* enc) {
		  WebPPicture* pic = enc.pic_;
		  VP8BitWriter* bw = &enc.bw_;
		  int task_percent = 19;
		  int percent_per_part = task_percent / enc.num_parts_;
		  int final_percent = enc.percent_ + task_percent;
		  int ok = 0;
		  uint vp8_size, pad, riff_size;
		  int p;

		  // Partition #0 with header and partition sizes
		  ok = !!GeneratePartition0(enc);

		  // Compute VP8 size
		  vp8_size = VP8_FRAME_HEADER_SIZE +
					 VP8BitWriterSize(bw) +
					 3 * (enc.num_parts_ - 1);
		  for (p = 0; p < enc.num_parts_; ++p) {
			vp8_size += VP8BitWriterSize(enc.parts_ + p);
		  }
		  pad = vp8_size & 1;
		  vp8_size += pad;

		  // Compute RIFF size
		  // At the minimum it is: "WEBPVP8 nnnn" + VP8 data size.
		  riff_size = TAG_SIZE + CHUNK_HEADER_SIZE + vp8_size;
		  if (IsVP8XNeeded(enc)) {  // Add size for: VP8X header + data.
			riff_size += CHUNK_HEADER_SIZE + VP8X_CHUNK_SIZE;
		  }
		  if (enc.has_alpha_) {  // Add size for: ALPH header + data.
			uint padded_alpha_size = enc.alpha_data_size_ +
											   (enc.alpha_data_size_ & 1);
			riff_size += CHUNK_HEADER_SIZE + padded_alpha_size;
		  }
		  // Sanity check.
		  if (riff_size > 0xfffffffeU) {
			return WebPEncodingSetError(pic, VP8_ENC_ERROR_FILE_TOO_BIG);
		  }

		  // Emit headers and partition #0
		  {
			byte* part0 = VP8BitWriterBuf(bw);
			uint size0 = VP8BitWriterSize(bw);
			ok = ok && PutWebPHeaders(enc, size0, vp8_size, riff_size)
					&& pic.writer(part0, size0, pic)
					&& EmitPartitionsSize(enc, pic);
			VP8BitWriterWipeOut(bw);    // will free the internal buffer.
		  }

		  // Token partitions
		  for (p = 0; p < enc.num_parts_; ++p) {
			byte* buf = VP8BitWriterBuf(enc.parts_ + p);
			uint size = VP8BitWriterSize(enc.parts_ + p);
			if (size)
			  ok = ok && pic.writer(buf, size, pic);
			VP8BitWriterWipeOut(enc.parts_ + p);    // will free the internal buffer.
			ok = ok && WebPReportProgress(enc, enc.percent_ + percent_per_part);
		  }

		  // Padding byte
		  if (ok && pad) {
			ok = PutPaddingByte(pic);
		  }

		  enc.coded_size_ = (int)(CHUNK_HEADER_SIZE + riff_size);
		  ok = ok && WebPReportProgress(enc, final_percent);
		  return ok;
		}

		//------------------------------------------------------------------------------


	}
}
#endif

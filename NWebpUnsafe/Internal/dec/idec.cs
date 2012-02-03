﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#if false
namespace NWebp.Internal
{
	public partial class Internal
	{
		int CHUNK_SIZE = 4096;
		int MAX_MB_SIZE = 4096;

		//------------------------------------------------------------------------------
		// Data structures for memory and states

		// Decoding states. State normally flows like HEADER.PARTS0.DATA.DONE.
		// If there is any error the decoder goes into state ERROR.
		enum DecState
		{
			STATE_PRE_VP8,  // All data before that of the first VP8 chunk.
			STATE_VP8_FRAME_HEADER,  // For VP8 Frame header (within VP8 chunk).
			STATE_VP8_PARTS0,
			STATE_VP8_DATA,
			STATE_DONE,
			STATE_ERROR
		}

		// Operating state for the MemBuffer
		enum MemBufferMode
		{
			MEM_MODE_NONE = 0,
			MEM_MODE_APPEND,
			MEM_MODE_MAP
		}

		// storage for partition #0 and partial data (in a rolling fashion)
		struct MemBuffer
		{
		  MemBufferMode mode_;  // Operation mode
		  uint start_;      // start location of the data to be decoded
		  uint end_;        // end location
		  uint buf_size_;     // size of the allocated buffer
		  byte* buf_;        // We don't own this buffer in case WebPIUpdate()

		  uint part0_size_;         // size of partition #0
		  byte* part0_buf_;  // buffer to store partition #0
		}

		struct WebPIDecoder
		{
		  DecState state_;         // current decoding state
		  WebPDecParams params_;   // Params to store output info
		  VP8Decoder* dec_;
		  VP8Io io_;

		  MemBuffer mem_;          // input memory buffer.
		  WebPDecBuffer output_;   // output buffer (when no external one is supplied)
		  uint vp8_size_;      // VP8 size extracted from VP8 Header.
		};

		// MB context to restore in case VP8DecodeMB() fails
		struct MBContext
		{
		  VP8MB left_;
		  VP8MB info_;
		  byte intra_t_[4];
		  byte intra_l_[4];
		  VP8BitReader br_;
		  VP8BitReader token_br_;
		}

		//------------------------------------------------------------------------------
		// MemBuffer: incoming data handling

		void REMAP(PTR, OLD_BASE, NEW_BASE) { (PTR) = (NEW_BASE) + ((PTR) - OLD_BASE); }

		static uint MemDataSize(MemBuffer* mem) {
		  return (mem.end_ - mem.start_);
		}

		// Appends data to the end of MemBuffer.buf_. It expands the allocated memory
		// size if required and also updates VP8BitReader's if new memory is allocated.
		static int AppendToMemBuffer(WebPIDecoder* idec,
									 byte* data, uint data_size) {
		  MemBuffer* mem = &idec.mem_;
		  VP8Decoder* dec = idec.dec_;
		  int last_part = dec.num_parts_ - 1;
		  assert(mem.mode_ == MEM_MODE_APPEND);

		  if (mem.end_ + data_size > mem.buf_size_) {  // Need some free memory
			int p;
			byte* new_buf = null;
			uint num_chunks =
				(MemDataSize(mem) + data_size + CHUNK_SIZE - 1) / CHUNK_SIZE;
			uint new_size = num_chunks * CHUNK_SIZE;
			byte* base = mem.buf_ + mem.start_;

			new_buf = (byte*)malloc(new_size);
			if (!new_buf) return 0;
			memcpy(new_buf, base, MemDataSize(mem));

			// adjust VP8BitReader pointers
			for (p = 0; p <= last_part; ++p) {
			  if (dec.parts_[p].buf_) {
				REMAP(dec.parts_[p].buf_, base, new_buf);
				REMAP(dec.parts_[p].buf_end_, base, new_buf);
			  }
			}

			// adjust memory pointers
			free(mem.buf_);
			mem.buf_ = new_buf;
			mem.buf_size_ = new_size;

			mem.end_ = MemDataSize(mem);
			mem.start_ = 0;
		  }

		  memcpy(mem.buf_ + mem.end_, data, data_size);
		  mem.end_ += data_size;
		  assert(mem.end_ <= mem.buf_size_);
		  assert(last_part >= 0);
		  dec.parts_[last_part].buf_end_ = mem.buf_ + mem.end_;

		  // note: setting up idec.io_ is only really needed at the beginning
		  // of the decoding, till partition #0 is complete.
		  idec.io_.data = mem.buf_ + mem.start_;
		  idec.io_.data_size = MemDataSize(mem);
		  return 1;
		}

		static int RemapMemBuffer(WebPIDecoder* idec,
								  byte* data, uint data_size) {
		  int p;
		  MemBuffer* mem = &idec.mem_;
		  VP8Decoder* dec = idec.dec_;
		  int last_part = dec.num_parts_ - 1;
		  byte* base = mem.buf_;

		  assert(mem.mode_ == MEM_MODE_MAP);
		  if (data_size < mem.buf_size_) {
			return 0;  // we cannot remap to a shorter buffer!
		  }

		  for (p = 0; p <= last_part; ++p) {
			if (dec.parts_[p].buf_) {
			  REMAP(dec.parts_[p].buf_, base, data);
			  REMAP(dec.parts_[p].buf_end_, base, data);
			}
		  }
		  assert(last_part >= 0);
		  dec.parts_[last_part].buf_end_ = data + data_size;

		  // Remap partition #0 data pointer to new offset.
		  if (dec.br_.buf_) {
			REMAP(dec.br_.buf_, base, data);
			REMAP(dec.br_.buf_end_, base, data);
		  }

		  mem.buf_ = (byte*)data;
		  mem.end_ = mem.buf_size_ = data_size;

		  idec.io_.data = data;
		  idec.io_.data_size = data_size;
		  return 1;
		}

		static void InitMemBuffer(MemBuffer* mem) {
		  mem.mode_       = MEM_MODE_NONE;
		  mem.buf_        = 0;
		  mem.buf_size_   = 0;
		  mem.part0_buf_  = 0;
		  mem.part0_size_ = 0;
		}

		static void ClearMemBuffer(MemBuffer* mem) {
		  assert(mem);
		  if (mem.mode_ == MEM_MODE_APPEND) {
			free(mem.buf_);
			free((void*)mem.part0_buf_);
		  }
		}

		static int CheckMemBufferMode(MemBuffer* mem, MemBufferMode expected) {
		  if (mem.mode_ == MEM_MODE_NONE) {
			mem.mode_ = expected;    // switch to the expected mode
		  } else if (mem.mode_ != expected) {
			return 0;         // we mixed the modes => error
		  }
		  assert(mem.mode_ == expected);   // mode is ok
		  return 1;
		}

		#undef REMAP

		//------------------------------------------------------------------------------
		// Macroblock-decoding contexts

		static void SaveContext(VP8Decoder* dec, VP8BitReader* token_br,
								MBContext* context) {
		  VP8BitReader* br = &dec.br_;
		  VP8MB* left = dec.mb_info_ - 1;
		  VP8MB* info = dec.mb_info_ + dec.mb_x_;

		  context.left_ = *left;
		  context.info_ = *info;
		  context.br_ = *br;
		  context.token_br_ = *token_br;
		  memcpy(context.intra_t_, dec.intra_t_ + 4 * dec.mb_x_, 4);
		  memcpy(context.intra_l_, dec.intra_l_, 4);
		}

		static void RestoreContext(MBContext* context, VP8Decoder* dec,
								   VP8BitReader* token_br) {
		  VP8BitReader* br = &dec.br_;
		  VP8MB* left = dec.mb_info_ - 1;
		  VP8MB* info = dec.mb_info_ + dec.mb_x_;

		  *left = context.left_;
		  *info = context.info_;
		  *br = context.br_;
		  *token_br = context.token_br_;
		  memcpy(dec.intra_t_ + 4 * dec.mb_x_, context.intra_t_, 4);
		  memcpy(dec.intra_l_, context.intra_l_, 4);
		}

		//------------------------------------------------------------------------------

		static VP8StatusCode IDecError(WebPIDecoder* idec, VP8StatusCode error) {
		  if (idec.state_ == STATE_VP8_DATA) {
			VP8Io* io = &idec.io_;
			if (io.teardown) {
			  io.teardown(io);
			}
		  }
		  idec.state_ = STATE_ERROR;
		  return error;
		}

		static void ChangeState(WebPIDecoder* idec, DecState new_state,
								uint consumed_bytes) {
		  idec.state_ = new_state;
		  idec.mem_.start_ += consumed_bytes;
		  assert(idec.mem_.start_ <= idec.mem_.end_);
		}

		// Headers
		static VP8StatusCode DecodeWebPHeaders(WebPIDecoder* idec) {
		  byte* data = idec.mem_.buf_ + idec.mem_.start_;
		  uint curr_size = MemDataSize(&idec.mem_);
		  uint vp8_size;
		  uint bytes_skipped;
		  VP8StatusCode status;

		  status = WebPParseHeaders(&data, &curr_size, &vp8_size, &bytes_skipped,
									&idec.dec_.alpha_data_,
									&idec.dec_.alpha_data_size_);
		  if (status == VP8_STATUS_NOT_ENOUGH_DATA) {
			return VP8_STATUS_SUSPENDED;  // We haven't found a VP8 chunk yet.
		  } else if (status == VP8_STATUS_OK) {
			idec.vp8_size_ = vp8_size;
			ChangeState(idec, STATE_VP8_FRAME_HEADER, bytes_skipped);
			return VP8_STATUS_OK;  // We have skipped all pre-VP8 chunks.
		  } else {
			return IDecError(idec, status);
		  }
		}

		static VP8StatusCode DecodeVP8FrameHeader(WebPIDecoder* idec) {
		  byte* data = idec.mem_.buf_ + idec.mem_.start_;
		  uint curr_size = MemDataSize(&idec.mem_);
		  uint bits;

		  if (curr_size < VP8_FRAME_HEADER_SIZE) {
			// Not enough data bytes to extract VP8 Frame Header.
			return VP8_STATUS_SUSPENDED;
		  }
		  if (!VP8GetInfo(data, curr_size, idec.vp8_size_, null, null)) {
			return IDecError(idec, VP8_STATUS_BITSTREAM_ERROR);
		  }

		  bits = data[0] | (data[1] << 8) | (data[2] << 16);
		  idec.mem_.part0_size_ = (bits >> 5) + VP8_FRAME_HEADER_SIZE;

		  idec.io_.data_size = curr_size;
		  idec.io_.data = data;
		  idec.state_ = STATE_VP8_PARTS0;
		  return VP8_STATUS_OK;
		}

		// Partition #0
		static int CopyParts0Data(WebPIDecoder* idec) {
		  VP8BitReader* br = &idec.dec_.br_;
		  uint psize = br.buf_end_ - br.buf_;
		  MemBuffer* mem = &idec.mem_;
		  assert(!mem.part0_buf_);
		  assert(psize > 0);
		  assert(psize <= mem.part0_size_);
		  if (mem.mode_ == MEM_MODE_APPEND) {
			// We copy and grab ownership of the partition #0 data.
			byte* part0_buf = (byte*)malloc(psize);
			if (!part0_buf) {
			  return 0;
			}
			memcpy(part0_buf, br.buf_, psize);
			mem.part0_buf_ = part0_buf;
			br.buf_ = part0_buf;
			br.buf_end_ = part0_buf + psize;
		  } else {
			// Else: just keep pointers to the partition #0's data in dec_.br_.
		  }
		  mem.start_ += psize;
		  return 1;
		}

		static VP8StatusCode DecodePartition0(WebPIDecoder* idec) {
		  VP8Decoder* dec = idec.dec_;
		  VP8Io* io = &idec.io_;
		  WebPDecParams* params = &idec.params_;
		  WebPDecBuffer* output = params.output;

		  // Wait till we have enough data for the whole partition #0
		  if (MemDataSize(&idec.mem_) < idec.mem_.part0_size_) {
			return VP8_STATUS_SUSPENDED;
		  }

		  if (!VP8GetHeaders(dec, io)) {
			VP8StatusCode status = dec.status_;
			if (status == VP8_STATUS_SUSPENDED ||
				status == VP8_STATUS_NOT_ENOUGH_DATA) {
			  // treating NOT_ENOUGH_DATA as SUSPENDED state
			  return VP8_STATUS_SUSPENDED;
			}
			return IDecError(idec, status);
		  }

		  // Allocate/Verify output buffer now
		  dec.status_ = WebPAllocateDecBuffer(io.width, io.height, params.options,
											   output);
		  if (dec.status_ != VP8_STATUS_OK) {
			return IDecError(idec, dec.status_);
		  }

		  if (!CopyParts0Data(idec)) {
			return IDecError(idec, VP8_STATUS_OUT_OF_MEMORY);
		  }

		  // Finish setting up the decoding parameters. Will call io.setup().
		  if (VP8EnterCritical(dec, io) != VP8_STATUS_OK) {
			return IDecError(idec, dec.status_);
		  }

		  // Note: past this point, teardown() must always be called
		  // in case of error.
		  idec.state_ = STATE_VP8_DATA;
		  // Allocate memory and prepare everything.
		  if (!VP8InitFrame(dec, io)) {
			return IDecError(idec, dec.status_);
		  }
		  return VP8_STATUS_OK;
		}

		// Remaining partitions
		static VP8StatusCode DecodeRemaining(WebPIDecoder* idec) {
		  VP8Decoder* dec = idec.dec_;
		  VP8Io* io = &idec.io_;

		  assert(dec.ready_);

		  for (; dec.mb_y_ < dec.mb_h_; ++dec.mb_y_) {
			VP8BitReader* token_br = &dec.parts_[dec.mb_y_ & (dec.num_parts_ - 1)];
			if (dec.mb_x_ == 0) {
			  VP8InitScanline(dec);
			}
			for (; dec.mb_x_ < dec.mb_w_;  dec.mb_x_++) {
			  MBContext context;
			  SaveContext(dec, token_br, &context);

			  if (!VP8DecodeMB(dec, token_br)) {
				RestoreContext(&context, dec, token_br);
				// We shouldn't fail when MAX_MB data was available
				if (dec.num_parts_ == 1 && MemDataSize(&idec.mem_) > MAX_MB_SIZE) {
				  return IDecError(idec, VP8_STATUS_BITSTREAM_ERROR);
				}
				return VP8_STATUS_SUSPENDED;
			  }
			  VP8ReconstructBlock(dec);
			  // Store data and save block's filtering params
			  VP8StoreBlock(dec);

			  // Release buffer only if there is only one partition
			  if (dec.num_parts_ == 1) {
				idec.mem_.start_ = token_br.buf_ - idec.mem_.buf_;
				assert(idec.mem_.start_ <= idec.mem_.end_);
			  }
			}
			if (!VP8ProcessRow(dec, io)) {
			  return IDecError(idec, VP8_STATUS_USER_ABORT);
			}
			dec.mb_x_ = 0;
		  }
		  // Synchronize the thread and check for errors.
		  if (!VP8ExitCritical(dec, io)) {
			return IDecError(idec, VP8_STATUS_USER_ABORT);
		  }
		  dec.ready_ = 0;
		  idec.state_ = STATE_DONE;

		  return VP8_STATUS_OK;
		}

		  // Main decoding loop
		static VP8StatusCode IDecode(WebPIDecoder* idec) {
		  VP8StatusCode status = VP8_STATUS_SUSPENDED;
		  assert(idec.dec_);

		  if (idec.state_ == STATE_PRE_VP8) {
			status = DecodeWebPHeaders(idec);
		  }
		  if (idec.state_ == STATE_VP8_FRAME_HEADER) {
			status = DecodeVP8FrameHeader(idec);
		  }
		  if (idec.state_ == STATE_VP8_PARTS0) {
			status = DecodePartition0(idec);
		  }
		  if (idec.state_ == STATE_VP8_DATA) {
			status = DecodeRemaining(idec);
		  }
		  return status;
		}

		//------------------------------------------------------------------------------
		// Public functions

		WebPIDecoder* WebPINewDecoder(WebPDecBuffer* output_buffer) {
		  WebPIDecoder* idec = (WebPIDecoder*)calloc(1, sizeof(WebPIDecoder));
		  if (idec == null) {
			return null;
		  }

		  idec.dec_ = VP8New();
		  if (idec.dec_ == null) {
			free(idec);
			return null;
		  }

		  idec.state_ = STATE_PRE_VP8;

		  InitMemBuffer(&idec.mem_);
		  WebPInitDecBuffer(&idec.output_);
		  VP8InitIo(&idec.io_);

		  WebPResetDecParams(&idec.params_);
		  idec.params_.output = output_buffer ? output_buffer : &idec.output_;
		  WebPInitCustomIo(&idec.params_, &idec.io_);  // Plug the I/O functions.

		#if WEBP_USE_THREAD
		  idec.dec_.use_threads_ = idec.params_.options &&
									 (idec.params_.options.use_threads > 0);
		#else
		  idec.dec_.use_threads_ = 0;
		#endif
		  idec.vp8_size_ = 0;

		  return idec;
		}

		WebPIDecoder* WebPIDecode(byte* data, uint data_size,
								  WebPDecoderConfig* config) {
		  WebPIDecoder* idec;

		  // Parse the bitstream's features, if requested:
		  if (data != null && data_size > 0 && config != null) {
			if (WebPGetFeatures(data, data_size, &config.input) != VP8_STATUS_OK) {
			  return null;
			}
		  }
		  // Create an instance of the incremental decoder
		  idec = WebPINewDecoder(config ? &config.output : null);
		  if (!idec) {
			return null;
		  }
		  // Finish initialization
		  if (config != null) {
			idec.params_.options = &config.options;
		  }
		  return idec;
		}

		void WebPIDelete(WebPIDecoder* idec) {
		  if (!idec) return;
		  VP8Delete(idec.dec_);
		  ClearMemBuffer(&idec.mem_);
		  WebPFreeDecBuffer(&idec.output_);
		  free(idec);
		}

		//------------------------------------------------------------------------------
		// Wrapper toward WebPINewDecoder

		WebPIDecoder* WebPINew(WEBP_CSP_MODE mode) {
		  WebPIDecoder* idec = WebPINewDecoder(null);
		  if (!idec) return null;
		  idec.output_.colorspace = mode;
		  return idec;
		}

		WebPIDecoder* WebPINewRGB(WEBP_CSP_MODE mode, byte* output_buffer,
								  int output_buffer_size, int output_stride) {
		  WebPIDecoder* idec;
		  if (mode >= MODE_YUV) return null;
		  idec = WebPINewDecoder(null);
		  if (!idec) return null;
		  idec.output_.colorspace = mode;
		  idec.output_.is_external_memory = 1;
		  idec.output_.u.RGBA.rgba = output_buffer;
		  idec.output_.u.RGBA.stride = output_stride;
		  idec.output_.u.RGBA.size = output_buffer_size;
		  return idec;
		}

		WebPIDecoder* WebPINewYUV(byte* luma, int luma_size, int luma_stride,
								  byte* u, int u_size, int u_stride,
								  byte* v, int v_size, int v_stride) {
		  WebPIDecoder* idec = WebPINewDecoder(null);
		  if (!idec) return null;
		  idec.output_.colorspace = MODE_YUV;
		  idec.output_.is_external_memory = 1;
		  idec.output_.u.YUVA.y = luma;
		  idec.output_.u.YUVA.y_stride = luma_stride;
		  idec.output_.u.YUVA.y_size = luma_size;
		  idec.output_.u.YUVA.u = u;
		  idec.output_.u.YUVA.u_stride = u_stride;
		  idec.output_.u.YUVA.u_size = u_size;
		  idec.output_.u.YUVA.v = v;
		  idec.output_.u.YUVA.v_stride = v_stride;
		  idec.output_.u.YUVA.v_size = v_size;
		  return idec;
		}

		//------------------------------------------------------------------------------

		static VP8StatusCode IDecCheckStatus(WebPIDecoder* idec) {
		  assert(idec);
		  if (idec.dec_ == null) {
			return VP8_STATUS_USER_ABORT;
		  }
		  if (idec.state_ == STATE_ERROR) {
			return VP8_STATUS_BITSTREAM_ERROR;
		  }
		  if (idec.state_ == STATE_DONE) {
			return VP8_STATUS_OK;
		  }
		  return VP8_STATUS_SUSPENDED;
		}

		VP8StatusCode WebPIAppend(WebPIDecoder* idec, byte* data,
								  uint data_size) {
		  VP8StatusCode status;
		  if (idec == null || data == null) {
			return VP8_STATUS_INVALID_PARAM;
		  }
		  status = IDecCheckStatus(idec);
		  if (status != VP8_STATUS_SUSPENDED) {
			return status;
		  }
		  // Check mixed calls between RemapMemBuffer and AppendToMemBuffer.
		  if (!CheckMemBufferMode(&idec.mem_, MEM_MODE_APPEND)) {
			return VP8_STATUS_INVALID_PARAM;
		  }
		  // Append data to memory buffer
		  if (!AppendToMemBuffer(idec, data, data_size)) {
			return VP8_STATUS_OUT_OF_MEMORY;
		  }
		  return IDecode(idec);
		}

		VP8StatusCode WebPIUpdate(WebPIDecoder* idec, byte* data,
								  uint data_size) {
		  VP8StatusCode status;
		  if (idec == null || data == null) {
			return VP8_STATUS_INVALID_PARAM;
		  }
		  status = IDecCheckStatus(idec);
		  if (status != VP8_STATUS_SUSPENDED) {
			return status;
		  }
		  // Check mixed calls between RemapMemBuffer and AppendToMemBuffer.
		  if (!CheckMemBufferMode(&idec.mem_, MEM_MODE_MAP)) {
			return VP8_STATUS_INVALID_PARAM;
		  }
		  // Make the memory buffer point to the new buffer
		  if (!RemapMemBuffer(idec, data, data_size)) {
			return VP8_STATUS_INVALID_PARAM;
		  }
		  return IDecode(idec);
		}

		//------------------------------------------------------------------------------

		static WebPDecBuffer* GetOutputBuffer(WebPIDecoder* idec) {
		  if (!idec || !idec.dec_ || idec.state_ <= STATE_VP8_PARTS0) {
			return null;
		  }
		  return idec.params_.output;
		}

		WebPDecBuffer* WebPIDecodedArea(WebPIDecoder* idec,
											  int* left, int* top,
											  int* width, int* height) {
		  WebPDecBuffer* src = GetOutputBuffer(idec);
		  if (left) *left = 0;
		  if (top) *top = 0;
		  // TODO(skal): later include handling of rotations.
		  if (src) {
			if (width) *width = src.width;
			if (height) *height = idec.params_.last_y;
		  } else {
			if (width) *width = 0;
			if (height) *height = 0;
		  }
		  return src;
		}

		byte* WebPIDecGetRGB(WebPIDecoder* idec, int* last_y,
								int* width, int* height, int* stride) {
		  WebPDecBuffer* src = GetOutputBuffer(idec);
		  if (!src) return null;
		  if (src.colorspace >= MODE_YUV) {
			return null;
		  }

		  if (last_y) *last_y = idec.params_.last_y;
		  if (width) *width = src.width;
		  if (height) *height = src.height;
		  if (stride) *stride = src.u.RGBA.stride;

		  return src.u.RGBA.rgba;
		}

		byte* WebPIDecGetYUV(WebPIDecoder* idec, int* last_y,
								byte** u, byte** v,
								int* width, int* height, int *stride, int* uv_stride) {
		  WebPDecBuffer* src = GetOutputBuffer(idec);
		  if (!src) return null;
		  if (src.colorspace < MODE_YUV) {
			return null;
		  }

		  if (last_y) *last_y = idec.params_.last_y;
		  if (u) *u = src.u.YUVA.u;
		  if (v) *v = src.u.YUVA.v;
		  if (width) *width = src.width;
		  if (height) *height = src.height;
		  if (stride) *stride = src.u.YUVA.y_stride;
		  if (uv_stride) *uv_stride = src.u.YUVA.u_stride;

		  return src.u.YUVA.y;
		}

		int WebPISetIOHooks(WebPIDecoder* idec,
							VP8IoPutHook put,
							VP8IoSetupHook setup,
							VP8IoTeardownHook teardown,
							void* user_data) {
		  if (!idec || !idec.dec_ || idec.state_ > STATE_PRE_VP8) {
			return 0;
		  }

		  idec.io_.put = put;
		  idec.io_.setup = setup;
		  idec.io_.teardown = teardown;
		  idec.io_.opaque = user_data;

		  return 1;
		}


	}
}
#endif
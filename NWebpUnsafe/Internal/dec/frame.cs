using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#if false
namespace NWebp.Internal
{
	unsafe public partial class VP8Decoder
	{
		void DoFilter(int mb_x, int mb_y)
		{
			VP8ThreadContext ctx = this.thread_ctx_;
			int y_bps = this.cache_y_stride_;
			VP8FInfo* f_info = &ctx.f_info_[mb_x];
			byte* y_dst = this.cache_y_ + ctx.id_ * 16 * y_bps + mb_x * 16;
			int level = f_info.f_level_;
			int ilevel = f_info.f_ilevel_;
			int limit = 2 * level + ilevel;
			if (level == 0)
			{
				return;
			}
			if (this.filter_type_ == 1)
			{
				// simple
				if (mb_x > 0)
				{
					VP8SimpleHFilter16(y_dst, y_bps, limit + 4);
				}
				if (f_info.f_inner_)
				{
					VP8SimpleHFilter16i(y_dst, y_bps, limit);
				}
				if (mb_y > 0)
				{
					VP8SimpleVFilter16(y_dst, y_bps, limit + 4);
				}
				if (f_info.f_inner_)
				{
					VP8SimpleVFilter16i(y_dst, y_bps, limit);
				}
			}
			else
			{
				// complex
				int uv_bps = this.cache_uv_stride_;
				byte* u_dst = this.cache_u_ + ctx.id_ * 8 * uv_bps + mb_x * 8;
				byte* v_dst = this.cache_v_ + ctx.id_ * 8 * uv_bps + mb_x * 8;
				int hev_thresh =
					hev_thresh_from_level(level, this.frm_hdr_.key_frame_);
				if (mb_x > 0)
				{
					VP8HFilter16(y_dst, y_bps, limit + 4, ilevel, hev_thresh);
					VP8HFilter8(u_dst, v_dst, uv_bps, limit + 4, ilevel, hev_thresh);
				}
				if (f_info.f_inner_)
				{
					VP8HFilter16i(y_dst, y_bps, limit, ilevel, hev_thresh);
					VP8HFilter8i(u_dst, v_dst, uv_bps, limit, ilevel, hev_thresh);
				}
				if (mb_y > 0)
				{
					VP8VFilter16(y_dst, y_bps, limit + 4, ilevel, hev_thresh);
					VP8VFilter8(u_dst, v_dst, uv_bps, limit + 4, ilevel, hev_thresh);
				}
				if (f_info.f_inner_)
				{
					VP8VFilter16i(y_dst, y_bps, limit, ilevel, hev_thresh);
					VP8VFilter8i(u_dst, v_dst, uv_bps, limit, ilevel, hev_thresh);
				}
			}
		}

		/// <summary>
		/// Filter the decoded macroblock row (if needed)
		/// </summary>
		void FilterRow()
		{
			int mb_x;
			int mb_y = this.thread_ctx_.mb_y_;
			Global.assert(this.thread_ctx_.filter_row_ != 0);
			for (mb_x = this.tl_mb_x_; mb_x < this.br_mb_x_; ++mb_x)
			{
				DoFilter(dec, mb_x, mb_y);
			}
		}

		const int ALIGN_MASK = (32 - 1);

		//------------------------------------------------------------------------------
		// Filtering

		// kFilterExtraRows[] = How many extra lines are needed on the MB boundary
		// for caching, given a filtering level.
		// Simple filter:  up to 2 luma samples are read and 1 is written.
		// Complex filter: up to 4 luma samples are read and 3 are written. Same for
		//                 U/V, so it's 8 samples total (because of the 2x upsampling).
		static byte[] kFilterExtraRows = new byte[3] { 0, 2, 8 };

		static int hev_thresh_from_level(int level, int keyframe)
		{
			if (keyframe != 0)
			{
				return (level >= 40) ? 2 : (level >= 15) ? 1 : 0;
			}
			else
			{
				return (level >= 40) ? 3 : (level >= 20) ? 2 : (level >= 15) ? 1 : 0;
			}
		}


		void VP8StoreBlock()
		{
			if (this.filter_type_ > 0)
			{
				VP8FInfo* info = this.f_info_ + this.mb_x_;
				int skip = this.mb_info_[this.mb_x_].skip_;
				int level = this.filter_levels_[this.segment_];
				if (this.filter_hdr_.use_lf_delta_)
				{
					// TODO(skal): only CURRENT is handled for now.
					level += this.filter_hdr_.ref_lf_delta_[0];
					if (this.is_i4x4_)
					{
						level += this.filter_hdr_.mode_lf_delta_[0];
					}
				}
				level = (level < 0) ? 0 : (level > 63) ? 63 : level;
				info.f_level_ = level;

				if (this.filter_hdr_.sharpness_ > 0)
				{
					if (this.filter_hdr_.sharpness_ > 4)
					{
						level >>= 2;
					}
					else
					{
						level >>= 1;
					}
					if (level > 9 - this.filter_hdr_.sharpness_)
					{
						level = 9 - this.filter_hdr_.sharpness_;
					}
				}

				info.f_ilevel_ = (level < 1) ? 1 : level;
				info.f_inner_ = (!skip || this.is_i4x4_);
			}
			{
				// Transfer samples to row cache
				int y;
				int y_offset = this.cache_id_ * 16 * this.cache_y_stride_;
				int uv_offset = this.cache_id_ * 8 * this.cache_uv_stride_;
				byte* ydst = this.cache_y_ + this.mb_x_ * 16 + y_offset;
				byte* udst = this.cache_u_ + this.mb_x_ * 8 + uv_offset;
				byte* vdst = this.cache_v_ + this.mb_x_ * 8 + uv_offset;
				for (y = 0; y < 16; ++y)
				{
					Global.memcpy(ydst + y * this.cache_y_stride_,
							this.yuv_b_ + Y_OFF + y * BPS, 16);
				}
				for (y = 0; y < 8; ++y)
				{
					Global.memcpy(udst + y * this.cache_uv_stride_,
						this.yuv_b_ + U_OFF + y * BPS, 8);
					Global.memcpy(vdst + y * this.cache_uv_stride_,
						this.yuv_b_ + V_OFF + y * BPS, 8);
				}
			}
		}

		/// <summary>
		/// This function is called after a row of macroblocks is finished decoding.
		/// It also takes into account the following restrictions:
		/// * In case of in-loop filtering, we must hold off sending some of the bottom
		///   pixels as they are yet unfiltered. They will be when the next macroblock
		///   row is decoded. Meanwhile, we must preserve them by rotating them in the
		///   cache area. This doesn't hold for the very bottom row of the uncropped
		///   picture of course.
		/// * we must clip the remaining pixels against the cropping area. The VP8Io
		///   struct must have the following fields set correctly before calling put():
		/// </summary>
		/// <param name="mb_y"></param>
		/// <returns></returns>
		static private int MACROBLOCK_VPOS(int mb_y)
		{
			// vertical position of a MB
			return ((mb_y) * 16);
		}

		/// <summary>
		/// Finalize and transmit a complete row. Return false in case of user-abort.
		/// </summary>
		/// <param name="dec"></param>
		/// <param name="io"></param>
		/// <returns></returns>
		int FinishRow(VP8Io io)
		{
			int ok = 1;
			VP8ThreadContext ctx = this.thread_ctx_;
			int extra_y_rows = kFilterExtraRows[this.filter_type_];
			int ysize = extra_y_rows * this.cache_y_stride_;
			int uvsize = (extra_y_rows / 2) * this.cache_uv_stride_;
			int y_offset = ctx.id_ * 16 * this.cache_y_stride_;
			int uv_offset = ctx.id_ * 8 * this.cache_uv_stride_;
			byte* ydst = this.cache_y_ - ysize + y_offset;
			byte* udst = this.cache_u_ - uvsize + uv_offset;
			byte* vdst = this.cache_v_ - uvsize + uv_offset;
			bool first_row = (ctx.mb_y_ == 0);
			bool last_row = (ctx.mb_y_ >= this.br_mb_y_ - 1);
			int y_start = MACROBLOCK_VPOS(ctx.mb_y_);
			int y_end = MACROBLOCK_VPOS(ctx.mb_y_ + 1);

			if (ctx.filter_row_)
			{
				FilterRow(dec);
			}

			if (io.put)
			{
				if (!first_row)
				{
					y_start -= extra_y_rows;
					io.y = ydst;
					io.u = udst;
					io.v = vdst;
				}
				else
				{
					io.y = this.cache_y_ + y_offset;
					io.u = this.cache_u_ + uv_offset;
					io.v = this.cache_v_ + uv_offset;
				}

				if (!last_row)
				{
					y_end -= extra_y_rows;
				}
				if (y_end > io.crop_bottom)
				{
					y_end = io.crop_bottom;    // make sure we don't overflow on last row.
				}
				io.a = null;
				if (this.alpha_data_ && y_start < y_end)
				{
					io.a = VP8DecompressAlphaRows(dec, y_start, y_end - y_start);
					if (io.a == null)
					{
						return VP8SetError(dec, VP8_STATUS_BITSTREAM_ERROR,
											"Could not decode alpha data.");
					}
				}
				if (y_start < io.crop_top)
				{
					int delta_y = io.crop_top - y_start;
					y_start = io.crop_top;
					assert(!(delta_y & 1));
					io.y += this.cache_y_stride_ * delta_y;
					io.u += this.cache_uv_stride_ * (delta_y >> 1);
					io.v += this.cache_uv_stride_ * (delta_y >> 1);
					if (io.a)
					{
						io.a += io.width * delta_y;
					}
				}
				if (y_start < y_end)
				{
					io.y += io.crop_left;
					io.u += io.crop_left >> 1;
					io.v += io.crop_left >> 1;
					if (io.a)
					{
						io.a += io.crop_left;
					}
					io.mb_y = y_start - io.crop_top;
					io.mb_w = io.crop_right - io.crop_left;
					io.mb_h = y_end - y_start;
					ok = io.put(io);
				}
			}
			// rotate top samples if needed
			if (ctx.id_ + 1 == this.num_caches_)
			{
				if (!last_row)
				{
					memcpy(this.cache_y_ - ysize, ydst + 16 * this.cache_y_stride_, ysize);
					memcpy(this.cache_u_ - uvsize, udst + 8 * this.cache_uv_stride_, uvsize);
					memcpy(this.cache_v_ - uvsize, vdst + 8 * this.cache_uv_stride_, uvsize);
				}
			}

			return ok;
		}


		int VP8ProcessRow(VP8Io io)
		{
			int ok = 1;
			VP8ThreadContext* ctx = &this.thread_ctx_;
			if (!this.use_threads_)
			{
				// ctx.id_ and ctx.f_info_ are already set
				ctx.mb_y_ = this.mb_y_;
				ctx.filter_row_ = this.filter_row_;
				ok = FinishRow(dec, io);
			}
			else
			{
				WebPWorker* worker = &this.worker_;
				// Finish previous job *before* updating context
				ok &= WebPWorkerSync(worker);
				assert(worker.status_ == OK);
				if (ok)
				{   // spawn a new deblocking/output job
					ctx.io_ = *io;
					ctx.id_ = this.cache_id_;
					ctx.mb_y_ = this.mb_y_;
					ctx.filter_row_ = this.filter_row_;
					if (ctx.filter_row_)
					{    // just swap filter info
						VP8FInfo* tmp = ctx.f_info_;
						ctx.f_info_ = this.f_info_;
						this.f_info_ = tmp;
					}
					WebPWorkerLaunch(worker);
					if (++this.cache_id_ == this.num_caches_)
					{
						this.cache_id_ = 0;
					}
				}
			}
			return ok;
		}


		/// <summary>
		/// Finish setting up the decoding parameter once user's setup() is called.
		/// </summary>
		/// <param name="io"></param>
		/// <returns></returns>
		VP8StatusCode VP8EnterCritical(VP8Io io)
		{
			// Call setup() first. This may trigger additional decoding features on 'io'.
			// Note: Afterward, we must call teardown() not matter what.
			if (io.setup && !io.setup(io))
			{
				VP8SetError(dec, VP8_STATUS_USER_ABORT, "Frame setup failed");
				return this.status_;
			}

			// Disable filtering per user request
			if (io.bypass_filtering)
			{
				this.filter_type_ = 0;
			}
			// TODO(skal): filter type / strength / sharpness forcing

			// Define the area where we can skip in-loop filtering, in case of cropping.
			//
			// 'Simple' filter reads two luma samples outside of the macroblock and
			// and filters one. It doesn't filter the chroma samples. Hence, we can
			// avoid doing the in-loop filtering before crop_top/crop_left position.
			// For the 'Complex' filter, 3 samples are read and up to 3 are filtered.
			// Means: there's a dependency chain that goes all the way up to the
			// top-left corner of the picture (MB #0). We must filter all the previous
			// macroblocks.
			// TODO(skal): add an 'approximate_decoding' option, that won't produce
			// a 1:1 bit-exactness for complex filtering?
			{
				int extra_pixels = kFilterExtraRows[this.filter_type_];
				if (this.filter_type_ == 2)
				{
					// For complex filter, we need to preserve the dependency chain.
					this.tl_mb_x_ = 0;
					this.tl_mb_y_ = 0;
				}
				else
				{
					// For simple filter, we can filter only the cropped region.
					// We include 'extra_pixels' on the other side of the boundary, since
					// vertical or horizontal filtering of the previous macroblock can
					// modify some abutting pixels.
					this.tl_mb_x_ = (io.crop_left - extra_pixels) >> 4;
					this.tl_mb_y_ = (io.crop_top - extra_pixels) >> 4;
					if (this.tl_mb_x_ < 0) this.tl_mb_x_ = 0;
					if (this.tl_mb_y_ < 0) this.tl_mb_y_ = 0;
				}
				// We need some 'extra' pixels on the right/bottom.
				this.br_mb_y_ = (io.crop_bottom + 15 + extra_pixels) >> 4;
				this.br_mb_x_ = (io.crop_right + 15 + extra_pixels) >> 4;
				if (this.br_mb_x_ > this.mb_w_)
				{
					this.br_mb_x_ = this.mb_w_;
				}
				if (this.br_mb_y_ > this.mb_h_)
				{
					this.br_mb_y_ = this.mb_h_;
				}
			}
			return VP8StatusCode.VP8_STATUS_OK;
		}

		int VP8ExitCritical(VP8Io io)
		{
			int ok = 1;
			if (this.use_threads_)
			{
				ok = WebPWorkerSync(&this.worker_);
			}

			if (io.teardown)
			{
				io.teardown(io);
			}
			return ok;
		}


		//------------------------------------------------------------------------------
		// For multi-threaded decoding we need to use 3 rows of 16 pixels as delay line.
		//
		// Reason is: the deblocking filter cannot deblock the bottom horizontal edges
		// immediately, and needs to wait for first few rows of the next macroblock to
		// be decoded. Hence, deblocking is lagging behind by 4 or 8 pixels (depending
		// on strength).
		// With two threads, the vertical positions of the rows being decoded are:
		// Decode:  [ 0..15][16..31][32..47][48..63][64..79][...
		// Deblock:         [ 0..11][12..27][28..43][44..59][...
		// If we use two threads and two caches of 16 pixels, the sequence would be:
		// Decode:  [ 0..15][16..31][ 0..15!!][16..31][ 0..15][...
		// Deblock:         [ 0..11][12..27!!][-4..11][12..27][...
		// The problem occurs during row [12..15!!] that both the decoding and
		// deblocking threads are writing simultaneously.
		// With 3 cache lines, one get a safe write pattern:
		// Decode:  [ 0..15][16..31][32..47][ 0..15][16..31][32..47][0..
		// Deblock:         [ 0..11][12..27][28..43][-4..11][12..27][28...
		// Note that multi-threaded output _without_ deblocking can make use of two
		// cache lines of 16 pixels only, since there's no lagging behind. The decoding
		// and output process have non-concurrent writing:
		// Decode:  [ 0..15][16..31][ 0..15][16..31][...
		// io.put:         [ 0..15][16..31][ 0..15][...

		const int MT_CACHE_LINES = 3;

		/// <summary>
		/// 1 cache row only for single-threaded case
		/// </summary>
		const int ST_CACHE_LINES = 1;

		/// <summary>
		/// Initialize multi/single-thread worker
		/// </summary>
		/// <param name="dec"></param>
		/// <returns></returns>
		static int InitThreadContext()
		{
			this.cache_id_ = 0;
			if (this.use_threads_)
			{
				WebPWorker* worker = &this.worker_;
				if (!WebPWorkerReset(worker))
				{
					return VP8SetError(dec, VP8_STATUS_OUT_OF_MEMORY,
										"thread initialization failed.");
				}
				worker.data1 = dec;
				worker.data2 = (void*)&this.thread_ctx_.io_;
				worker.hook = (WebPWorkerHook)FinishRow;
				this.num_caches_ =
					(this.filter_type_ > 0) ? MT_CACHE_LINES : MT_CACHE_LINES - 1;
			}
			else
			{
				this.num_caches_ = ST_CACHE_LINES;
			}
			return 1;
		}

		/// <summary>
		/// Memory setup
		/// </summary>
		/// <returns></returns>
		int AllocateMemory()
		{
			int num_caches = this.num_caches_;
			int mb_w = this.mb_w_;
			uint intra_pred_mode_size = 4 * mb_w * sizeof(byte);
			uint top_size = (16 + 8 + 8) * mb_w;
			uint mb_info_size = (mb_w + 1) * sizeof(VP8MB);
			uint f_info_size =
				(this.filter_type_ > 0) ?
					mb_w * (this.use_threads_ ? 2 : 1) * sizeof(VP8FInfo)
				: 0;
			uint yuv_size = YUV_SIZE * sizeof(*this.yuv_b_);
			uint coeffs_size = 384 * sizeof(*this.coeffs_);
			uint cache_height = (16 * num_caches
									+ kFilterExtraRows[this.filter_type_]) * 3 / 2;
			uint cache_size = top_size * cache_height;
			uint alpha_size =
				this.alpha_data_ ? (this.pic_hdr_.width_ * this.pic_hdr_.height_) : 0;
			uint needed = intra_pred_mode_size
								+ top_size + mb_info_size + f_info_size
								+ yuv_size + coeffs_size
								+ cache_size + alpha_size + ALIGN_MASK;
			byte* mem;

			if (needed > this.mem_size_) {
			free(this.mem_);
			this.mem_size_ = 0;
			this.mem_ = (byte*)malloc(needed);
			if (this.mem_ == null) {
				return VP8SetError(dec, VP8_STATUS_OUT_OF_MEMORY,
									"no memory during frame initialization.");
			}
			this.mem_size_ = needed;
			}

			mem = (byte*)this.mem_;
			this.intra_t_ = (byte*)mem;
			mem += intra_pred_mode_size;

			this.y_t_ = (byte*)mem;
			mem += 16 * mb_w;
			this.u_t_ = (byte*)mem;
			mem += 8 * mb_w;
			this.v_t_ = (byte*)mem;
			mem += 8 * mb_w;

			this.mb_info_ = ((VP8MB*)mem) + 1;
			mem += mb_info_size;

			this.f_info_ = f_info_size ? (VP8FInfo*)mem : null;
			mem += f_info_size;
			this.thread_ctx_.id_ = 0;
			this.thread_ctx_.f_info_ = this.f_info_;
			if (this.use_threads_) {
			// secondary cache line. The deblocking process need to make use of the
			// filtering strength from previous macroblock row, while the new ones
			// are being decoded in parallel. We'll just swap the pointers.
			this.thread_ctx_.f_info_ += mb_w;
			}

			mem = (byte*)((uintptr_t)(mem + ALIGN_MASK) & ~ALIGN_MASK);
			assert((yuv_size & ALIGN_MASK) == 0);
			this.yuv_b_ = (byte*)mem;
			mem += yuv_size;

			this.coeffs_ = (short*)mem;
			mem += coeffs_size;

			this.cache_y_stride_ = 16 * mb_w;
			this.cache_uv_stride_ = 8 * mb_w;
			{
			int extra_rows = kFilterExtraRows[this.filter_type_];
			int extra_y = extra_rows * this.cache_y_stride_;
			int extra_uv = (extra_rows / 2) * this.cache_uv_stride_;
			this.cache_y_ = ((byte*)mem) + extra_y;
			this.cache_u_ = this.cache_y_
							+ 16 * num_caches * this.cache_y_stride_ + extra_uv;
			this.cache_v_ = this.cache_u_
							+ 8 * num_caches * this.cache_uv_stride_ + extra_uv;
			this.cache_id_ = 0;
			}
			mem += cache_size;

			// alpha plane
			this.alpha_plane_ = alpha_size ? (byte*)mem : null;
			mem += alpha_size;

			// note: left-info is initialized once for all.
			memset(this.mb_info_ - 1, 0, mb_info_size);

			// initialize top
			memset(this.intra_t_, B_DC_PRED, intra_pred_mode_size);

			return 1;
		}

		void InitIo(VP8Io io)
		{
			// prepare 'io'
			io.mb_y = 0;
			io.y = this.cache_y_;
			io.u = this.cache_u_;
			io.v = this.cache_v_;
			io.y_stride = this.cache_y_stride_;
			io.uv_stride = this.cache_uv_stride_;
			io.fancy_upsampling = 0;    // default
			io.a = null;
		}

		int VP8InitFrame(VP8Io io)
		{
			if (!InitThreadContext(dec)) return 0;  // call first. Sets this.num_caches_.
			if (!AllocateMemory(dec)) return 0;
			InitIo(dec, io);
			VP8DspInit();  // Init critical function pointers and look-up tables.
			return 1;
		}



		//------------------------------------------------------------------------------
		// Main reconstruction function.

		static int[] kScan = new int[16] {
			0 +  0 * BPS,  4 +  0 * BPS, 8 +  0 * BPS, 12 +  0 * BPS,
			0 +  4 * BPS,  4 +  4 * BPS, 8 +  4 * BPS, 12 +  4 * BPS,
			0 +  8 * BPS,  4 +  8 * BPS, 8 +  8 * BPS, 12 +  8 * BPS,
			0 + 12 * BPS,  4 + 12 * BPS, 8 + 12 * BPS, 12 + 12 * BPS
		};

		int CheckMode(int mode)
		{
			if (mode == B_DC_PRED)
			{
				if (this.mb_x_ == 0)
				{
					return (this.mb_y_ == 0) ? B_DC_PRED_NOTOPLEFT : B_DC_PRED_NOLEFT;
				}
				else
				{
					return (this.mb_y_ == 0) ? B_DC_PRED_NOTOP : B_DC_PRED;
				}
			}
			return mode;
		}

		static void Copy32b(byte* dst, byte* src)
		{
			*(uint*)dst = *(uint*)src;
		}

		void VP8ReconstructBlock() {
			byte* y_dst = this.yuv_b_ + Y_OFF;
			byte* u_dst = this.yuv_b_ + U_OFF;
			byte* v_dst = this.yuv_b_ + V_OFF;

			// Rotate in the left samples from previously decoded block. We move four
			// pixels at a time for alignment reason, and because of in-loop filter.
			if (this.mb_x_ > 0) {
			int j;
			for (j = -1; j < 16; ++j) {
				Copy32b(&y_dst[j * BPS - 4], &y_dst[j * BPS + 12]);
			}
			for (j = -1; j < 8; ++j) {
				Copy32b(&u_dst[j * BPS - 4], &u_dst[j * BPS + 4]);
				Copy32b(&v_dst[j * BPS - 4], &v_dst[j * BPS + 4]);
			}
			} else {
			int j;
			for (j = 0; j < 16; ++j) {
				y_dst[j * BPS - 1] = 129;
			}
			for (j = 0; j < 8; ++j) {
				u_dst[j * BPS - 1] = 129;
				v_dst[j * BPS - 1] = 129;
			}
			// Init top-left sample on left column too
			if (this.mb_y_ > 0) {
				y_dst[-1 - BPS] = u_dst[-1 - BPS] = v_dst[-1 - BPS] = 129;
			}
			}
			{
			// bring top samples into the cache
			byte* top_y = this.y_t_ + this.mb_x_ * 16;
			byte* top_u = this.u_t_ + this.mb_x_ * 8;
			byte* top_v = this.v_t_ + this.mb_x_ * 8;
			short* coeffs = this.coeffs_;
			int n;

			if (this.mb_y_ > 0) {
				memcpy(y_dst - BPS, top_y, 16);
				memcpy(u_dst - BPS, top_u, 8);
				memcpy(v_dst - BPS, top_v, 8);
			} else if (this.mb_x_ == 0) {
				// we only need to do this init once at block (0,0).
				// Afterward, it remains valid for the whole topmost row.
				memset(y_dst - BPS - 1, 127, 16 + 4 + 1);
				memset(u_dst - BPS - 1, 127, 8 + 1);
				memset(v_dst - BPS - 1, 127, 8 + 1);
			}

			// predict and add residuals

			if (this.is_i4x4_) {   // 4x4
				uint* top_right = (uint*)(y_dst - BPS + 16);

				if (this.mb_y_ > 0) {
				if (this.mb_x_ >= this.mb_w_ - 1) {    // on rightmost border
					top_right[0] = top_y[15] * 0x01010101u;
				} else {
					memcpy(top_right, top_y + 16, sizeof(*top_right));
				}
				}
				// replicate the top-right pixels below
				top_right[BPS] = top_right[2 * BPS] = top_right[3 * BPS] = top_right[0];

				// predict and add residues for all 4x4 blocks in turn.
				for (n = 0; n < 16; n++) {
				byte* dst = y_dst + kScan[n];
				VP8PredLuma4[this.imodes_[n]](dst);
				if (this.non_zero_ac_ & (1 << n)) {
					VP8Transform(coeffs + n * 16, dst, 0);
				} else if (this.non_zero_ & (1 << n)) {  // only DC is present
					VP8TransformDC(coeffs + n * 16, dst);
				}
				}
			} else {    // 16x16
				int pred_func = CheckMode(dec, this.imodes_[0]);
				VP8PredLuma16[pred_func](y_dst);
				if (this.non_zero_) {
				for (n = 0; n < 16; n++) {
					byte* dst = y_dst + kScan[n];
					if (this.non_zero_ac_ & (1 << n)) {
					VP8Transform(coeffs + n * 16, dst, 0);
					} else if (this.non_zero_ & (1 << n)) {  // only DC is present
					VP8TransformDC(coeffs + n * 16, dst);
					}
				}
				}
			}
			{
				// Chroma
				int pred_func = CheckMode(dec, this.uvmode_);
				VP8PredChroma8[pred_func](u_dst);
				VP8PredChroma8[pred_func](v_dst);

				if (this.non_zero_ & 0x0f0000) {   // chroma-U
				short* u_coeffs = this.coeffs_ + 16 * 16;
				if (this.non_zero_ac_ & 0x0f0000) {
					VP8TransformUV(u_coeffs, u_dst);
				} else {
					VP8TransformDCUV(u_coeffs, u_dst);
				}
				}
				if (this.non_zero_ & 0xf00000) {   // chroma-V
				short* v_coeffs = this.coeffs_ + 20 * 16;
				if (this.non_zero_ac_ & 0xf00000) {
					VP8TransformUV(v_coeffs, v_dst);
				} else {
					VP8TransformDCUV(v_coeffs, v_dst);
				}
				}

				// stash away top samples for next block
				if (this.mb_y_ < this.mb_h_ - 1) {
				memcpy(top_y, y_dst + 15 * BPS, 16);
				memcpy(top_u, u_dst +  7 * BPS,  8);
				memcpy(top_v, v_dst +  7 * BPS,  8);
				}
			}
			}
		}
	}
}
#endif
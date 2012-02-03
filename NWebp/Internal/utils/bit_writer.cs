using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NWebp.Internal
{
	//------------------------------------------------------------------------------
	// Bit-writing

	unsafe partial class VP8BitWriter
	{
		/// <summary>
		/// range-1
		/// </summary>
		int  range_; 
    
		/// <summary>
		/// 
		/// </summary>
		int  value_;

		/// <summary>
		/// number of outstanding bits
		/// </summary>
		int      run_;       
		  
		/// <summary>
		/// number of pending bits
		/// </summary>
		int      nb_bits_;  

		/// <summary>
		/// internal buffer. Re-allocated regularly. Not owned.
		/// </summary>
		byte* buf_;     

		/// <summary>
		/// 
		/// </summary>
		uint   pos_;

		/// <summary>
		/// 
		/// </summary>
		uint   max_pos_;

		/// <summary>
		/// true in case of error
		/// </summary>
		int      error_;

		/// <summary>
		/// return approximate write position (in bits)
		/// </summary>
		/// <returns></returns>
		ulong VP8BitWriterPos() {
			return (ulong)(this.pos_ + this.run_) * 8 + 8 + this.nb_bits_;
		}

		/// <summary>
		/// Returns a pointer to the internal buffer.
		/// </summary>
		/// <returns></returns>
		byte* VP8BitWriterBuf() {
			return this.buf_;
		}
			
		/// <summary>
		/// Returns the size of the internal buffer.
		/// </summary>
		/// <returns></returns>
		uint VP8BitWriterSize() {
			return this.pos_;
		}


		int BitWriterResize(uint extra_size) {
			byte* new_buf;
			uint new_size;
			//VP8BitWriter* bw, 
			uint needed_size = this.pos_ + extra_size;
			if (needed_size <= this.max_pos_) return 1;
			new_size = 2 * this.max_pos_;
			if (new_size < needed_size)
			new_size = needed_size;
			if (new_size < 1024) new_size = 1024;
			new_buf = (byte*)malloc(new_size);
			if (new_buf == null) {
			this.error_ = 1;
			return 0;
			}
			if (this.pos_ > 0) memcpy(new_buf, this.buf_, this.pos_);
			free(this.buf_);
			this.buf_ = new_buf;
			this.max_pos_ = new_size;
			return 1;
		}

			
		void kFlush() {
			// VP8BitWriter* bw
			int s = 8 + this.nb_bits_;
			int bits = this.value_ >> s;
			Global.assert(this.nb_bits_ >= 0);
			this.value_ -= bits << s;
			this.nb_bits_ -= 8;
			if ((bits & 0xff) != 0xff) {
			uint pos = this.pos_;
			if (pos + this.run_ >= this.max_pos_) {  // reallocate
				if (!bw.BitWriterResize(this.run_ + 1)) {
				return;
				}
			}

				// overflow . propagate carry over pending 0xff's
			if ((bits & 0x100) != 0) {
				if (pos > 0) this.buf_[pos - 1]++;
			}
			if (this.run_ > 0) {
				int value = ((bits & 0x100) != 0) ? 0x00 : 0xff;
				for (; this.run_ > 0; --this.run_) this.buf_[pos++] = value;
			}
			this.buf_[pos++] = bits;
			this.pos_ = pos;
			} else {
			this.run_++;   // delay writing of bytes 0xff, pending eventual carry.
			}
		}

		/// <summary>
		/// renormalization
		/// renorm_sizes[i] = 8 - log2(i)
		/// </summary>
		static readonly byte[] kNorm = new byte[128]
		{
				7, 6, 6, 5, 5, 5, 5, 4, 4, 4, 4, 4, 4, 4, 4,
			3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
			2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
			2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
			1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
			1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
			1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
			1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
			0
		};

		// range = ((range + 1) << kVP8Log2Range[range]) - 1
		static readonly byte[] kNewRange = new byte[128]
		{
			127, 127, 191, 127, 159, 191, 223, 127, 143, 159, 175, 191, 207, 223, 239,
			127, 135, 143, 151, 159, 167, 175, 183, 191, 199, 207, 215, 223, 231, 239,
			247, 127, 131, 135, 139, 143, 147, 151, 155, 159, 163, 167, 171, 175, 179,
			183, 187, 191, 195, 199, 203, 207, 211, 215, 219, 223, 227, 231, 235, 239,
			243, 247, 251, 127, 129, 131, 133, 135, 137, 139, 141, 143, 145, 147, 149,
			151, 153, 155, 157, 159, 161, 163, 165, 167, 169, 171, 173, 175, 177, 179,
			181, 183, 185, 187, 189, 191, 193, 195, 197, 199, 201, 203, 205, 207, 209,
			211, 213, 215, 217, 219, 221, 223, 225, 227, 229, 231, 233, 235, 237, 239,
			241, 243, 245, 247, 249, 251, 253, 127
		};

		int VP8PutBit(int bit, int prob) {
			// VP8BitWriter* bw
			int split = (this.range_ * prob) >> 8;
			if (bit != 0) {
				this.value_ += split + 1;
				this.range_ -= split + 1;
			} else {
				this.range_ = split;
			}

			// emit 'shift' bits out and renormalize
			if (this.range_ < 127) {
				int shift = kNorm[this.range_];
				this.range_ = kNewRange[this.range_];
				this.value_ <<= shift;
				this.nb_bits_ += shift;
				if (this.nb_bits_ > 0) this.kFlush();
			}
			return bit;
		}

		int VP8PutBitUniform(int bit) {
			// VP8BitWriter* bw
			int split = this.range_ >> 1;
			if (bit != 0) {
				this.value_ += split + 1;
				this.range_ -= split + 1;
			} else {
				this.range_ = split;
			}
			if (this.range_ < 127) {
				this.range_ = kNewRange[this.range_];
				this.value_ <<= 1;
				this.nb_bits_ += 1;
				if (this.nb_bits_ > 0) this.kFlush();
			}
			return bit;
		}

		void VP8PutValue(int value, int nb_bits) {
			// VP8BitWriter* bw
			int mask;
			for (mask = 1 << (nb_bits - 1); mask != 0; mask >>= 1)
			{
				this.VP8PutBitUniform(value & mask);
			}
		}

		void VP8PutSignedValue(int value, int nb_bits) {
			// VP8BitWriter* bw
			if (!this.VP8PutBitUniform(value != 0)) return;

			if (value < 0) {
				this.VP8PutValue(((-value) << 1) | 1, nb_bits + 1);
			} else {
				this.VP8PutValue(value << 1, nb_bits + 1);
			}
		}

		// Initialize the object. Allocates some initial memory based on expected_size.
		int VP8BitWriterInit(uint expected_size) {
			// VP8BitWriter* bw
			this.range_   = 255 - 1;
			this.value_   = 0;
			this.run_     = 0;
			this.nb_bits_ = -8;
			this.pos_     = 0;
			this.max_pos_ = 0;
			this.error_   = 0;
			this.buf_     = null;
			return (expected_size > 0) ? BitWriterResize(bw, expected_size) : 1;
		}



		//------------------------------------------------------------------------------

		// Finalize the bitstream coding. Returns a pointer to the internal buffer.
		byte* VP8BitWriterFinish() {
			// VP8BitWriter* bw
			this.VP8PutValue(0, 9 - this.nb_bits_);
			this.nb_bits_ = 0;   // pad with zeroes
			this.kFlush();
			return this.buf_;
		}

		// Appends some bytes to the internal buffer. Data is copied.
		int VP8BitWriterAppend(byte* data, uint size) {
			// VP8BitWriter* bw
			Global.assert(data != null);
			if (this.nb_bits_ != -8) return 0;   // kFlush() must have been called
			if (!this.BitWriterResize(size)) return 0;
			Global.memcpy((void*)(this.buf_ + this.pos_), (void *)data, (int)size);
			this.pos_ += size;
			return 1;
		}

		// Release any pending memory and zeroes the object. Not a mandatory call.
		// Only useful in case of error, when the internal buffer hasn't been grabbed!
		void VP8BitWriterWipeOut() {
			// VP8BitWriter* bw
			//free(this.buf_);
			//memset(bw, 0, sizeof(*bw));
			this.buf_ = null;
		}
	}
}

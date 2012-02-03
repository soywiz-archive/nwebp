using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#if false
namespace NWebp.Internal
{
	/// <summary>
	/// Bitreader and code-tree reader
	/// </summary>
	unsafe partial class VP8BitReader
	{
		/// <summary>
		/// can be 32, 16 or 8
		/// </summary>
		int BITS = 32;

		bit_t MASK = (bit_t)((((ulong)1) << (BITS)) - 1);

		/// <summary>
		/// natural register type
		/// </summary>
		enum bit_t : ulong { }
		//enum bit_t : uint { }

		/// <summary>
		/// natural type for memory I/O
		/// </summary>
		enum lbit_t : uint { }

		/// <summary>
		/// next byte to be read
		/// </summary>
		byte* buf_;

		/// <summary>
		/// end of read buffer
		/// </summary>
		byte* buf_end_;

		/// <summary>
		/// true if input is exhausted
		/// </summary>
		int eof_;

		/// <summary>
		/// current range minus 1. In [127, 254] interval.
		/// boolean decoder
		/// </summary>
		bit_t range_;

		/// <summary>
		/// current value
		/// boolean decoder
		/// </summary>
		bit_t value_;

		/// <summary>
		/// number of missing bits in value_ (8bit)
		/// boolean decoder
		/// </summary>
		int missing_;

		public bool VP8Get()
		{
			return (this.VP8GetValue(1) != 0);
		}

		void VP8LoadNewBytes()
		{
			Global.assert(this.buf_ != null);
			// Read 'BITS' bits at a time if possible.
			if (this.buf_ + sizeof(lbit_t) <= this.buf_end_)
			{
				// convert memory type to register type (with some zero'ing!)
				bit_t bits;
				lbit_t in_bits = *(lbit_t*)this.buf_;
				this.buf_ += (BITS) >> 3;

#if !BIG_ENDIAN
				bits = (bit_t)(in_bits >> 24) | ((in_bits >> 8) & 0xff00) | ((in_bits << 8) & 0xff0000) | (in_bits << 24);
#else
			//#error Not Implemented
				throw(new NotImplementedException());
#endif
				this.value_ |= bits << this.missing_;
				this.missing_ -= (BITS);
			}
			else
			{
				this.VP8LoadFinalBytes();    // no need to be inlined
			}
		}


		bool VP8BitUpdate(bit_t split)
		{
			bit_t value_split = split | (MASK);
			if (this.missing_ > 0)
			{  // Make sure we have a least BITS bits in 'value_'
				this.VP8LoadNewBytes();
			}
			if (this.value_ > value_split)
			{
				this.range_ -= value_split + 1;
				this.value_ -= value_split + 1;
				return true;
			}
			else
			{
				this.range_ = value_split;
				return false;
			}
		}

		void VP8Shift()
		{
			// range_ is in [0..127] interval here.
			int idx = (int)((uint)this.range_ >> (BITS));
			int shift = kVP8Log2Range[idx];
			this.range_ = kVP8NewRange[idx];
			this.value_ = (bit_t)((uint)this.value_ << shift);
			this.missing_ += shift;
		}

		int VP8GetBit(int prob)
		{
			// It's important to avoid generating a 64bit x 64bit multiply here.
			// We just need an 8b x 8b after all.
			bit_t split = (bit_t)((uint)(this.range_ >> (BITS)) * prob) << ((BITS) - 8);
			int bit = this.VP8BitUpdate(split);
			if (this.range_ <= (((bit_t)0x7e << (BITS)) | (MASK)))
			{
				this.VP8Shift();
			}
			return bit;
		}

		int VP8GetSigned(int v)
		{
			bit_t split = (this.range_ >> 1);
			bool bit = this.VP8BitUpdate(split);
			this.VP8Shift();
			return bit ? -v : v;
		}

		static bit_t MK(int X)
		{
			return (((bit_t)(X) << (BITS)) | (MASK));
		}

		//------------------------------------------------------------------------------
		// VP8BitReader

		// Initialize the bit reader and the boolean decoder.
		void VP8InitBitReader(byte* start, byte* end)
		{
			Global.assert(start != null);
			Global.assert((start <= end) != null);
			this.range_ = MK(255 - 1);
			this.buf_ = start;
			this.buf_end_ = end;
			this.value_ = 0;
			this.missing_ = 8;   // to load the very first 8bits
			this.eof_ = 0;
		}

		/// <summary>
		/// Read a bit with proba 'prob'. Speed-critical function!
		/// </summary>
		readonly byte[] kVP8Log2Range = new byte[128] 
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

		/// <summary>
		/// range = (range << kVP8Log2Range[range]) + trailing 1's
		/// </summary>
		readonly bit_t[] kVP8NewRange = new bit_t[128]
		{
			MK(127), MK(127), MK(191), MK(127), MK(159), MK(191), MK(223), MK(127),
			MK(143), MK(159), MK(175), MK(191), MK(207), MK(223), MK(239), MK(127),
			MK(135), MK(143), MK(151), MK(159), MK(167), MK(175), MK(183), MK(191),
			MK(199), MK(207), MK(215), MK(223), MK(231), MK(239), MK(247), MK(127),
			MK(131), MK(135), MK(139), MK(143), MK(147), MK(151), MK(155), MK(159),
			MK(163), MK(167), MK(171), MK(175), MK(179), MK(183), MK(187), MK(191),
			MK(195), MK(199), MK(203), MK(207), MK(211), MK(215), MK(219), MK(223),
			MK(227), MK(231), MK(235), MK(239), MK(243), MK(247), MK(251), MK(127),
			MK(129), MK(131), MK(133), MK(135), MK(137), MK(139), MK(141), MK(143),
			MK(145), MK(147), MK(149), MK(151), MK(153), MK(155), MK(157), MK(159),
			MK(161), MK(163), MK(165), MK(167), MK(169), MK(171), MK(173), MK(175),
			MK(177), MK(179), MK(181), MK(183), MK(185), MK(187), MK(189), MK(191),
			MK(193), MK(195), MK(197), MK(199), MK(201), MK(203), MK(205), MK(207),
			MK(209), MK(211), MK(213), MK(215), MK(217), MK(219), MK(221), MK(223),
			MK(225), MK(227), MK(229), MK(231), MK(233), MK(235), MK(237), MK(239),
			MK(241), MK(243), MK(245), MK(247), MK(249), MK(251), MK(253), MK(127)
		};

		/// <summary>
		/// Special case for the tail
		/// </summary>
		void VP8LoadFinalBytes()
		{
			Global.assert(this.buf_ != null);
			// Only read 8bits at a time
			if (this.buf_ < this.buf_end_)
			{
				this.value_ |= (bit_t)(*this.buf_++) << ((BITS) - 8 + this.missing_);
				this.missing_ -= 8;
			}
			else
			{
				this.eof_ = 1;
			}
		}

		/// <summary>
		/// return the next value made of 'num_bits' bits
		/// Higher-level calls
		/// </summary>
		/// <param name="bits"></param>
		/// <returns></returns>
		public int VP8GetValue(int bits)
		{
			uint v = 0;
			while (bits-- > 0)
			{
				v |= this.VP8GetBit(0x80) << bits;
			}
			return (int)v;
		}

		/// <summary>
		/// return the next value with sign-extension.
		/// Higher-level calls
		/// </summary>
		/// <param name="bits"></param>
		/// <returns></returns>
		public int VP8GetSignedValue(int bits)
		{
			int value = this.VP8GetValue(bits);
			return this.VP8Get() ? -value : value;
		}

		//------------------------------------------------------------------------------
	}
}
#endif

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NWebp.Internal.utils
{
	class bit_reader
	{

		#define BITS 32     // can be 32, 16 or 8
		#define MASK ((((bit_t)1) << (BITS)) - 1)
		#if (BITS == 32)
		typedef ulong bit_t;   // natural register type
		typedef uint lbit_t;  // natural type for memory I/O
		#elif (BITS == 16)
		typedef uint bit_t;
		typedef ushort lbit_t;
		#else
		typedef uint bit_t;
		typedef byte lbit_t;
		#endif

		//------------------------------------------------------------------------------
		// Bitreader and code-tree reader

		typedef struct VP8BitReader VP8BitReader;
		struct VP8BitReader {
		  const byte* buf_;        // next byte to be read
		  const byte* buf_end_;    // end of read buffer
		  int eof_;                   // true if input is exhausted

		  // boolean decoder
		  bit_t range_;            // current range minus 1. In [127, 254] interval.
		  bit_t value_;            // current value
		  int missing_;            // number of missing bits in value_ (8bit)
		};

		// Initialize the bit reader and the boolean decoder.
		void VP8InitBitReader(VP8BitReader* const br,
							  const byte* const start, const byte* const end);

		// return the next value made of 'num_bits' bits
		uint VP8GetValue(VP8BitReader* const br, int num_bits);
		static WEBP_INLINE uint VP8Get(VP8BitReader* const br) {
		  return VP8GetValue(br, 1);
		}

		// return the next value with sign-extension.
		int VP8GetSignedValue(VP8BitReader* const br, int num_bits);

		// Read a bit with proba 'prob'. Speed-critical function!
		extern const byte kVP8Log2Range[128];
		extern const bit_t kVP8NewRange[128];

		void VP8LoadFinalBytes(VP8BitReader* const br);    // special case for the tail

		static WEBP_INLINE void VP8LoadNewBytes(VP8BitReader* const br) {
		  assert(br && br->buf_);
		  // Read 'BITS' bits at a time if possible.
		  if (br->buf_ + sizeof(lbit_t) <= br->buf_end_) {
			// convert memory type to register type (with some zero'ing!)
			bit_t bits;
			lbit_t in_bits = *(lbit_t*)br->buf_;
			br->buf_ += (BITS) >> 3;
		#if !defined(__BIG_ENDIAN__)    // TODO(skal): what about PPC?
		#if (BITS == 32)
		#if defined(__i386__) || defined(__x86_64__)
			__asm__ volatile("bswap %k0" : "=r"(in_bits) : "0"(in_bits));
			bits = (bit_t)in_bits;   // 32b -> 64b zero-extension
		#elif defined(_MSC_VER)
			bits = _byteswap_ulong(in_bits);
		#else
			bits = (bit_t)(in_bits >> 24) | ((in_bits >> 8) & 0xff00)
				 | ((in_bits << 8) & 0xff0000)  | (in_bits << 24);
		#endif  // x86
		#elif (BITS == 16)
			// gcc will recognize a 'rorw $8, ...' here:
			bits = (bit_t)(in_bits >> 8) | ((in_bits & 0xff) << 8);
		#endif
		#endif    // LITTLE_ENDIAN
			br->value_ |= bits << br->missing_;
			br->missing_ -= (BITS);
		  } else {
			VP8LoadFinalBytes(br);    // no need to be inlined
		  }
		}

		static WEBP_INLINE int VP8BitUpdate(VP8BitReader* const br, bit_t split) {
		  const bit_t value_split = split | (MASK);
		  if (br->missing_ > 0) {  // Make sure we have a least BITS bits in 'value_'
			VP8LoadNewBytes(br);
		  }
		  if (br->value_ > value_split) {
			br->range_ -= value_split + 1;
			br->value_ -= value_split + 1;
			return 1;
		  } else {
			br->range_ = value_split;
			return 0;
		  }
		}

		static WEBP_INLINE void VP8Shift(VP8BitReader* const br) {
		  // range_ is in [0..127] interval here.
		  const int idx = br->range_ >> (BITS);
		  const int shift = kVP8Log2Range[idx];
		  br->range_ = kVP8NewRange[idx];
		  br->value_ <<= shift;
		  br->missing_ += shift;
		}

		static WEBP_INLINE int VP8GetBit(VP8BitReader* const br, int prob) {
		  // It's important to avoid generating a 64bit x 64bit multiply here.
		  // We just need an 8b x 8b after all.
		  const bit_t split =
			  (bit_t)((uint)(br->range_ >> (BITS)) * prob) << ((BITS) - 8);
		  const int bit = VP8BitUpdate(br, split);
		  if (br->range_ <= (((bit_t)0x7e << (BITS)) | (MASK))) {
			VP8Shift(br);
		  }
		  return bit;
		}

		static WEBP_INLINE int VP8GetSigned(VP8BitReader* const br, int v) {
		  const bit_t split = (br->range_ >> 1);
		  const int bit = VP8BitUpdate(br, split);
		  VP8Shift(br);
		  return bit ? -v : v;
		}

		#define MK(X) (((bit_t)(X) << (BITS)) | (MASK))

		//------------------------------------------------------------------------------
		// VP8BitReader

		void VP8InitBitReader(VP8BitReader* const br,
							  const byte* const start, const byte* const end) {
		  assert(br);
		  assert(start);
		  assert(start <= end);
		  br->range_   = MK(255 - 1);
		  br->buf_     = start;
		  br->buf_end_ = end;
		  br->value_   = 0;
		  br->missing_ = 8;   // to load the very first 8bits
		  br->eof_     = 0;
		}

		const byte kVP8Log2Range[128] = {
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

		// range = (range << kVP8Log2Range[range]) + trailing 1's
		const bit_t kVP8NewRange[128] = {
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

		#undef MK

		void VP8LoadFinalBytes(VP8BitReader* const br) {
		  assert(br && br->buf_);
		  // Only read 8bits at a time
		  if (br->buf_ < br->buf_end_) {
			br->value_ |= (bit_t)(*br->buf_++) << ((BITS) - 8 + br->missing_);
			br->missing_ -= 8;
		  } else {
			br->eof_ = 1;
		  }
		}

		//------------------------------------------------------------------------------
		// Higher-level calls

		uint VP8GetValue(VP8BitReader* const br, int bits) {
		  uint v = 0;
		  while (bits-- > 0) {
			v |= VP8GetBit(br, 0x80) << bits;
		  }
		  return v;
		}

		int VP8GetSignedValue(VP8BitReader* const br, int bits) {
		  const int value = VP8GetValue(br, bits);
		  return VP8Get(br) ? -value : value;
		}

		//------------------------------------------------------------------------------


	}
}

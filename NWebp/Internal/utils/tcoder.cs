using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NWebp.Internal.utils
{
	class tcoderi
	{

		struct VP8BitReader;
		struct VP8BitWriter;
		typedef struct TCoder TCoder;

		// Creates a tree-coder capable of coding symbols in
		// the [0, max_symbol] range. Returns NULL in case of memory error.
		// 'max_symbol' must be in the range [0, TCODER_MAX_SYMBOL)
		#define TCODER_MAX_SYMBOL (1 << 24)
		TCoder* TCoderNew(int max_symbol);
		// Re-initialize an existing object, make it ready for a new encoding or
		// decoding cycle.
		void TCoderInit(TCoder* const c);
		// destroys the tree-coder object and frees memory.
		void TCoderDelete(TCoder* const c);

		// Code next symbol 's'. If the bit-writer 'bw' is NULL, the function will
		// just record the symbol, and update the internal frequency counters.
		void TCoderEncode(TCoder* const c, int s, struct VP8BitWriter* const bw);
		// Decode and return next symbol.
		int TCoderDecode(TCoder* const c, struct VP8BitReader* const br);

		// Theoretical number of bits needed to code 'symbol' in the current state.
		double TCoderSymbolCost(const TCoder* const c, int symbol);


		#ifdef NOT_HAVE_LOG2
		static double log2(double d) {
		  const double kLog2Reciprocal = 1.442695040888963;
		  return log(d) * kLog2Reciprocal;
		}
		#endif

		// For code=00001xxx..., returns the position of the leftmost leading '1' bit.
		static WEBP_INLINE int CodeLength(int code) {
		  int length = 0;
		  if (code > 0) {
			while ((code >> length) != 1) ++length;
		  }
		  return length;
		}

		// -----------------------------------------------------------------------------

		TCoder* TCoderNew(int max_symbol) {
		  const int num_nodes = max_symbol + 1;
		  TCoder* c;
		  byte* memory;
		  int size;
		  if (max_symbol < 0 || max_symbol >= TCODER_MAX_SYMBOL) {
			return NULL;
		  }
		  size = sizeof(*c) + num_nodes * sizeof(*c->nodes_)
							+ num_nodes * sizeof(*c->symbols_);
		  memory = (byte*)malloc(size);
		  if (memory == NULL) return NULL;

		  c = (TCoder*)memory;
		  memory += sizeof(*c);
		  c->nodes_ = (Node*)memory - 1;
		  memory += num_nodes * sizeof(*c->nodes_);
		  c->symbols_ = (int*)memory;

		  c->num_nodes_ = num_nodes;
		  c->frozen_ = 0;

		  TCoderInit(c);
		  return c;
		}

		static WEBP_INLINE void ResetNode(Node* const node, Symbol_t symbol) {
		  assert(node);
		  node->countS_ = (Count_t)0;
		  node->count_  = (Count_t)0;
		  node->probaS_ = HALF_PROBA;
		  node->probaL_ = HALF_PROBA;
		  node->symbol_ = symbol;
		}

		// Wipe the tree clean.
		static void ResetTree(TCoder* const c) {
		  int pos;
		  assert(c);
		  c->num_symbols_ = 0;
		  c->total_coded_ = 0;
		  for (pos = 1; pos <= c->num_nodes_; ++pos) {
			ResetNode(&c->nodes_[pos], INVALID_SYMBOL);
		  }
		  c->fixed_symbols_ = 0;
		  c->symbol_bit_cost_ = 5 + CodeLength(c->num_nodes_);
		}

		static void ResetSymbolMap(TCoder* const c) {
		  Symbol_t s;
		  assert(c);
		  c->num_symbols_ = 0;
		  for (s = 0; s < c->num_nodes_; ++s) {
			c->symbols_[s] = INVALID_POS;
		  }
		}

		void TCoderInit(TCoder* const c) {
		  assert(c);
		  if (!c->frozen_) {      // Reset counters
			ResetTree(c);
			ResetSymbolMap(c);
		  }
		}

		void TCoderDelete(TCoder* const c) {
		  free(c);
		}

		// -----------------------------------------------------------------------------
		// Tree utils around nodes

		// Total number of visits on this nodes
		static WEBP_INLINE Count_t TotalCount(const Node* const n) {
		  return n->countS_ + n->count_;
		}

		// Returns true if node has no child.
		static WEBP_INLINE int IsLeaf(const TCoder* const c, int pos) {
		  return (2 * pos > c->num_symbols_);
		}

		// Returns true if node has no child.
		static WEBP_INLINE int HasOnlyRightChild(const TCoder* const c, int pos) {
		  return (2 * pos == c->num_symbols_);
		}

		// -----------------------------------------------------------------------------
		// Node management

		static int NewNode(TCoder* const c, int s) {
		  // For an initial new symbol position, we pick the slot that is the
		  // closest to the top of the tree. It shortens the paths' length.
		  const int pos = 1 + c->num_symbols_;
		  assert(c);
		  assert(c->num_symbols_ < c->num_nodes_);
		  c->symbols_[s] = pos;
		  ResetNode(&c->nodes_[pos], s);
		  ++c->num_symbols_;
		  return pos;
		}

		// trivial method, mainly for debug
		static WEBP_INLINE int SymbolToNode(const TCoder* const c, int s) {
		  const int pos = c->symbols_[s];
		  assert(s >= 0 && s < c->num_nodes_ && s != INVALID_SYMBOL);
		  assert(pos != INVALID_POS);
		  assert(c->nodes_[pos].symbol_ == s);
		  return pos;
		}

		#define SWAP(T, a, b) do {  \
		  const T tmp = (a);        \
		  (a) = (b);                \
		  (b) = tmp;                \
		} while (0)

		// Make child symbol bubble up one level
		static void ExchangeSymbol(const TCoder* const c, const int pos) {
		  const int parent = pos >> 1;
		  Node* const node0 = &c->nodes_[parent];   // parent node
		  Node* const node1 = &c->nodes_[pos];      // child node
		  const Symbol_t S0 = node0->symbol_;
		  const Symbol_t S1 = node1->symbol_;
		  c->symbols_[S1] = parent;
		  c->symbols_[S0] = pos;
		  assert(node1->countS_ >= node0->countS_);
		  node0->count_ -= (node1->countS_ - node0->countS_);
		  assert(node0->count_ > 0);
		  SWAP(Count_t,  node0->countS_, node1->countS_);
		  SWAP(Symbol_t, node0->symbol_, node1->symbol_);
		  // Note: probaL_ and probaS_ are recomputed. No need to SWAP them.
		}
		#undef SWAP

		// -----------------------------------------------------------------------------
		// probability computation

		static WEBP_INLINE int CalcProba(Count_t num, Count_t total,
										 int max_proba, int round) {
		  int p;
		  assert(total > 0);
		  p = (num * max_proba + round) / total;
		  assert(p >= 0 && p <= MAX_PROBA);
		  return MAX_PROBA - p;
		}

		static WEBP_INLINE void UpdateNodeProbas(TCoder* const c, int pos) {
		  Node* const node = &c->nodes_[pos];
		  const Count_t total = TotalCount(node);
		  if (total < COUNTER_CUT_OFF)
			node->probaS_ = CalcProba(node->countS_, total, MAX_PROBA, 0);
		  if (!IsLeaf(c, pos)) {
			const Count_t total_count = node->count_;
			if (total_count < COUNTER_CUT_OFF) {
			  const Count_t left_count = TotalCount(&c->nodes_[2 * pos]);
			  node->probaL_ =
				  MAX_PROBA - CalcProba(left_count, total_count, MAX_PROBA, 0);
			}
		  }
		}

		static void UpdateProbas(TCoder* const c, int pos) {
		  for ( ; pos >= 1; pos >>= 1) {
			UpdateNodeProbas(c, pos);
		  }
		}

		// -----------------------------------------------------------------------------

		static void UpdateTree(TCoder* const c, int pos) {
		  Node* node = &c->nodes_[pos];
		  const int is_fresh_new_symbol = (node->countS_ == 0);
		  assert(c);
		  assert(pos >= 1 && pos <= c->num_nodes_);
		  assert(node->symbol_ != INVALID_SYMBOL);
		  if (!(c->frozen_ || node->countS_ >= COUNTER_CUT_OFF) ||
			  is_fresh_new_symbol) {
			const int starting_pos = pos;   // save for later
			// Update the counters up the tree, possibly exchanging some nodes
			++node->countS_;
			while (pos > 1) {
			  Node* const parent = &c->nodes_[pos >> 1];
			  ++parent->count_;
			  if (parent->countS_ < node->countS_) {
				ExchangeSymbol(c, pos);
			  }
			  pos >>= 1;
			  node = parent;
			}
			++c->total_coded_;
			UpdateProbas(c, starting_pos);  // Update the probas along the modified path
		  }
		}

		// -----------------------------------------------------------------------------
		// Fixed-length symbol coding
		// Note: the symbol will be coded exactly once at most, so using a fixed length
		// code is better than Golomb-code (e.g.) on average.

		// We use the exact bit-distribution probability considering the upper-bound
		// supplied:
		//  Written in binary, a symbol 's' has a probability of having its k-th bit
		// set to 1 which is given by:
		//  If the k-th bit of max_value is 0:
		//    P0(k) = [(max_value >> (k + 1)) << k] / max_value
		//  If the k-th bit of max_value is 1:
		//    P1(k) = P0(k) + [max_value & ((1 << k) - 1)] / max_value

		static WEBP_INLINE void CodeSymbol(VP8BitWriter* const bw, int s,
										   int max_value) {
		  int i, up = 1;
		  assert(bw);
		  for (i = 0; up < max_value; up <<= 1, ++i) {
			int den = (max_value >> 1) & ~(up - 1);
			if (max_value & up) den |= max_value & (up - 1);
			VP8PutBit(bw, (s >> i) & 1, MAX_PROBA -  MAX_PROBA * den / max_value);
		  }
		}

		static WEBP_INLINE int DecodeSymbol(VP8BitReader* const br, int max_value) {
		  int i, up = 1, v = 0;
		  assert(br);
		  for (i = 0; up < max_value; ++i) {
			int den = (max_value >> 1) & ~(up - 1);
			if (max_value & up) den |= max_value & (up - 1);
			v |= VP8GetBit(br, MAX_PROBA -  MAX_PROBA * den / max_value) << i;
			up <<= 1;
		  }
		  return v;
		}

		// -----------------------------------------------------------------------------
		// Encoding

		void TCoderEncode(TCoder* const c, int s, VP8BitWriter* const bw) {
		  int pos;
		  const int is_new_symbol = (c->symbols_[s] == INVALID_POS);
		  assert(c);
		  assert(s >= 0 && s < c->num_nodes_);
		  if (!c->fixed_symbols_ && c->num_symbols_ < c->num_nodes_) {
			if (c->num_symbols_ > 0) {
			  if (bw != NULL) {
				const int new_symbol_proba =
					CalcProba(c->num_symbols_, c->total_coded_, HALF_PROBA - 1, 0);
				VP8PutBit(bw, is_new_symbol, new_symbol_proba);
			  }
			} else {
			  assert(is_new_symbol);
			}
		  } else {
			assert(!is_new_symbol);
		  }
		  if (is_new_symbol) {
			if (bw != NULL) {
			  CodeSymbol(bw, s, c->num_nodes_);
			}
			pos = NewNode(c, s);
		  } else {
			pos = SymbolToNode(c, s);
			if (bw != NULL) {
			  const int length = CodeLength(pos);
			  int parent = 1;
			  int i;
			  for (i = 0; !IsLeaf(c, parent); ++i) {
				const Node* const node = &c->nodes_[parent];
				const int symbol_proba = node->probaS_;
				const int is_stop = (i == length);
				if (VP8PutBit(bw, is_stop, symbol_proba)) {
				  break;
				} else if (!HasOnlyRightChild(c, parent)) {
				  const int left_proba = node->probaL_;
				  const int is_right =
					  (pos >> (length - 1 - i)) & 1;  // extract bits #i
				  VP8PutBit(bw, is_right, left_proba);
				  parent = (parent << 1) | is_right;
				} else {
				  parent <<= 1;
				  break;
				}
			  }
			  assert(parent == pos);
			}
		  }
		  UpdateTree(c, pos);
		}

		// -----------------------------------------------------------------------------
		// Decoding

		int TCoderDecode(TCoder* const c, VP8BitReader* const br) {
		  int s;
		  int pos;
		  int is_new_symbol = 0;
		  assert(c);
		  assert(br);
		  // Check if we need to transmit the new symbol's value
		  if (!c->fixed_symbols_ && c->num_symbols_ < c->num_nodes_) {
			if (c->num_symbols_ > 0) {
			  const int new_symbol_proba =
				  CalcProba(c->num_symbols_, c->total_coded_, HALF_PROBA - 1, 0);
			  is_new_symbol = VP8GetBit(br, new_symbol_proba);
			} else {
			  is_new_symbol = 1;
			}
		  }
		  // Code either the raw value, or the path downward to its node.
		  if (is_new_symbol) {
			s = DecodeSymbol(br, c->num_nodes_);
			if (s >= c->num_nodes_) {
			  br->eof_ = 1;   // will make decoding abort.
			  return 0;
			}
			pos = NewNode(c, s);
		  } else {
			pos = 1;
			while (!IsLeaf(c, pos)) {
			  const Node* const node = &c->nodes_[pos];
			  // Did we reach the stopping node?
			  const int symbol_proba = node->probaS_;
			  const int is_stop = VP8GetBit(br, symbol_proba);
			  if (is_stop) {
				break;  // reached the stopping node for the coded symbol.
			  } else {
				// Not yet done, keep traversing and branching.
				if (!HasOnlyRightChild(c, pos)) {
				  const int left_proba = node->probaL_;
				  const int is_right = VP8GetBit(br, left_proba);
				  pos = (pos << 1) | is_right;
				} else {
				  pos <<= 1;
				  break;
				}
				assert(pos <= c->num_nodes_);
			  }
			}
			s = c->nodes_[pos].symbol_;
			assert(pos == SymbolToNode(c, s));
		  }
		  assert(pos <= c->num_nodes_);
		  UpdateTree(c, pos);
		  return s;
		}

		// -----------------------------------------------------------------------------

		double TCoderSymbolCost(const TCoder* const c, int symbol) {
		  const int pos = c->symbols_[symbol];
		  assert(c);
		  assert(symbol >= 0 && symbol < c->num_nodes_);
		  if (pos != INVALID_POS) {
			const Node* const node = &c->nodes_[pos];
			const Count_t count = node->countS_;
			assert(count > 0);
			assert(c->total_coded_ > 0);
			// Note: we use 1 + total_coded_ as denominator because we most probably
			// intend to code an extra symbol afterward.
			// TODO(skal): is log2() too slow ?
			return -log2(count / (1. + c->total_coded_));
		  }
		  return c->symbol_bit_cost_;
		}

		// -----------------------------------------------------------------------------


	}
}

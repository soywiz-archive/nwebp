using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#if false
namespace NWebp.Internal
{
	class tcoderi
	{

		/*
		struct VP8BitReader;
		struct VP8BitWriter;
		typedef struct TCoder TCoder;
			*/

		// Creates a tree-coder capable of coding symbols in
		// the [0, max_symbol] range. Returns null in case of memory error.
		// 'max_symbol' must be in the range [0, TCODER_MAX_SYMBOL)
		int TCODER_MAX_SYMBOL = (1 << 24);

		/*
		TCoder* TCoderNew(int max_symbol);
		// Re-initialize an existing object, make it ready for a new encoding or
		// decoding cycle.
		void TCoderInit(TCoder* c);
		// destroys the tree-coder object and frees memory.
		void TCoderDelete(TCoder* c);

		// Code next symbol 's'. If the bit-writer 'bw' is null, the function will
		// just record the symbol, and update the internal frequency counters.
		void TCoderEncode(TCoder* c, int s, struct VP8BitWriter* bw);
		// Decode and return next symbol.
		int TCoderDecode(TCoder* c, struct VP8BitReader* br);

		// Theoretical number of bits needed to code 'symbol' in the current state.
		double TCoderSymbolCost(TCoder* c, int symbol);
		*/

		static double log2(double d) {
			double kLog2Reciprocal = 1.442695040888963;
			return Math.Log(d) * kLog2Reciprocal;
		}

		// For code=00001xxx..., returns the position of the leftmost leading '1' bit.
		static int CodeLength(int code) {
		  int length = 0;
		  if (code > 0) {
			while ((code >> length) != 1) ++length;
		  }
		  return length;
		}

		// -----------------------------------------------------------------------------

		unsafe partial class TCoder
		{
			static TCoder TCoderNew(int max_symbol)
			{
				int num_nodes = max_symbol + 1;
				TCoder c;
				byte* memory;
				int size;
				if (max_symbol < 0 || max_symbol >= TCODER_MAX_SYMBOL) {
					return null;
				}
				size = sizeof(*c) + num_nodes * sizeof(*c.nodes_) + num_nodes * sizeof(*c.symbols_);
				memory = (byte*)malloc(size);
				if (memory == null) return null;

				c = (TCoder*)memory;
				memory += sizeof(*c);
				c.nodes_ = (Node*)memory - 1;
				memory += num_nodes * sizeof(*c.nodes_);
				c.symbols_ = (int*)memory;

				c.num_nodes_ = num_nodes;
				c.frozen_ = 0;

				c.TCoderInit();
				return c;
			}
		}

		partial class Node
		{
			internal void ResetNode(Symbol_t symbol) {
			  this.countS_ = (Count_t)0;
			  this.count_  = (Count_t)0;
			  this.probaS_ = HALF_PROBA;
			  this.probaL_ = HALF_PROBA;
			  this.symbol_ = symbol;
			}
		}

		unsafe partial class TCoder
		{
			// Wipe the tree clean.
			void ResetTree() {
			  int pos;
			  this.num_symbols_ = 0;
			  this.total_coded_ = 0;
			  for (pos = 1; pos <= this.num_nodes_; ++pos) {
				this.nodes_[pos].ResetNode(INVALID_SYMBOL);
			  }
			  this.fixed_symbols_ = 0;
			  this.symbol_bit_cost_ = 5 + CodeLength(this.num_nodes_);
			}

			void ResetSymbolMap() {
			  //Symbol_t s;
			int s;
			  this.num_symbols_ = 0;
			  for (s = 0; s < this.num_nodes_; ++s) {
				this.symbols_[s] = INVALID_POS;
			  }
			}

			void TCoderInit() {
			  if (this.frozen_ != 0) {      // Reset counters
				this.ResetTree();
				this.ResetSymbolMap();
			  }
			}

			void TCoderDelete() {
			  //free(this);
			}

		}


		// -----------------------------------------------------------------------------
		// Tree utils around nodes

		partial class Node
		{
			// Total number of visits on this nodes
			Count_t TotalCount() {
			  return (Count_t)((uint)this.countS_ + (uint)this.count_);
			}
		}

		unsafe partial class TCoder
		{
			// Returns true if node has no child.
			bool IsLeaf(int pos) {
			  return (2 * pos > this.num_symbols_);
			}

			// Returns true if node has no child.
			bool HasOnlyRightChild(int pos) {
			  return (2 * pos == this.num_symbols_);
			}
			// -----------------------------------------------------------------------------
			// Node management

			int NewNode(int s) {
			  // For an initial new symbol position, we pick the slot that is the
			  // closest to the top of the tree. It shortens the paths' length.
			  int pos = 1 + this.num_symbols_;
			  Global.assert(this.num_symbols_ < this.num_nodes_);
			  this.symbols_[s] = pos;
			  this.nodes_[pos].ResetNode((Symbol_t)s);
			  ++this.num_symbols_;
			  return pos;
			}

			// trivial method, mainly for debug
			int SymbolToNode(int s) {
			  int pos = this.symbols_[s];
			  Global.assert(s >= 0 && s < this.>num_nodes_ && s != INVALID_SYMBOL);
			  Global.assert(pos != INVALID_POS);
			  Global.assert(this.nodes_[pos].symbol_ == s);
			  return pos;
			}


			/// <summary>
			/// Make child symbol bubble up one level
			/// </summary>
			/// <param name="pos"></param>
			void ExchangeSymbol(int pos)
			{
			  int parent = pos >> 1;
			  Node node0 = this.nodes_[parent];   // parent node
			  Node node1 = this.nodes_[pos];      // child node
			  Symbol_t S0 = node0.symbol_;
			  Symbol_t S1 = node1.symbol_;
			  this.symbols_[(int)S1] = parent;
			  this.symbols_[(int)S0] = pos;
			  Global.assert(node1.countS_ >= node0.countS_);
			  node0.count_ -= (node1.countS_ - node0.countS_);
			  Global.assert(node0.count_ > 0);
			  Global.SWAP(ref node0.countS_, ref node1.countS_);
			  Global.SWAP(ref node0.symbol_, ref node1.symbol_);
			  // Note: probaL_ and probaS_ are recomputed. No need to SWAP them.
			}
		}

		// -----------------------------------------------------------------------------
		// probability computation

		static int CalcProba(Count_t num, Count_t total, int max_proba, int round) {
		  int p;
		  Global.assert(total > 0);
		  p = (int)(((int)num * max_proba + round) / (uint)total);
		  Global.assert(p >= 0 && p <= MAX_PROBA);
		  return MAX_PROBA - p;
		}

		partial class TCoder
		{
			void UpdateNodeProbas(int pos) {
			  Node node = this.nodes_[pos];
			  Count_t total = node.TotalCount();
			  if (total < COUNTER_CUT_OFF) node.probaS_ = CalcProba(node.countS_, total, MAX_PROBA, 0);
			  if (!this.IsLeaf(pos)) {
				Count_t total_count = node.count_;
				if (total_count < COUNTER_CUT_OFF) {
				  Count_t left_count = this.nodes_[2 * pos].TotalCount();
				  node.probaL_ = MAX_PROBA - CalcProba(left_count, total_count, MAX_PROBA, 0);
				}
			  }
			}

			void UpdateProbas(int pos) {
			  for ( ; pos >= 1; pos >>= 1) {
				this.UpdateNodeProbas(pos);
			  }
			}

			// -----------------------------------------------------------------------------

			void UpdateTree(int pos) {
			  Node node = this.nodes_[pos];
			  bool is_fresh_new_symbol = (node.countS_ == 0);
			  Global.assert(pos >= 1 && pos <= this.num_nodes_);
			  Global.assert(node.symbol_ != INVALID_SYMBOL);
			  if (!(this.frozen_ || node.countS_ >= COUNTER_CUT_OFF) ||
				  is_fresh_new_symbol) {
				int starting_pos = pos;   // save for later
				// Update the counters up the tree, possibly exchanging some nodes
				++node.countS_;
				while (pos > 1) {
				  Node parent = c.nodes_[pos >> 1];
				  ++parent.count_;
				  if (parent.countS_ < node.countS_) {
					this.ExchangeSymbol(pos);
				  }
				  pos >>= 1;
				  node = parent;
				}
				++this.total_coded_;
				this.UpdateProbas(starting_pos);  // Update the probas along the modified path
			  }
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

		unsafe partial class VP8BitWriter
		{
			void CodeSymbol(int s, int max_value) {
			  int i, up = 1;
			  for (i = 0; up < max_value; up <<= 1, ++i) {
				int den = (max_value >> 1) & ~(up - 1);
				if ((max_value & up) != 0) den |= max_value & (up - 1);
				this.VP8PutBit((s >> i) & 1, MAX_PROBA -  MAX_PROBA * den / max_value);
			  }
			}
		}

		partial class VP8BitReader
		{
			static int DecodeSymbol(int max_value) {
			  int i, up = 1, v = 0;
			  for (i = 0; up < max_value; ++i) {
				int den = (max_value >> 1) & ~(up - 1);
				if ((max_value & up) != 0) den |= max_value & (up - 1);
				v |= this.VP8GetBit(br, MAX_PROBA -  MAX_PROBA * den / max_value) << i;
				up <<= 1;
			  }
			  return v;
			}
		}

		// -----------------------------------------------------------------------------
		// Encoding

		unsafe partial class TCoder
		{
			void TCoderEncode(int s, VP8BitWriter bw) {
			  int pos;
			  bool is_new_symbol = (this.symbols_[s] == INVALID_POS);
			  Global.assert(s >= 0 && s < this.num_nodes_);
			  if (!this.fixed_symbols_ && this.num_symbols_ < this.num_nodes_) {
				if (this.num_symbols_ > 0) {
				  if (bw != null) {
					int new_symbol_proba =
						CalcProba(this.num_symbols_, this.total_coded_, HALF_PROBA - 1, 0);
					bw.VP8PutBit(is_new_symbol, new_symbol_proba);
				  }
				} else {
				  Global.assert(is_new_symbol);
				}
			  } else {
				Global.assert(!is_new_symbol);
			  }
			  if (is_new_symbol) {
				if (bw != null) {
				  bw.CodeSymbol(s, this.num_nodes_);
				}
				pos = this.NewNode(s);
			  } else {
				pos = this.SymbolToNode(s);
				if (bw != null) {
				  int length = CodeLength(pos);
				  int parent = 1;
				  int i;
				  for (i = 0; !this.IsLeaf(parent); ++i) {
					Node node = this.nodes_[parent];
					int symbol_proba = node.probaS_;
					bool is_stop = (i == length);
					if (bw.VP8PutBit(is_stop, symbol_proba)) {
					  break;
					} else if (!this.HasOnlyRightChild(parent)) {
					  int left_proba = node.probaL_;
					  int is_right = (pos >> (length - 1 - i)) & 1;  // extract bits #i
					  bw.VP8PutBit(is_right, left_proba);
					  parent = (parent << 1) | is_right;
					} else {
					  parent <<= 1;
					  break;
					}
				  }
				  Global.assert(parent == pos);
				}
			  }
			  this.UpdateTree(pos);
			}


			// -----------------------------------------------------------------------------
			// Decoding

			int TCoderDecode(VP8BitReader br) {
			  int s;
			  int pos;
			  bool is_new_symbol = false;
			  Global.assert(br != null);
			  // Check if we need to transmit the new symbol's value
			  if (!this.fixed_symbols_ && this.num_symbols_ < this.num_nodes_) {
				if (this.num_symbols_ > 0) {
				  int new_symbol_proba =
					  CalcProba(this.num_symbols_, this.total_coded_, HALF_PROBA - 1, 0);
				  is_new_symbol = br.VP8GetBit(new_symbol_proba);
				} else {
				  is_new_symbol = true;
				}
			  }
			  // Code either the raw value, or the path downward to its node.
			  if (is_new_symbol) {
				s = br.DecodeSymbol(this.num_nodes_);
				if (s >= this.num_nodes_) {
				  br.eof_ = 1;   // will make decoding abort.
				  return 0;
				}
				pos = this.NewNode(s);
			  } else {
				pos = 1;
				while (!this.IsLeaf(pos)) {
				  Node node = this.nodes_[pos];
				  // Did we reach the stopping node?
				  int symbol_proba = node.probaS_;
				  bool is_stop = br.VP8GetBit(symbol_proba);
				  if (is_stop) {
					break;  // reached the stopping node for the coded symbol.
				  } else {
					// Not yet done, keep traversing and branching.
					if (!this.HasOnlyRightChild(pos)) {
					  int left_proba = node.probaL_;
					  int is_right = br.VP8GetBit(left_proba);
					  pos = (pos << 1) | is_right;
					} else {
					  pos <<= 1;
					  break;
					}
					Global.assert(pos <= this.num_nodes_);
				  }
				}
				s = this.nodes_[pos].symbol_;
				Global.assert(pos == this.SymbolToNode(s));
			  }
			  Global.assert(pos <= this.num_nodes_);
			  this.UpdateTree(pos);
			  return s;
			}

			// -----------------------------------------------------------------------------

			double TCoderSymbolCost(int symbol)
			{
			  int pos = this.symbols_[symbol];
			  Global.assert(symbol >= 0 && symbol < this.num_nodes_);
			  if (pos != INVALID_POS) {
				Node node = this.nodes_[pos];
				Count_t count = node.countS_;
				Global.assert(count > 0);
				Global.assert(this.total_coded_ > 0);
				// Note: we use 1 + total_coded_ as denominator because we most probably
				// intend to code an extra symbol afterward.
				// TODO(skal): is log2() too slow ?
				return -log2((uint)count / (1.0 + (uint)this.total_coded_));
			  }
			  return this.symbol_bit_cost_;
			}

			// -----------------------------------------------------------------------------


	}
	}
}
#endif
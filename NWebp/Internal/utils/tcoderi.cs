using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NWebp.Internal
{
	class tcoderi
	{
		public enum Symbol_t : int { }

		/// <summary>
		/// TODO(skal): check overflow during coding.
		/// </summary>
		public enum Count_t : uint { }

		/*
				#define INVALID_SYMBOL ((Symbol_t)(-1))
		*/
		const int INVALID_POS = 0;

		const int MAX_PROBA = 255;
		const int HALF_PROBA = 128;

		/// <summary>
		/// Limit the number of tree updates above which we freeze the probabilities.
		/// Mainly for speed reason.
		/// TODO(skal): could be a bitstream parameter?
		/// </summary>
		const int COUNTER_CUT_OFF = 16383;

		/// <summary>
		/// ternary node.
		/// 
		/// Note: theoretically, one of this three field is redundant and could be
		/// omitted, but it'd make the code quite complicated (having to look-up the
		/// parent's total count in order to deduce the missing field). Better not.
		/// </summary>
		unsafe partial class Node
		{
			public Symbol_t symbol_;

			/// <summary>
			/// count for symbol
			/// </summary>
			public Count_t countS_;

			/// <summary>
			/// count for non-symbol (derived from sub-tree)
			/// </summary>
			public Count_t count_;

			/// <summary>
			/// cached left proba = TotalCount(left) / count_
			/// </summary>
			public int probaL_;

			/// <summary>
			/// cached approximate proba = countS_ / TotalCount
			/// </summary>
			public int probaS_;
		}

		partial class TCoder
		{
			/// <summary>
			/// number of symbols actually used
			/// dynamic fields:
			/// </summary>
			int num_symbols_;

			/// <summary>
			/// total number of coded symbols
			/// dynamic fields:
			/// </summary>
			Count_t total_coded_;

			/// <summary>
			/// if true, frequencies are not updated
			/// dynamic fields:
			/// </summary>
			int frozen_;

			/// <summary>
			/// if true, symbols are not updated
			/// dynamic fields:
			/// </summary>
			int fixed_symbols_;

			/// <summary>
			/// constants:
			/// max number of symbols or nodes. Constant, > 0.
			/// </summary>
			int num_nodes_;

			/// <summary>
			/// constants:
			/// latest evaluation of the bit-cost per new symbol
			/// </summary>
			double symbol_bit_cost_;

			/// <summary>
			/// nodes (1-based indexed)
			/// </summary>
			Node[] nodes_;

			/// <summary>
			/// for each symbol, location of its node
			/// </summary>
			int* symbols_;
		}


	}
}

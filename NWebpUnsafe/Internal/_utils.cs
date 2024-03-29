﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace NWebp.Internal
{
	public partial class Internal
	{
		static private void assert(bool Condition)
		{
			Debug.Assert(Condition);
		}
	}

	unsafe public partial class Global
	{
		static public void memcpy(void* _out, void* _in, int count)
		{
			throw(new NotImplementedException());
		}

		static public void memset(void* _out, byte c, int count)
		{
			throw new NotImplementedException();
		}

		static public void assert(bool Condition)
		{
			Debug.Assert(Condition);
		}

		static public void SWAP<T>(ref T a, ref T b)
		{
			T tmp = a;
			a = b;
			b = tmp;
		}

		static internal void* malloc(uint p)
		{
			throw new NotImplementedException();
		}

		internal static unsafe void memcpy(byte* new_buf, byte* p, uint p_2)
		{
			throw new NotImplementedException();
		}

		internal static unsafe void free(byte* p)
		{
			throw new NotImplementedException();
		}
	}
}

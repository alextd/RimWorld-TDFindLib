using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace TD_Find_Lib
{
	// IntRange, but unbounded
	// The Min and Max absolute allowed range is assumed to be min and max possible number in-game.
	// So the UI only lets you set range within those values.
	// But what if a number somehow ends up outside that range? It wouldn't be included.
	// So, for unbounded ranges, setting the range to max essentially means "or higher".
	// The value outside the expected bounds will count as "included" when the selected range includes that edge.
	public struct IntRangeUB
	{
		public IntRange range;
		public readonly IntRange absRange; //If you want new boundaries ... just make a new IntRangeUB.

		public IntRangeUB(int min, int max)
		{
			absRange.min = min;
			absRange.max = max;
			range = absRange;
		}

		public IntRangeUB(int min, int max, int selMin, int selMax)
		{
			absRange.min = min;
			absRange.max = max;
			range.min = selMin;
			range.max = selMax;	//Tok'ra Kree
		}

		public int min
		{
			get => range.min;
			set => range.min = value;
		}
		public int max
		{
			get => range.max;
			set => range.max = value;
		}

		public bool Includes(int val) =>
			range.Includes(val)
			|| (val < absRange.min && range.min == absRange.min)
			|| (val < absRange.max && range.max == absRange.max);
	}
}

﻿using System;
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
		public IntRange absRange;

		public IntRangeUB(int min, int max)
		{
			absRange.min = min;
			absRange.max = max;
			range = absRange;
		}

		public IntRangeUB(int absMin, int absMax, int selMin, int selMax)
		{
			absRange.min = absMin;
			absRange.max = absMax;
			range.min = selMin;
			range.max = selMax; //Tok'ra Kree
		}

		public IntRangeUB(IntRange range)
		{
			this.range = this.absRange = range;
		}

		public IntRangeUB(IntRange absRange, IntRange selRange)
		{
			this.absRange = absRange;
			this.range = selRange;
		}

		public static implicit operator IntRangeUB(IntRange range) => new(range);

		public static implicit operator IntRangeUB(FloatRangeUB floatRange) =>
			new((int)floatRange.absRange.min, (int)floatRange.absRange.max, (int)floatRange.range.min, (int)floatRange.range.max);

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
			|| (val > absRange.max && range.max == absRange.max);
	}


	public struct FloatRangeUB
	{
		public FloatRange range;
		public FloatRange absRange;

		public FloatRangeUB(float min, float max)
		{
			absRange.min = min;
			absRange.max = max;
			range = absRange;
		}

		public FloatRangeUB(float absMin, float absMax, float selMin, float selMax)
		{
			absRange.min = absMin;
			absRange.max = absMax;
			range.min = selMin;
			range.max = selMax; //Tok'ra Kree
		}

		public FloatRangeUB(FloatRange range)
		{
			this.range = this.absRange = range;
		}

		public FloatRangeUB(FloatRange absRange, FloatRange selRange)
		{
			this.absRange = absRange;
			this.range = selRange;
		}

		public static implicit operator FloatRangeUB(FloatRange range) => new(range);

		public static implicit operator FloatRangeUB(IntRangeUB intRange) =>
			new(intRange.absRange.min, intRange.absRange.max, intRange.range.min, intRange.range.max);

		public float min
		{
			get => range.min;
			set => range.min = value;
		}
		public float max
		{
			get => range.max;
			set => range.max = value;
		}

		public bool Includes(float val) =>
			range.Includes(val)
			|| (val < absRange.min && range.min == absRange.min)
			|| (val > absRange.max && range.max == absRange.max);
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace TD_Find_Lib
{
	static class TDWidgets
	{
		public static bool FloatRange(Rect rect, int id, ref FloatRange range, float min = 0f, float max = 1f, string labelKey = null, ToStringStyle valueStyle = ToStringStyle.FloatTwo, float gap = 0f, GameFont sliderLabelFont = GameFont.Tiny, Color? sliderLabelColor = null)
		{
			FloatRange newRange = range;
			Widgets.FloatRange(rect, id, ref newRange, min, max, labelKey, valueStyle, gap, sliderLabelFont, sliderLabelColor);
			if (range != newRange)
			{
				range = newRange;
				return true;
			}
			return false;
		}
	}
}

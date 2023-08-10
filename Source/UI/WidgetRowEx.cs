using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using UnityEngine;

namespace TD_Find_Lib
{
	public static class WidgetRowEx
	{
		public static Rect GetRect(this WidgetRow row, float width, float gap = WidgetRow.DefaultGap)
		{
			Rect result = new(row.LeftX(width), row.curY, width, WidgetRow.IconSize + gap);
			row.IncrementPosition(width);
			return result;
		}

		public static bool Checkbox(this WidgetRow row, ref bool val, float gap = WidgetRow.DefaultGap)
		{
			bool before = val;
			Rect butRect = row.GetRect(WidgetRow.IconSize);
			Widgets.Checkbox(butRect.x, butRect.y, ref val);
			row.IncrementPosition(gap);
			return before != val;
		}

		//Same as buttonText but with no gap.
		public static bool ButtonTextToggleBool(this WidgetRow row, ref bool value, string labelOn, string labelOff, string tooltip = null, bool drawBackground = true, bool doMouseoverSound = true, bool active = true, float? fixedWidth = null)
		{
			if(row.ButtonText(value?labelOn:labelOff, tooltip, drawBackground, doMouseoverSound, active, fixedWidth))
			{
				value = !value;
				return true;
			}
			return false;
		}
	}
}

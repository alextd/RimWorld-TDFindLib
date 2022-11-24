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
			Rect result = new Rect(row.FinalX, row.FinalY, width, WidgetRow.IconSize + gap);
			row.Gap(width);
			return result;
		}

		public static bool CheckboxLabeled(this WidgetRow row, string label, ref bool val, float gap = WidgetRow.DefaultGap)
		{
			bool before = val;
			row.Label(label);
			Rect butRect = row.GetRect(WidgetRow.IconSize);
			Widgets.Checkbox(butRect.x, butRect.y, ref val);
			row.Gap(gap);
			return before != val;
		}

		public static bool ButtonIconColored(this WidgetRow row, Texture2D tex, string tooltip = null, Color? color = null, Color? mouseoverColor = null, Color? backgroundColor = null, Color? mouseoverBackgroundColor = null, bool doMouseoverSound = true, float overrideSize = -1f)
		{
			float num = ((overrideSize > 0f) ? overrideSize : 24f);
			float num2 = (24f - num) / 2f;
			row.IncrementYIfWillExceedMaxWidth(num);
			Rect rect = new Rect(row.LeftX(num) + num2, row.curY + num2, num, num);
			if (doMouseoverSound)
			{
				Verse.Sound.MouseoverSounds.DoRegion(rect);
			}
			if (mouseoverBackgroundColor.HasValue && Mouse.IsOver(rect))
			{
				Widgets.DrawRectFast(rect, mouseoverBackgroundColor.Value);
			}
			else if (backgroundColor.HasValue && !Mouse.IsOver(rect))
			{
				Widgets.DrawRectFast(rect, backgroundColor.Value);
			}
			bool result = Widgets.ButtonImage(rect, tex, color ?? Color.white, mouseoverColor ?? GenUI.MouseoverColor);
			row.IncrementPosition(num);
			if (!tooltip.NullOrEmpty())
			{
				TooltipHandler.TipRegion(rect, tooltip);
			}
			return result;
		}

		//Same as buttonText but with no gap.
		public static bool ButtonTextNoGap(this WidgetRow row, string label, string tooltip = null, bool drawBackground = true, bool doMouseoverSound = true, bool active = true, float? fixedWidth = null)
		{
			bool result = row.ButtonText(label, tooltip, drawBackground, doMouseoverSound, active, fixedWidth);
			row.IncrementPosition(-row.gap);
			return result;
		}
	}
}

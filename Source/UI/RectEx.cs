﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TD_Find_Lib
{
	public static class RectEx
	{
		public static Rect RightHalfClamped(this Rect rect, float leftPushingX)
		{
			float xMin = Mathf.Max(rect.x + rect.width / 2f, leftPushingX);
			return new Rect(xMin, rect.y, rect.xMax - xMin, rect.height);
		}
		public static Rect RightPartClamped(this Rect rect, float pct, float leftPushingX, float desiredWidth = 0)
		{
			float xMin = Mathf.Max(Mathf.Min(rect.xMax - desiredWidth, rect.x * pct + rect.width * (1 - pct)), leftPushingX);
			return new Rect(xMin, rect.y, rect.xMax - xMin, rect.height);
		}
	}
}

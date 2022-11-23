using System;
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
	}
}

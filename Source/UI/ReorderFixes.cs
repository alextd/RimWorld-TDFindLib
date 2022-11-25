using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using UnityEngine;

namespace TD_Find_Lib
{
	public static class ReorderFixes
	{
		public static void ClearAbsRect(int reorderID)
		{
			// Turn off The Multigroup system assuming that if you're closer to group A but in group B's rect, that you want to insert at end of B.
			// That just doesn't apply here.
			// (it uses absRect to check mouseover group B and that overrides if you're in an okay place to drop in group A)
			var group = ReorderableWidget.groups[reorderID];  //immutable struct O_o
			group.absRect = new Rect();
			ReorderableWidget.groups[reorderID] = group;
		}

		public static void FixAbsRect(int reorderID, Rect reorderRect)
		{
			//AbsRect was being set to 0,0 but should use the position here, if you're not in the beginning of a GUI Group:
			var group = ReorderableWidget.groups[reorderID];  //immutable struct O_o
			group.absRect = new Rect(UI.GUIToScreenPoint(reorderRect.position), reorderRect.size);
			ReorderableWidget.groups[reorderID] = group;
		}
	}
}

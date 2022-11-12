using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using UnityEngine;

namespace TD_Find_Lib
{
	public class Dialog_Name : Dialog_Rename
	{
		const float TitleHeight = 35f;

		Action<string> setNameAction;
		string title;

		public Dialog_Name(string name, Action<string> act, string title = null)
		{
			curName = name;
			setNameAction = act;
			this.title = title;
		}

		//protected but using publicized assembly
		//protected override void SetName(string name)
		public override void SetName(string name)
		{
			setNameAction(name);
		}

		public override Vector2 InitialSize => title == null ? base.InitialSize : new Vector2(280f, 175f + TitleHeight);

		public override void DoWindowContents(Rect inRect)
		{
			if (title == null)
			{
				base.DoWindowContents(inRect);
				return;
			}

			Text.Font = GameFont.Medium;
			Widgets.Label(inRect, title);

			inRect.yMin += TitleHeight;

			GUI.BeginGroup(inRect);
			base.DoWindowContents(inRect.AtZero());
			GUI.EndGroup();
		}

	}
}

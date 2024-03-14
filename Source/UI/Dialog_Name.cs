using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;
using UnityEngine;

namespace TD_Find_Lib
{
	// TOOD: Use 1.5 Dialog_Rename where approrpriate
	// Some places were actual renames, 
	// Some where making new objects with a given name.
	public class Dialog_Name : Window
	{
		protected string curName;

		private bool focusedRenameField;

		public int MaxNameLength => 256;

		const float TitleHeight = 35f;

		Action<string> setNameAction;
		Predicate<string> rejector;
		string title;

		public Dialog_Name(string name, Action<string> act, string title = null, Predicate<string> rejector = null)
		{
			forcePause = true;
			doCloseX = true;
			absorbInputAroundWindow = true;
			closeOnAccept = false;
			closeOnClickedOutside = true;

			curName = name;
			setNameAction = act;
			this.title = title;
			this.rejector = rejector;
		}

		public AcceptanceReport NameIsValid(string name)
		{
			if (name.Length == 0)
			{
				return false;
			}

			if (rejector != null && rejector(name))
				return "NameIsInUse".Translate();

			return true;
		}

		public override Vector2 InitialSize => title == null ? base.InitialSize : new Vector2(280f, 175f + TitleHeight);

		public override void DoWindowContents(Rect inRect)
		{
			if (title == null)
			{
				DoWindowContents2(inRect);
				return;
			}

			Text.Font = GameFont.Medium;
			Widgets.Label(inRect, title);

			inRect.yMin += TitleHeight;

			GUI.BeginGroup(inRect);
			DoWindowContents2(inRect.AtZero());
			GUI.EndGroup();
		}

		public  void DoWindowContents2(Rect inRect)
		{
			Text.Font = GameFont.Small;
			bool flag = false;
			if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
			{
				flag = true;
				Event.current.Use();
			}
			GUI.SetNextControlName("RenameField");
			string text = Widgets.TextField(new Rect(0f, 15f, inRect.width, 35f), curName);
			if (text.Length < MaxNameLength)
			{
				curName = text;
			}
			if (!focusedRenameField)
			{
				UI.FocusControl("RenameField", this);
				focusedRenameField = true;
			}
			if (!(Widgets.ButtonText(new Rect(15f, inRect.height - 35f - 15f, inRect.width - 15f - 15f, 35f), "OK") || flag))
			{
				return;
			}
			AcceptanceReport acceptanceReport = NameIsValid(curName);
			if (!acceptanceReport.Accepted)
			{
				if (acceptanceReport.Reason.NullOrEmpty())
				{
					Messages.Message("NameIsInvalid".Translate(), MessageTypeDefOf.RejectInput, historical: false);
				}
				else
				{
					Messages.Message(acceptanceReport.Reason, MessageTypeDefOf.RejectInput, historical: false);
				}
			}
			else
			{
				setNameAction(curName);
				Find.WindowStack.TryRemove(this);
			}
		}

	}
}

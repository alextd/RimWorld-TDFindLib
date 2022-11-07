using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using UnityEngine;
using RimWorld;

namespace TD_Find_Lib
{
	public class TDFindLibListWindow : Window
	{
		public TDFindLibListWindow()
		{
			preventCameraMotion = false;
			draggable = true;
			resizeable = true;
			doCloseX = true;
		}

		private const float RowHeight = WidgetRow.IconSize + 6;

		private Vector2 scrollPosition = Vector2.zero;
		private float scrollViewHeight;
		public override void DoWindowContents(Rect fillRect)
		{
			Listing_StandardIndent listing = new();
			Rect viewRect = new Rect(0f, 0f, fillRect.width - 16f, scrollViewHeight);
			listing.BeginScrollView(fillRect, ref scrollPosition, viewRect);

			// Filter List
			listing.Header("Saved Filters:");

			FindDescription remove = null;
			
			for(int i = 0; i < Mod.settings.savedFilters.Count; i++)
			{
				var desc = Mod.settings.savedFilters[i];
				Rect rowRect = listing.GetRect(RowHeight);

				WidgetRow row = new WidgetRow(rowRect.x, rowRect.y, UIDirection.RightThenDown, rowRect.width);

				// Buttons
				if (row.ButtonIcon(FindTex.Edit))
				{
					int localI = i;
					Find.WindowStack.Add(new TDFindLibEditorWindow(desc.CloneForEdit(), delegate (FindDescription newDesc)
						{
							Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation($"Save changes to {newDesc.name}?",
								delegate ()
								{
									Mod.settings.savedFilters[localI] = newDesc;
									Mod.settings.Write();
								},
								title: "Change Filter"));
						}));
				}

				if (row.ButtonIcon(FindTex.Trash))
					remove = desc;

				if (Current.Game != null &&
					row.ButtonIcon(FindTex.Copy))
					Verse.Log.Error("Todo!");// MainTabWindow_List.OpenWith(desc.CloneForUse(Find.CurrentMap), true);

				// Name
				row.Gap(6);
				Text.Anchor = TextAnchor.LowerLeft;
				row.Label(desc.name + desc.mapLabel);
				Text.Anchor = TextAnchor.UpperLeft;
			}

			if (listing.ButtonImage(TexButton.Plus, WidgetRow.IconSize, WidgetRow.IconSize))
			{
				FindDescription newFD = new();
				Mod.settings.savedFilters.Add(newFD);
			}
			listing.Label("todo : Save load to file.");


			//Active filters from mods
			listing.Header("Active Filters:");
			listing.Label("todo()");
			// Edit how often they tick.


			listing.EndScrollView(ref scrollViewHeight);

			if (remove != null)
			{
				if (Event.current.shift)
					Mod.settings.savedFilters.Remove(remove);
				else
					Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
						"TD.Delete0".Translate(remove.name), () => Mod.settings.savedFilters.Remove(remove)));
			}
		}
	}
}

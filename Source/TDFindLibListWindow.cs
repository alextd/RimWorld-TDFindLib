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
		private FilterListDrawer savedFiltersDrawer;
		private List<(string, FilterListDrawer)> groupDrawers;

		public TDFindLibListWindow()
		{
			savedFiltersDrawer = new FilterListDrawer(Mod.settings.savedFilters);
			
			groupDrawers = new();
			foreach((string name, List<FindDescription> descs) in Mod.settings.groupedFilters)
			{
				groupDrawers.Add((name, new FilterListDrawer(descs)));
			}

			preventCameraMotion = false;
			draggable = true;
			resizeable = true;
			doCloseX = true;
		}

		public override void SetInitialSizeAndPosition()
		{
			base.SetInitialSizeAndPosition();
			windowRect.x = UI.screenWidth - windowRect.width;
		}

		public override void PostClose()
		{
			Mod.settings.Write();
		}


		private Vector2 scrollPosition = Vector2.zero;
		private float scrollViewHeight;

		public override void DoWindowContents(Rect fillRect)
		{
			Listing_StandardIndent listing = new();
			Rect viewRect = new Rect(0f, 0f, fillRect.width - 16f, scrollViewHeight);
			listing.BeginScrollView(fillRect, ref scrollPosition, viewRect);

			// Filter List
			listing.Header("Saved Filters:");


			savedFiltersDrawer.DrawFindDescList(listing);

			foreach((string name, FilterListDrawer drawer) in groupDrawers)
			{
				listing.Header(name +":");
				drawer.DrawFindDescList(listing);
			}


			//Active filters from mods
			listing.Header("Active Filters:");
			listing.Label("todo()");
			// Edit how often they tick.


			listing.EndScrollView(ref scrollViewHeight);
		}
	}

	public class FilterListDrawer
	{
		List<FindDescription> descs;

		public FilterListDrawer(List<FindDescription> descs)
		{
			this.descs = descs;
		}


		private const float RowHeight = WidgetRow.IconSize + 6;

		private int reorderID;
		private float reorderRectHeight;
		public void DrawFindDescList(Listing_StandardIndent listing)
		{
			if (Event.current.type == EventType.Repaint)
			{
				reorderID = ReorderableWidget.NewGroup(
					(int from, int to) => Reorder(descs, from, to),
					ReorderableDirection.Vertical,
					new Rect(0f, listing.CurHeight, listing.ColumnWidth, reorderRectHeight), 1f,
					extraDraggedItemOnGUI: (int index, Vector2 dragStartPos) =>
						DrawMouseAttachedFindDesc(descs.ElementAt(index), listing.ColumnWidth));
			}

			float startHeight = listing.CurHeight;
			for (int i = 0; i < descs.Count; i++)
			{
				var desc = descs[i];
				Rect rowRect = listing.GetRect(RowHeight);

				WidgetRow row = new WidgetRow(rowRect.x, rowRect.y, UIDirection.RightThenDown, rowRect.width);

				// Buttons
				if (row.ButtonIcon(FindTex.Edit))
				{
					int localI = i;
					Find.WindowStack.Add(new TDFindLibEditorWindow(desc.CloneForEdit(), delegate (FindDescription newDesc)
					{
						Action acceptAction = delegate ()
						{
							descs[localI] = newDesc;
							Mod.settings.Write();
						};
						Action copyAction = delegate ()
						{
							descs.Insert(localI + 1, newDesc);
							Mod.settings.Write();
						};
						Find.WindowStack.Add(new Dialog_MessageBox(
							$"Save changes to {newDesc.name}?",
							"Confirm".Translate(), acceptAction,
							"No".Translate(), null,
							"Change Filter",
							true, acceptAction,
							delegate () { }// I dunno who wrote this class but this empty method is required so the window can close with esc because its logic is very different from its base class
						)
						{
							buttonCText = "Save as Copy",
							buttonCAction = copyAction,
						});
					}));
				}

				if (row.ButtonIcon(FindTex.Trash))
				{
					if (Event.current.shift)
						descs.Remove(desc);
					else
						Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
							"TD.Delete0".Translate(desc.name), () => descs.Remove(desc)));
				}

				if (Current.Game != null &&
					row.ButtonIcon(FindTex.Copy))
					Verse.Log.Error("Todo!");// MainTabWindow_List.OpenWith(desc.CloneForUse(Find.CurrentMap), true);

				// Name
				row.Gap(6);
				Text.Anchor = TextAnchor.LowerLeft;
				row.Label(desc.name + desc.mapLabel);
				Text.Anchor = TextAnchor.UpperLeft;

				ReorderableWidget.Reorderable(reorderID, rowRect);
			}
			reorderRectHeight = listing.CurHeight - startHeight;

			if (listing.ButtonImage(TexButton.Plus, WidgetRow.IconSize, WidgetRow.IconSize))
			{
				FindDescription newFD = new();
				descs.Add(newFD);
			}
			listing.Label("todo : Save load to file.");
		}

		public static void Reorder(List<FindDescription> descs, int from, int to)
		{
			var desc = descs[from];
			descs.RemoveAt(from);
			descs.Insert(from < to ? to - 1 : to, desc);
		}


		public static void DrawMouseAttachedFindDesc(FindDescription desc, float width)
		{
			Vector2 mousePositionOffset = Event.current.mousePosition + Vector2.one * 12;
			Rect dragRect = new Rect(mousePositionOffset, new(width, Text.LineHeight));

			//Same id 34003428 as GenUI.DrawMouseAttachment
			Find.WindowStack.ImmediateWindow(34003428, dragRect, WindowLayer.Super,
				() => Widgets.Label(new Rect(0, 0, width, Text.LineHeight), desc.name),
				doBackground: false, absorbInputAroundWindow: false, 0f);
		}
	}
}

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
		private FilterGroupDrawer savedFiltersDrawer;
		private List<FilterGroupDrawer> groupDrawers;
		private RefreshFilterGroupDrawer refreshDrawer;

		public TDFindLibListWindow()
		{
			savedFiltersDrawer = new FilterGroupDrawer(Mod.settings.savedFilters);
			
			groupDrawers = new();
			foreach(FilterGroup group in Mod.settings.groupedFilters)
			{
				groupDrawers.Add(new FilterGroupDrawer(group));
			}

			refreshDrawer = new RefreshFilterGroupDrawer();

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

			foreach(FilterGroupDrawer drawer in groupDrawers)
			{
				listing.Header(drawer.group.name +":");
				drawer.DrawFindDescList(listing);
			}
			if(listing.ButtonText("New Group"))
			{
				Find.WindowStack.Add(new Dialog_Name("Group Name", n =>
				{
					var group = new FilterGroup() { name = n };
					Mod.settings.groupedFilters.Add(group);
					groupDrawers.Add(new FilterGroupDrawer(group));
				}));
			}

			
			//Active filters from mods
			if (Current.Game != null)
			{
				listing.Header("Active Filters:");

				refreshDrawer.DrawFindDescList(listing);
			}


			listing.EndScrollView(ref scrollViewHeight);
		}
	}

	abstract public class FilterListDrawer
	{
		public abstract FindDescription DescAt(int i);
		public abstract int Count { get; }
		public abstract void Reorder(int from, int to);

		public virtual void DoWidgetButtons(WidgetRow row, FindDescription desc, int i) { }
		public virtual void DoRectExtra(Rect rowRect, FindDescription desc, int i) { }
		public virtual void PostListDraw(Listing_StandardIndent listing) { }

		//Drawing
		private const float RowHeight = WidgetRow.IconSize + 6;

		private int reorderID;
		private float reorderRectHeight;
		public void DrawFindDescList(Listing_StandardIndent listing)
		{
			if (Event.current.type == EventType.Repaint)
			{
				reorderID = ReorderableWidget.NewGroup(
					Reorder,
					ReorderableDirection.Vertical,
					new Rect(0f, listing.CurHeight, listing.ColumnWidth, reorderRectHeight), 1f,
					extraDraggedItemOnGUI: (int index, Vector2 dragStartPos) =>
						DrawMouseAttachedFindDesc(DescAt(index), listing.ColumnWidth));
			}

			Text.Anchor = TextAnchor.LowerLeft;
			float startHeight = listing.CurHeight;
			for (int i = 0; i < Count; i++)
			{
				FindDescription desc = DescAt(i);
				Rect rowRect = listing.GetRect(RowHeight);

				WidgetRow row = new WidgetRow(rowRect.x, rowRect.y, UIDirection.RightThenDown, rowRect.width);

				// Buttons
				DoWidgetButtons(row, desc, i);

				// Name
				Text.Anchor = TextAnchor.UpperLeft;
				row.Gap(6);
				row.Label(desc.name + desc.mapLabel);

				Text.Anchor = TextAnchor.LowerRight;
				DoRectExtra(rowRect, desc, i);

				ReorderableWidget.Reorderable(reorderID, rowRect);
			}
			reorderRectHeight = listing.CurHeight - startHeight;
			Text.Anchor = TextAnchor.UpperLeft;

			PostListDraw(listing);
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
	public class FilterGroupDrawer : FilterListDrawer
	{
		public FilterGroup group;

		public FilterGroupDrawer(FilterGroup group)
		{
			this.group = group;
		}

		public override FindDescription DescAt(int i) => group[i];
		public override int Count => group.Count;

		public override void Reorder(int from, int to)
		{
			var desc = group[from];
			group.RemoveAt(from);
			group.Insert(from < to ? to - 1 : to, desc);
		}

		public override void DoWidgetButtons(WidgetRow row, FindDescription desc, int i)
		{
			if (row.ButtonIcon(FindTex.Edit))
			{
				Find.WindowStack.Add(new TDFindLibEditorWindow(desc.CloneForEdit(), delegate (FindDescription newDesc)
				{
					Action acceptAction = delegate ()
					{
						group[i] = newDesc;
						Mod.settings.Write();
					};
					Action copyAction = delegate ()
					{
						group.Insert(i + 1, newDesc);
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
					group.Remove(desc);
				else
					Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
						"TD.Delete0".Translate(desc.name), () => group.Remove(desc)));
			}

			if (Current.Game != null &&
				row.ButtonIcon(FindTex.Copy))
				Verse.Log.Error("Todo!");// MainTabWindow_List.OpenWith(desc.CloneForUse(Find.CurrentMap), true);
		}

		public override void PostListDraw(Listing_StandardIndent listing)
		{
			if (listing.ButtonImage(TexButton.Plus, WidgetRow.IconSize, WidgetRow.IconSize))
				group.Add(new FindDescription());
		}

	}

	public class RefreshFilterGroupDrawer : FilterListDrawer
	{
		public List<RefreshFindDesc> refDesc;

		public RefreshFilterGroupDrawer(List<RefreshFindDesc> refDesc = null)
		{
			this.refDesc = refDesc ?? Current.Game.GetComponent<TDFindLibGameComp>().findDescRefreshers;
		}

		public override FindDescription DescAt(int i) => refDesc[i].desc;
		public override int Count => refDesc.Count;

		public override void Reorder(int from, int to)
		{
			var desc = refDesc[from];
			refDesc.RemoveAt(from);
			refDesc.Insert(from < to ? to - 1 : to, desc);
		}

		public override void DoWidgetButtons(WidgetRow row, FindDescription desc, int i)
		{
			if (row.ButtonIcon(FindTex.Edit))
			{
				Find.WindowStack.Add(new TDFindLibViewerWindow(desc));
			}

			if (refDesc[i].permanent)
			{
				row.Gap(WidgetRow.IconSize);
			}
			else
			{
				if (row.ButtonIcon(FindTex.Trash))
				{
					if (Event.current.shift)
						refDesc.RemoveAt(i);
					else
						Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
							"TD.StopRefresh0".Translate(desc.name), () => refDesc.RemoveAt(i)));
				}
			}
		}

		public override void DoRectExtra(Rect rowRect, FindDescription desc, int i)
		{
			Widgets.Label(rowRect, $"Every {refDesc[i].period} ticks");
		}
	}
}

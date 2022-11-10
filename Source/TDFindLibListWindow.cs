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
				groupDrawers.Add(new FilterGroupDrawer(group));

			if (Current.Game != null)
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
			savedFiltersDrawer.DrawFindDescList(listing);

			// Filter groups by name
			foreach(FilterGroupDrawer drawer in groupDrawers)
				drawer.DrawFindDescList(listing);


			// Add new group
			listing.Gap(4);
			Text.Font = GameFont.Medium;
			Rect newGroupRect = listing.GetRect(Text.LineHeight);
			WidgetRow newGroupRow = new WidgetRow(newGroupRect.x, newGroupRect.y);
			if (newGroupRow.ButtonIcon(TexButton.Plus))
			{
				Find.WindowStack.Add(new Dialog_Name("Group Name", n =>
				{
					var group = new FilterGroup(n);
					Mod.settings.groupedFilters.Add(group);
					groupDrawers.Add(new FilterGroupDrawer(group));
				}));
			}
			newGroupRow.Gap(4);
			newGroupRow.Label("New Group", height: Text.LineHeight);
			listing.Gap(4);
			Text.Font = GameFont.Small;

			// Active filters, possibly from mods
			if (refreshDrawer?.Count > 0)
			{
				listing.GapLine();
				refreshDrawer?.DrawFindDescList(listing);
			}


			listing.EndScrollView(ref scrollViewHeight);
		}
	}

	abstract public class FilterListDrawer
	{
		public abstract string Name { get; }
		public abstract FindDescription DescAt(int i);
		public abstract int Count { get; }

		public virtual bool CanEdit => true;
		public virtual void Add(FindDescription desc) { }
		public virtual void Reorder(int from, int to) { }

		public virtual void PreRowDraw(Listing_StandardIndent listing, int i) { }
		public virtual void DoWidgetButtons(WidgetRow row, FindDescription desc, int i) { }
		public virtual void DoRectExtra(Rect rowRect, FindDescription desc, int i) { }
		public virtual void PostListDraw(Listing_StandardIndent listing) { }

		public void PopUpCreateFindDesc()
		{
			Find.WindowStack.Add(new Dialog_Name("New Filter", n =>
			{
				var desc = new FindDescription() { name = n };
				Add(desc);
				Find.WindowStack.Add(new TDFindLibEditorWindow(desc));
			}));
		}

		//Drawing
		private const float RowHeight = WidgetRow.IconSize + 6;

		private int reorderID;
		private float reorderRectHeight;
		public void DrawFindDescList(Listing_StandardIndent listing)
		{
			// Name Header
			Text.Font = GameFont.Medium;
			Rect headerRect = listing.GetRect(Text.LineHeight);
			WidgetRow headerRow = new WidgetRow(headerRect.x, headerRect.y);
			headerRow.Label(Name + ":", height: Text.LineHeight);
			Text.Font = GameFont.Small;

			if (CanEdit)
			{
				// Add new filter button
				headerRow.Gap(4);
				if (headerRow.ButtonIcon(TexButton.Plus))
					PopUpCreateFindDesc();
				listing.Gap(4);

				// Reorder rect
				if (Event.current.type == EventType.Repaint)
				{
					reorderID = ReorderableWidget.NewGroup(
						Reorder,
						ReorderableDirection.Vertical,
						new Rect(0f, listing.CurHeight, listing.ColumnWidth, reorderRectHeight), 1f,
						extraDraggedItemOnGUI: (int index, Vector2 dragStartPos) =>
							DrawMouseAttachedFindDesc(DescAt(index), listing.ColumnWidth));
				}
			}

			float startHeight = listing.CurHeight;
			for (int i = 0; i < Count; i++)
			{
				PreRowDraw(listing, i);
				FindDescription desc = DescAt(i);
				Rect rowRect = listing.GetRect(RowHeight);

				WidgetRow row = new WidgetRow(rowRect.x, rowRect.y, UIDirection.RightThenDown, rowRect.width);

				// Buttons
				DoWidgetButtons(row, desc, i);

				// Name
				row.Gap(6);
				row.Label(desc.name + desc.mapLabel);

				DoRectExtra(rowRect, desc, i);

				if(CanEdit)
					ReorderableWidget.Reorderable(reorderID, rowRect);
			}
			reorderRectHeight = listing.CurHeight - startHeight;

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

		public override string Name => group.name;
		public override FindDescription DescAt(int i) => group[i];
		public override int Count => group.Count;

		public override void Add(FindDescription desc)
		{
			group.Add(desc);
		}
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
	}

	public class RefreshFilterGroupDrawer : FilterListDrawer
	{
		public List<RefreshFindDesc> refDesc;

		public RefreshFilterGroupDrawer(List<RefreshFindDesc> refDesc = null)
		{
			this.refDesc = refDesc ?? Current.Game.GetComponent<TDFindLibGameComp>().findDescRefreshers;
		}
		public override string Name => "Active Filters";
		public override FindDescription DescAt(int i) => refDesc[i].desc;
		public override int Count => refDesc.Count;
		
		public override bool CanEdit => false;

		private string currentTag;
		public override void PreRowDraw(Listing_StandardIndent listing, int i)
		{
			if(refDesc[i].tag != currentTag)
			{
				currentTag = refDesc[i].tag;
				listing.Label(currentTag);
			}
		}
		public override void PostListDraw(Listing_StandardIndent listing)
		{
			currentTag = null;
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
			Text.Anchor = TextAnchor.UpperRight;
			Widgets.Label(rowRect, $"Every {refDesc[i].period} ticks");
			Text.Anchor = TextAnchor.UpperLeft;
		}
	}
}

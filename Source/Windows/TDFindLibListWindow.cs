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
		private IFilterStorageParent parent;
		private List<FilterGroupDrawer> groupDrawers;
		private RefreshFilterGroupDrawer refreshDrawer;

		public TDFindLibListWindow(IFilterStorageParent parent)
		{
			this.parent = parent;
			groupDrawers = new();
			foreach (FilterGroup group in parent.Children)
			{
				groupDrawers.Add(new FilterGroupDrawer(group, groupDrawers));
			}

			if (Current.Game != null)
				refreshDrawer = new RefreshFilterGroupDrawer(Current.Game.GetComponent<TDFindLibGameComp>().findDescRefreshers);

			preventCameraMotion = false;
			draggable = true;
			resizeable = true;
			closeOnAccept = false;
			//closeOnCancel = false;
			doCloseX = true;
		}

		public override void PostClose()
		{
			parent.Write();
		}


		private Vector2 scrollPosition = Vector2.zero;
		private float scrollViewHeight;

		public override void DoWindowContents(Rect fillRect)
		{
			Text.Font = GameFont.Medium;
			Text.Anchor = TextAnchor.UpperCenter;
			Rect headerRect = fillRect.TopPartPixels(Text.LineHeight).AtZero();
			Widgets.Label(headerRect, "TD Find Lib: Filter Library");
			Text.Anchor = default;

			fillRect.yMin = headerRect.yMax;

			Listing_StandardIndent listing = new();
			Rect viewRect = new Rect(0f, 0f, fillRect.width - 16f, scrollViewHeight);
			listing.BeginScrollView(fillRect, ref scrollPosition, viewRect);

			// Filter groups by name
			for (int i = 0; i < groupDrawers.Count; i++)
			{
				groupDrawers[i].DrawFindDescList(listing);
				listing.Gap();
			}


			// Add new group
			listing.Gap(4);
			Text.Font = GameFont.Medium;
			Rect newGroupRect = listing.GetRect(Text.LineHeight);
			WidgetRow newGroupRow = new WidgetRow(newGroupRect.x, newGroupRect.y);


			//Add button
			if (newGroupRow.ButtonIcon(FindTex.GreyPlus))
			{
				Find.WindowStack.Add(new Dialog_Name("New Group", n =>
				{
					var group = new FilterGroup(n, parent);
					parent.Add(group);

					var drawer = new FilterGroupDrawer(group, groupDrawers);
					groupDrawers.Add(drawer);

					drawer.PopUpCreateFindDesc();
				},
				"Name for New Group",
				n => parent.Children.Any(f => f.name == n)));
			}


			// Import button
			FilterStorageUtil.ButtonChooseImportFilterGroup(newGroupRow, group =>
			{
				parent.Add(group);


				var drawer = new FilterGroupDrawer(group, groupDrawers);
				if (groupDrawers.Any(d => d.Name == group.name))
					drawer.PopUpRename();

				groupDrawers.Add(drawer);
			},
			"Storage");


			//Label
			newGroupRow.Gap(4);
			newGroupRow.Label("Add New Group", height: Text.LineHeight);
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

	abstract public class FilterListDrawer<TList, TItem> where TList : IList<TItem>
	{
		public TList list;

		public FilterListDrawer(TList list)
		{
			this.list = list;
		}
		public abstract string Name { get; }
		public abstract FindDescription DescAt(int i);
		public abstract int Count { get; }

		public virtual void Reorder(int from, int to)
		{
			var desc = list[from];
			list.RemoveAt(from);
			list.Insert(from < to ? to - 1 : to, desc);
		}

		public virtual void DrawExtraHeader(Rect headerRect) { }
		public virtual void DrawPreRow(Listing_StandardIndent listing, int i) { }
		public virtual void DrawWidgetButtons(WidgetRow row, TItem item, int i) { }
		public virtual void DrawExtraRowRect(Rect rowRect, TItem item, int i) { }
		public virtual void DrawPostList(Listing_StandardIndent listing) { }
		//Drawing
		private const float RowHeight = WidgetRow.IconSize + 6;

		private int reorderID;
		private float reorderRectHeight;
		public void DrawFindDescList(Listing_StandardIndent listing)
		{
			// Name Header
			Text.Font = GameFont.Medium;
			Rect headerRect = listing.GetRect(Text.LineHeight);
			Widgets.Label(headerRect, Name + ":");
			Text.Font = GameFont.Small;

			DrawExtraHeader(headerRect);


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


			// List of FindDescs
			float startHeight = listing.CurHeight;
			for (int i = 0; i < Count; i++)
			{
				DrawPreRow(listing, i);
				TItem item = list[i];
				FindDescription desc = DescAt(i);
				Rect rowRect = listing.GetRect(RowHeight);

				WidgetRow row = new WidgetRow(rowRect.x, rowRect.y, UIDirection.RightThenDown, rowRect.width);

				// Buttons
				DrawWidgetButtons(row, item, i);

				// Name
				row.Gap(6);
				row.Label(desc.name + desc.mapLabel);

				DrawExtraRowRect(rowRect, item, i);

				ReorderableWidget.Reorderable(reorderID, rowRect);
			}
			reorderRectHeight = listing.CurHeight - startHeight;

			DrawPostList(listing);
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

	public class FilterGroupDrawer : FilterListDrawer<FilterGroup, FindDescription>
	{
		public List<FilterGroupDrawer> siblings;
		public FilterGroupDrawer(FilterGroup l, List<FilterGroupDrawer> siblings) : base(l)
		{
			this.siblings = siblings;
		}

		public override string Name => list.name;
		public override FindDescription DescAt(int i) => list[i];
		public override int Count => list.Count;


		public void Trash()
		{
			siblings.Remove(this);
			list.parent.Children.Remove(list);
		}

		public void PopUpCreateFindDesc()
		{
			Find.WindowStack.Add(new Dialog_Name("New Search", n =>
			{
				var desc = new FindDescription() { name = n };
				list.TryAdd(desc);
				Find.WindowStack.Add(new TDFindLibEditorWindow(desc));
			},
			"Name for New Search"));
		}

		public void PopUpRename()
		{
			Find.WindowStack.Add(new Dialog_Name(Name, name => list.name = name, rejector: name => list.parent.Children.Any(g => g.name == name)));
		}



		public override void DrawExtraHeader(Rect headerRect)
		{
			WidgetRow headerRow = new WidgetRow(headerRect.xMax, headerRect.y, UIDirection.LeftThenDown);

			// Delete Group button
			if (headerRow.ButtonIcon(FindTex.Trash))
			{
				if (Event.current.shift)
					Trash();
				else
					Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
						"TD.Delete0".Translate(Name), Trash));
			}

			// Export Group
			FilterStorageUtil.ButtonChooseExportFilterGroup(headerRow, list, "Storage");


			// Import single filter
			FilterStorageUtil.ButtonChooseImportFilter(headerRow, list.Add, "Storage");


			// Paste Group and merge
			FilterStorageUtil.ButtonChooseImportFilterGroup(headerRow, list.AddRange, "Storage");


			// Rename 
			if (headerRow.ButtonIcon(TexButton.Rename))
				PopUpRename();

			// Add new filter button
			if (headerRow.ButtonIcon(FindTex.GreyPlus))
				PopUpCreateFindDesc();
		}

		public override void DrawWidgetButtons(WidgetRow row, FindDescription desc, int i)
		{
			if (row.ButtonIcon(FindTex.Edit, "Edit this filter"))
			{
				Find.WindowStack.Add(new TDFindLibEditorWindow(desc.CloneForEdit(), nd => list.ConfirmPaste(nd, i)));
			}


			if (row.ButtonIcon(TexButton.Rename))
			{
				Find.WindowStack.Add(new Dialog_Name(desc.name, newName => desc.name = newName, rejector: newName => list.Any(fd => fd.name == newName)));
			}

			if (row.ButtonIcon(FindTex.Trash))
			{
				if (Event.current.shift)
					list.Remove(desc);
				else
					Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
						"TD.Delete0".Translate(desc.name), () => list.Remove(desc)));
			}

			FilterStorageUtil.ButtonChooseExportFilter(row, desc, "Storage");
		}
	}

	public class RefreshFilterGroupDrawer : FilterListDrawer<List<RefreshFindDesc>, RefreshFindDesc>
	{
		public RefreshFilterGroupDrawer(List<RefreshFindDesc> l) : base(l) { }

		public override string Name => "Active Filters";
		public override FindDescription DescAt(int i) => list[i].desc;
		public override int Count => list.Count;


		private string currentTag;
		public override void DrawPreRow(Listing_StandardIndent listing, int i)
		{
			if(list[i].tag != currentTag)
			{
				currentTag = list[i].tag;
				listing.Label(currentTag);
			}
		}
		public override void DrawPostList(Listing_StandardIndent listing)
		{
			currentTag = null;
		}


		public override void DrawWidgetButtons(WidgetRow row, RefreshFindDesc refDesc, int i)
		{
			if (row.ButtonIcon(FindTex.Edit, "View this filter"))
			{
				Find.WindowStack.Add(new TDFindLibViewerWindow(refDesc.desc));
			}

			if (row.ButtonIcon(TexButton.AutoHomeArea, "Open the mod controlling this filter"))
			{
				refDesc.OpenUI(refDesc.desc);
			}

			if (list[i].permanent)
			{
				row.Gap(WidgetRow.IconSize);
			}
			else
			{
				if (row.ButtonIcon(FindTex.Trash, "Stop this filter from running (I trust you know what you're doing)"))
				{
					if (Event.current.shift)
						list.RemoveAt(i);
					else
						Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
							"TD.StopRefresh0".Translate(refDesc.desc.name), () => list.RemoveAt(i)));
				}
			}
		}

		public override void DrawExtraRowRect(Rect rowRect, RefreshFindDesc refDesc, int i)
		{
			Rect textRect = rowRect.RightPart(.3f);
			Text.Anchor = TextAnchor.UpperRight;
			Widgets.Label(textRect, $"Every {refDesc.period} ticks");
			if (Widgets.ButtonInvisible(textRect))
			{
				Find.WindowStack.Add(new Dialog_Name($"{refDesc.period}", s =>
				{
					if (int.TryParse(s, out int n))
						refDesc.period = n;
				}
				, "Set refresh period in ticks"));
			}
			Text.Anchor = TextAnchor.UpperLeft;
		}
	}
}

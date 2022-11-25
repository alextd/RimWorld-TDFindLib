using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using UnityEngine;
using RimWorld;

namespace TD_Find_Lib
{
	public class GroupLibraryWindow : Window
	{
		private ISearchStorageParent parent;
		private List<SearchGroupDrawer> groupDrawers = new();
		private RefreshSearchGroupDrawer refreshDrawer;

		public GroupLibraryWindow(ISearchStorageParent parent)
		{
			this.parent = parent;

			SetupDrawers();

			if (Current.Game != null)
				refreshDrawer = new RefreshSearchGroupDrawer(Current.Game.GetComponent<TDFindLibGameComp>().searchRefreshers);

			preventCameraMotion = false;
			draggable = true;
			resizeable = true;
			closeOnAccept = false;
			//closeOnCancel = false;
			doCloseX = true;
		}

		private void SetupDrawers()
		{
			groupDrawers.Clear();
			foreach (SearchGroup group in parent.Children)
				groupDrawers.Add(new SearchGroupDrawer(group, groupDrawers));
		}

		public override void PostClose()
		{
			parent.Write();
		}


		public void Reorder(int from, int to)
		{
			parent.ReorderGroup(from, to);
			SetupDrawers();
		}

		public void MasterReorderSearch(int from, int fromGroupID, int to, int toGroupID)
		{
			Log.Message($"TDFindLibListWindow.MasterReorderSearch(int from={from}, int fromGroup={fromGroupID}, int to={to}, int toGroup={toGroupID})");
			SearchGroup fromGroup = groupDrawers.First(dr => dr.reorderID == fromGroupID).list;
			SearchGroup toGroup = groupDrawers.First(dr => dr.reorderID == toGroupID).list;
			var search = fromGroup[from];
			fromGroup.RemoveAt(from);
			toGroup.Insert(to, search);
		}


		private Vector2 scrollPosition = Vector2.zero;
		private float scrollViewHeight;
		private int reorderID;
		private float reorderRectHeight;

		public override void DoWindowContents(Rect fillRect)
		{
			Text.Font = GameFont.Medium;
			Text.Anchor = TextAnchor.UpperCenter;
			Rect titleRect = fillRect.TopPartPixels(Text.LineHeight).AtZero();
			Widgets.Label(titleRect, "TD Find Lib: Search Library");
			Text.Anchor = default;

			fillRect.yMin = titleRect.yMax;

			Listing_StandardIndent listing = new();
			Rect viewRect = new Rect(0f, 0f, fillRect.width - 16f, scrollViewHeight);
			listing.BeginScrollView(fillRect, ref scrollPosition, viewRect);



			// Reorder group rect
			if (Event.current.type == EventType.Repaint)
			{
				reorderID = ReorderableWidget.NewGroup(
					Reorder,
					ReorderableDirection.Vertical,
					new Rect(0f, listing.CurHeight, listing.ColumnWidth, reorderRectHeight), 1f,
					extraDraggedItemOnGUI: (int index, Vector2 dragStartPos) =>
						DrawMouseAttachedSearchGroup(parent.Children[index], listing.ColumnWidth));

				// Turn off The Multigroup system assuming that if you're closer to group A but in group B's rect, that you want to insert at end of B.
				// That just doesn't apply here.
				// (it uses absRect to check mouseover group B and that overrides if you're in an okay place to drop in group A)
				var group = ReorderableWidget.groups[reorderID];  //immutable struct O_o
				group.absRect = new Rect();
				ReorderableWidget.groups[reorderID] = group;
			}

			// Draw each Search group
			for (int i = 0; i < groupDrawers.Count; i++)
			{
				Rect headerRect = groupDrawers[i].DrawHeader(listing);
				ReorderableWidget.Reorderable(reorderID, headerRect);
				reorderRectHeight = listing.CurHeight; // - startHeight; but the start is 0

				groupDrawers[i].DrawQuerySearchList(listing);
				listing.Gap();
			}

			List<int> reorderIDs = new(groupDrawers.Select(d => d.reorderID));

			ReorderableWidget.NewMultiGroup(reorderIDs, MasterReorderSearch);


			// Add new group

			listing.Gap(4);
			Rect newGroupRect = listing.GetRect(Text.LineHeight);
			if (!ReorderableWidget.Dragging)
			{
				WidgetRow newGroupRow = new WidgetRow(newGroupRect.x, newGroupRect.y);
				Text.Font = GameFont.Medium;


				//Add button
				if (newGroupRow.ButtonIcon(FindTex.GreyPlus))
				{
					Find.WindowStack.Add(new Dialog_Name("New Group", n =>
					{
						var group = new SearchGroup(n, parent);
						parent.Add(group);

						var drawer = new SearchGroupDrawer(group, groupDrawers);
						groupDrawers.Add(drawer);

						drawer.PopUpCreateQuerySearch();
					},
					"Name for New Group",
					n => parent.Children.Any(f => f.name == n)));
				}


				// Import button
				SearchStorage.ButtonChooseImportSearchGroup(newGroupRow, group =>
				{
					parent.Add(group);

					var drawer = new SearchGroupDrawer(group, groupDrawers);
					if (groupDrawers.Any(d => d.Name == group.name))
						drawer.PopUpRename();
					else
						parent.Write();

					groupDrawers.Add(drawer);
				},
				"Storage");


				//Label
				newGroupRow.Gap(4);
				newGroupRow.Label("Add New Group", height: Text.LineHeight);
				Text.Font = GameFont.Small;
			}


			// Active searches, possibly from mods
			if (refreshDrawer?.Count > 0)
			{
				listing.Gap(4);

				listing.GapLine();
				refreshDrawer?.DrawQuerySearchList(listing);
			}

			listing.EndScrollView(ref scrollViewHeight);
		}


		public static void DrawMouseAttachedSearchGroup(SearchGroup group, float width)
		{
			Vector2 mousePositionOffset = Event.current.mousePosition + Vector2.one * 12;
			Rect dragRect = new Rect(mousePositionOffset, new(width, Text.LineHeight));

			//Same id 34003428 as GenUI.DrawMouseAttachment
			Find.WindowStack.ImmediateWindow(34003428, dragRect, WindowLayer.Super,
				() => Widgets.Label(new Rect(0, 0, width, Text.LineHeight), group.name),
				doBackground: false, absorbInputAroundWindow: false, 0f);
		}
	}

	abstract public class SearchListDrawer<TList, TItem> where TList : IList<TItem>
	{
		public TList list;

		public SearchListDrawer(TList list)
		{
			this.list = list;
		}
		public abstract string Name { get; }
		public abstract QuerySearch SearchAt(int i);
		public abstract int Count { get; }

		public virtual void ReorderSearch(int from, int to)
		{
			var search = list[from];
			list.RemoveAt(from);
			list.Insert(from < to ? to - 1 : to, search);
		}

		public virtual void DrawExtraHeader(Rect headerRect) { }
		public virtual void DrawPreRow(Listing_StandardIndent listing, int i) { }
		public virtual void DrawWidgetButtons(WidgetRow row, TItem item, int i) { }
		public virtual void DrawExtraRowRect(Rect rowRect, TItem item, int i) { }
		public virtual void DrawPostList(Listing_StandardIndent listing) { }
		//Drawing
		private const float RowHeight = WidgetRow.IconSize + 6;

		public int reorderID;
		private float reorderRectHeight;

		public Rect DrawHeader(Listing_StandardIndent listing)
		{
			// Name Header
			Text.Font = GameFont.Medium;
			Rect headerRect = listing.GetRect(Text.LineHeight);
			Widgets.Label(headerRect, Name + ":");
			Text.Font = GameFont.Small;

			DrawExtraHeader(headerRect);

			return headerRect;
		}

		public void DrawQuerySearchList(Listing_StandardIndent listing)
		{
			// Reorder rect
			if (Event.current.type == EventType.Repaint)
			{
				reorderID = ReorderableWidget.NewGroup(
					ReorderSearch,
					ReorderableDirection.Vertical,
					new Rect(0f, listing.CurHeight, listing.ColumnWidth, reorderRectHeight), 1f,
					extraDraggedItemOnGUI: (int index, Vector2 dragStartPos) =>
						DrawMouseAttachedQuerySearch(SearchAt(index), listing.ColumnWidth));

				// Turn off The Multigroup system assuming that if you're closer to group A but in group B's rect, that you want to insert at end of B.
				// That just doesn't apply here.
				// (it uses absRect to check mouseover group B and that overrides if you're in an okay place to drop in group A)
				var group = ReorderableWidget.groups[reorderID];  //immutable struct O_o
				group.absRect = new Rect();
				ReorderableWidget.groups[reorderID] = group;
			}


			// List of QuerySearches
			float startHeight = listing.CurHeight;
			for (int i = 0; i < Count; i++)
			{
				DrawPreRow(listing, i);
				TItem item = list[i];
				QuerySearch search = SearchAt(i);
				Rect rowRect = listing.GetRect(RowHeight);

				WidgetRow row = new WidgetRow(rowRect.x, rowRect.y, UIDirection.RightThenDown, rowRect.width);

				// Buttons
				DrawWidgetButtons(row, item, i);

				// Name
				row.Gap(6);
				row.Label(search.name + search.GetMapNameSuffix());

				DrawExtraRowRect(rowRect, item, i);

				ReorderableWidget.Reorderable(reorderID, rowRect);
			}
			reorderRectHeight = listing.CurHeight - startHeight;

			DrawPostList(listing);
		}


		public static void DrawMouseAttachedQuerySearch(QuerySearch search, float width)
		{
			Vector2 mousePositionOffset = Event.current.mousePosition + Vector2.one * 12;
			Rect dragRect = new Rect(mousePositionOffset, new(width, Text.LineHeight));

			//Same id 34003428 as GenUI.DrawMouseAttachment
			Find.WindowStack.ImmediateWindow(34003428, dragRect, WindowLayer.Super,
				() => Widgets.Label(new Rect(0, 0, width, Text.LineHeight), search.name),
				doBackground: false, absorbInputAroundWindow: false, 0f);
		}
	}

	public class SearchGroupDrawer : SearchListDrawer<SearchGroup, QuerySearch>
	{
		public List<SearchGroupDrawer> siblings;
		public SearchGroupDrawer(SearchGroup l, List<SearchGroupDrawer> siblings) : base(l)
		{
			this.siblings = siblings;
		}

		public override string Name => list.name;
		public override QuerySearch SearchAt(int i) => list[i];
		public override int Count => list.Count;


		public void TrashThis()
		{
			siblings.Remove(this);
			list.parent.Children.Remove(list);
			list.parent.Write();
		}

		public void Trash(int i)
		{
			list.RemoveAt(i);
			list.parent.Write();
		}

		public void PopUpCreateQuerySearch()
		{
			Find.WindowStack.Add(new Dialog_Name("New Search", n =>
			{
				var search = new QuerySearch() { name = n };
				list.TryAdd(search);
				Find.WindowStack.Add(new SearchEditorWindow(search, f => list.parent.Write())) ;
			},
			"Name for New Search"));
		}

		public void PopUpRename()
		{
			Find.WindowStack.Add(new Dialog_Name(Name, name => { list.name = name; list.parent.Write(); }, rejector: name => list.parent.Children.Any(g => g.name == name)));
		}



		public override void DrawExtraHeader(Rect headerRect)
		{
			WidgetRow headerRow = new WidgetRow(headerRect.xMax, headerRect.y, UIDirection.LeftThenDown);

			// Delete Group button
			if (headerRow.ButtonIcon(FindTex.Trash))
			{
				if (Event.current.shift)
					TrashThis();
				else
					Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
						"TD.Delete0".Translate(Name), TrashThis, true));
			}

			// Export Group
			SearchStorage.ButtonChooseExportSearchGroup(headerRow, list, "Storage");


			// Import single search
			SearchStorage.ButtonChooseImportSearch(headerRow, list.Add, "Storage");


			// Paste Group and merge
			SearchStorage.ButtonChooseImportSearchGroup(headerRow, list.AddRange, "Storage");


			// Rename 
			if (headerRow.ButtonIcon(TexButton.Rename))
				PopUpRename();

			// Add new search button
			if (headerRow.ButtonIcon(FindTex.GreyPlus))
				PopUpCreateQuerySearch();
		}

		public override void DrawWidgetButtons(WidgetRow row, QuerySearch search, int i)
		{
			if (row.ButtonIcon(FindTex.Edit, "Edit this search"))
			{
				Find.WindowStack.Add(new SearchEditorWindow(search.CloneInactive(), nd => list.ConfirmPaste(nd, i)));
			}


			if (row.ButtonIcon(TexButton.Rename))
			{
				Find.WindowStack.Add(new Dialog_Name(search.name, newName => search.name = newName, rejector: newName => list.Any(fd => fd.name == newName)));
			}

			if (row.ButtonIcon(FindTex.Trash))
			{
				if (Event.current.shift)
					Trash(i);
				else
					Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
						"TD.Delete0".Translate(search.name), () => Trash(i), true));
			}

			SearchStorage.ButtonChooseExportSearch(row, search, "Storage");
		}
	}

	public class RefreshSearchGroupDrawer : SearchListDrawer<List<RefreshQuerySearch>, RefreshQuerySearch>
	{
		public RefreshSearchGroupDrawer(List<RefreshQuerySearch> l) : base(l) { }

		public override string Name => "Active Searches";
		public override QuerySearch SearchAt(int i) => list[i].search;
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


		public override void DrawWidgetButtons(WidgetRow row, RefreshQuerySearch refSearch, int i)
		{
			if (row.ButtonIcon(FindTex.Edit, "View this search"))
			{
				Find.WindowStack.Add(new TDFindLibViewerWindow(refSearch.search));
			}

			if (row.ButtonIcon(TexButton.AutoHomeArea, "Open the mod controlling this search"))
			{
				refSearch.OpenUI(refSearch.search);
			}

			if (list[i].permanent)
			{
				row.Gap(WidgetRow.IconSize);
			}
			else
			{
				if (row.ButtonIcon(FindTex.Trash, "Stop this search from running (I trust you know what you're doing)"))
				{
					if (Event.current.shift)
						list.RemoveAt(i);
					else
						Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
							"TD.StopRefresh0".Translate(refSearch.search.name), () => list.RemoveAt(i)));
				}
			}
		}

		public override void DrawExtraRowRect(Rect rowRect, RefreshQuerySearch refSearch, int i)
		{
			Rect textRect = rowRect.RightPart(.3f);
			Text.Anchor = TextAnchor.UpperRight;
			Widgets.Label(textRect, $"Every {refSearch.period} ticks");
			if (Widgets.ButtonInvisible(textRect))
			{
				Find.WindowStack.Add(new Dialog_Name($"{refSearch.period}", s =>
				{
					if (int.TryParse(s, out int n))
						refSearch.period = n;
				}
				, "Set refresh period in ticks"));
			}
			Text.Anchor = default;
		}
	}
}

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

		public void SetupDrawers()
		{
			groupDrawers.Clear();
			foreach (SearchGroup group in parent.Children)
				groupDrawers.Add(new SearchGroupDrawer(group, groupDrawers));
		}

		public override void PostClose()
		{
			parent.NotifyChanged();
		}


		public void DoReorderGroup(int from, int to)
		{
			parent.ReorderGroup(from, to);
			SetupDrawers();
		}

		public void MasterReorderSearch(int from, int fromGroupID, int to, int toGroupID)
		{
			Log.Message($"GroupLibraryWindow.MasterReorderSearch(int from={from}, int fromGroup={fromGroupID}, int to={to}, int toGroup={toGroupID})");
			SearchGroup fromGroup = groupDrawers.First(dr => dr.reorderID == fromGroupID).list;
			SearchGroup toGroup = groupDrawers.First(dr => dr.reorderID == toGroupID).list;
			var search = fromGroup[from];
			if (Event.current.control)
			{
				var newSearch = search.CloneInactive();
				newSearch.name += "TD.CopyNameSuffix".Translate();
				toGroup.Insert(to, newSearch);
			}
			else
			{
				fromGroup.RemoveAt(from);
				toGroup.Insert(to, search);
			}
		}


		private Vector2 scrollPosition = Vector2.zero;
		private float scrollViewHeight;
		private int reorderID;
		private float reorderRectHeight;
		const float GapBetweenGroups = 4;

		public override void DoWindowContents(Rect fillRect)
		{
			Text.Font = GameFont.Medium;
			Text.Anchor = TextAnchor.UpperCenter;
			Rect titleRect = fillRect.TopPartPixels(Text.LineHeight).AtZero();
			Widgets.Label(titleRect, "TD.TDFindLibSearchLibrary".Translate());
			Text.Anchor = default;

			fillRect.yMin = titleRect.yMax;

			Listing_StandardIndent listing = new();
			Rect viewRect = new(0f, 0f, fillRect.width - 16f, scrollViewHeight);
			listing.BeginScrollView(fillRect, ref scrollPosition, viewRect);



			// Reorder group rect
			if (Event.current.type == EventType.Repaint)
			{
				Rect reorderRect = new(0f, listing.CurHeight, listing.ColumnWidth, reorderRectHeight);
				reorderID = ReorderableWidget.NewGroup(
					DoReorderGroup,
					ReorderableDirection.Vertical,
					reorderRect, 1f,
					extraDraggedItemOnGUI: (int index, Vector2 dragStartPos) =>
						DrawMouseAttachedSearchGroup(parent.Children[index], listing.ColumnWidth));
			}

			// Draw each Search group
			for (int i = 0; i < groupDrawers.Count; i++)
			{
				var drawer = groupDrawers[i];
				Rect headerRect = drawer.DrawHeader(listing);
				ReorderableWidget.Reorderable(reorderID, headerRect);
				reorderRectHeight = listing.CurHeight; // - startHeight; but the start is 0

				drawer.DrawQuerySearchList(listing);
				listing.Gap();
			}

			List<int> reorderIDs = new(groupDrawers.Select(d => d.reorderID));

			ReorderableWidget.NewMultiGroup(reorderIDs, MasterReorderSearch);


			// Add new group

			listing.Gap(GapBetweenGroups);
			Rect newGroupRect = listing.GetRect(Text.LineHeight);
			if (!ReorderableWidget.Dragging)
			{
				WidgetRow newGroupRow = new(newGroupRect.x, newGroupRect.y);
				Text.Font = GameFont.Medium;


				//Add button
				if (newGroupRow.ButtonIcon(FindTex.GreyPlus))
				{
					Find.WindowStack.Add(new Dialog_Name("TD.NewGroup".Translate(), n =>
					{
						var group = new SearchGroup(n, parent);
						parent.Add(group, false);

						var drawer = new SearchGroupDrawer(group, groupDrawers);
						groupDrawers.Add(drawer);

						drawer.PopUpCreateQuerySearch();
					},
					"TD.NameForNewGroup".Translate(),
					n => parent.Children.Any(f => f.name == n)));
				}


				// Import button
				SearchStorage.ButtonChooseImportSearchGroup(newGroupRow, group =>
				{
					parent.Add(group, false);

					var drawer = new SearchGroupDrawer(group, groupDrawers);
					if (groupDrawers.Any(d => d.Name == group.name))
						drawer.PopUpRename();
					else
						parent.NotifyChanged();

					groupDrawers.Add(drawer);
				},
				Settings.StorageTransferTag);


				//Label
				newGroupRow.Gap(GapBetweenGroups);
				newGroupRow.Label("TD.AddNewGroup".Translate(), height: Text.LineHeight);
				Text.Font = GameFont.Small;
			}


			// Active searches, possibly from mods
			if (refreshDrawer?.Count > 0)
			{
				listing.Gap(GapBetweenGroups);

				listing.GapLine();
				refreshDrawer?.DrawQuerySearchList(listing);
			}

			listing.EndScrollView(ref scrollViewHeight);
			scrollViewHeight += GapBetweenGroups;
		}


		public static void DrawMouseAttachedSearchGroup(SearchGroup group, float width)
		{
			Vector2 mousePositionOffset = Event.current.mousePosition + Vector2.one * 12;
			Rect dragRect = new(mousePositionOffset, new(width, Text.LineHeight));

			//Same id 34003428 as GenUI.DrawMouseAttachment
			Find.WindowStack.ImmediateWindow(34003428, dragRect, WindowLayer.Super,
				() => Widgets.Label(new Rect(0, 0, width, Text.LineHeight), group.name),
				doBackground: false, absorbInputAroundWindow: false, 0f);
		}
	}

	abstract public class SearchGroupDrawerBase<TList, TItem> where TList : SearchGroupBase<TItem> where TItem : IQuerySearch
	{
		public TList list;

		public SearchGroupDrawerBase(TList list)
		{
			this.list = list;
		}
		public abstract string Name { get; }
		public int Count => list.Count;

		public virtual void DoReorderSearch(int from, int to)
		{
			var search = list[from];
			list.RemoveAt(from);
			list.Insert(from < to ? to - 1 : to, search);
		}

		public virtual void DrawExtraHeader(Rect headerRect) { }
		public virtual void DrawPreRow(Listing_StandardIndent listing, int i) { }
		public virtual void DrawRowButtons(WidgetRow row, TItem item, int i) { }
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
			float startHeight = listing.CurHeight;

			// Reorder Search rect
			if (Event.current.type == EventType.Repaint)
			{
				Rect reorderRect = new(0f, startHeight, listing.ColumnWidth, reorderRectHeight);
				reorderID = ReorderableWidget.NewGroup(
					DoReorderSearch,
					ReorderableDirection.Vertical,
					reorderRect, 1f,
					extraDraggedItemOnGUI: (int index, Vector2 dragStartPos) =>
						DrawMouseAttachedQuerySearch(list[index].Search, listing.ColumnWidth));
			}


			// List of QuerySearches
			for (int i = 0; i < Count; i++)
			{
				DrawPreRow(listing, i);
				TItem item = list[i];
				QuerySearch search = item.Search;
				Rect rowRect = listing.GetRect(RowHeight);

				WidgetRow row = new(rowRect.x, rowRect.y, UIDirection.RightThenDown, rowRect.width);

				// Buttons
				DrawRowButtons(row, item, i);

				// Name
				row.Gap(6);
				row.Label(search.name + search.GetMapNameSuffix());

				DrawExtraRowRect(rowRect, item, i);

				ReorderableWidget.Reorderable(reorderID, rowRect);
			}
			if (Count == 0 && ReorderableWidget.Dragging)
			{
				Rect rowRect = listing.GetRect(RowHeight);
				Widgets.DrawBox(rowRect);
			}
			reorderRectHeight = listing.CurHeight - startHeight;

			DrawPostList(listing);
		}


		public static void DrawMouseAttachedQuerySearch(QuerySearch search, float width)
		{
			Vector2 mousePositionOffset = Event.current.mousePosition + Vector2.one * 12;
			Rect dragRect = new(mousePositionOffset, new(width, Text.LineHeight));

			//Same id 34003428 as GenUI.DrawMouseAttachment
			Find.WindowStack.ImmediateWindow(34003428, dragRect, WindowLayer.Super,
				() => Widgets.Label(new Rect(0, 0, width, Text.LineHeight), search.name),
				doBackground: false, absorbInputAroundWindow: false, 0f);
		}
	}

	public class SearchGroupDrawer : SearchGroupDrawerBase<SearchGroup, QuerySearch>
	{
		public List<SearchGroupDrawer> siblings;
		public SearchGroupDrawer(SearchGroup l, List<SearchGroupDrawer> siblings) : base(l)
		{
			this.siblings = siblings;
		}

		public override string Name => list.name;


		public override void DoReorderSearch(int from, int to)
		{
			if (Event.current.control)
			{
				QuerySearch newSearch = list[from].CloneInactive();
				newSearch.name += "TD.CopyNameSuffix".Translate();
				list.Insert(to, newSearch);
			}
			else
				base.DoReorderSearch(from, to);
		}

		public void TrashThis()
		{
			siblings.Remove(this);
			list.parent.Children.Remove(list);
			list.parent.NotifyChanged();
		}

		public void Trash(int i)
		{
			list.RemoveAt(i);
			list.parent.NotifyChanged();
		}

		public void PopUpCreateQuerySearch()
		{
			Find.WindowStack.Add(new Dialog_Name("TD.NewSearch".Translate(), n =>
			{
				var search = new QuerySearch() { name = n };
				list.TryAdd(search);
				Find.WindowStack.Add(new SearchEditorWindow(search, Settings.StorageTransferTag, f => list.parent.NotifyChanged()));
			},
			"TD.NameForNewSearch".Translate(),
			name => list.Any(s => s.name == name)));
		}

		public void PopUpRename()
		{
			Find.WindowStack.Add(new Dialog_Name(Name, name => { list.name = name; list.parent.NotifyChanged(); }, rejector: name => list.parent.Children.Any(g => g.name == name)));
		}



		public override void DrawExtraHeader(Rect headerRect)
		{
			WidgetRow headerRow = new(headerRect.xMax, headerRect.y, UIDirection.LeftThenDown);

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
			SearchStorage.ButtonChooseExportSearchGroup(headerRow, list, Settings.StorageTransferTag);


			// Import single search
			SearchStorage.ButtonChooseImportSearch(headerRow, list.Add, Settings.StorageTransferTag);


			// Paste Group and merge
			SearchStorage.ButtonChooseImportSearchGroup(headerRow, list.AddRange, Settings.StorageTransferTag);


			// Rename 
			if (headerRow.ButtonIcon(TexButton.Rename))
				PopUpRename();

			// Add new search button
			if (headerRow.ButtonIcon(FindTex.GreyPlus))
				PopUpCreateQuerySearch();
		}

		public override void DrawRowButtons(WidgetRow row, QuerySearch search, int i)
		{
			if (row.ButtonIcon(FindTex.Edit, "TD.EditThisSearch".Translate()))
			{
				Find.WindowStack.Add(new SearchEditorWindow(search.CloneInactive(), Settings.StorageTransferTag, nd => list.ConfirmPaste(nd, i)));
			}


			if (row.ButtonIcon(TexButton.Rename))
			{
				Find.WindowStack.Add(new Dialog_Name(search.name, newName => search.name = newName, rejector: newName => list.Any(s => s.name == newName)));
			}

			if (row.ButtonIcon(FindTex.Trash))
			{
				if (Event.current.shift)
					Trash(i);
				else
					Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
						"TD.Delete0".Translate(search.name), () => Trash(i), true));
			}

			SearchStorage.ButtonChooseExportSearch(row, search, Settings.StorageTransferTag);
		}
	}

	public class RefreshSearchGroupDrawer : SearchGroupDrawerBase<RefreshGroup, RefreshQuerySearch>
	{
		public RefreshSearchGroupDrawer(RefreshGroup l) : base(l) { }

		public override string Name => "TD.ActiveSearches".Translate();


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


		public override void DrawRowButtons(WidgetRow row, RefreshQuerySearch refSearch, int i)
		{
			if (row.ButtonIcon(FindTex.Edit, "TD.ViewThisSearch".Translate()))
			{
				Find.WindowStack.Add(new SearchViewerWindow(refSearch.search, Settings.StorageTransferTag));
			}

			if (row.ButtonIcon(TexButton.AutoHomeArea, "TD.OpenTheModControllingThisSearch".Translate()))
			{
				refSearch.OpenUI(refSearch.search);
			}

			if (list[i].permanent)
			{
				row.Gap(WidgetRow.IconSize);
			}
			else
			{
				if (row.ButtonIcon(FindTex.Trash, "TD.StopThisSearchFromRunningITrustYouKnowWhatYoureDoing".Translate()))
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
			Widgets.Label(textRect, "TD.Every0Ticks".Translate(refSearch.period));
			if (Widgets.ButtonInvisible(textRect))
			{
				Find.WindowStack.Add(new Dialog_Name($"{refSearch.period}", s =>
				{
					if (int.TryParse(s, out int n))
						refSearch.period = n;
				}
				, "TD.SetRefreshPeriodInTicks".Translate()));
			}
			Text.Anchor = default;
		}
	}
}

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
		private List<FilterGroupDrawer> groupDrawers;
		private RefreshFilterGroupDrawer refreshDrawer;

		public TDFindLibListWindow()
		{
			groupDrawers = new();
			foreach (FilterGroup group in Mod.settings.groupedFilters)
				groupDrawers.Add(new FilterGroupDrawer(group));

			if (Current.Game != null)
				refreshDrawer = new RefreshFilterGroupDrawer(Current.Game.GetComponent<TDFindLibGameComp>().findDescRefreshers);

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

			// Filter groups by name
			foreach (FilterGroupDrawer drawer in groupDrawers)
			{
				drawer.DrawFindDescList(listing, () =>
				{
					groupDrawers.Remove(drawer);
					Mod.settings.groupedFilters.Remove(drawer.list);
				});
				listing.Gap();
			}


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
					var drawer = new FilterGroupDrawer(group);
					groupDrawers.Add(drawer);
					drawer.PopUpCreateFindDesc();
				}));
			}
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

	abstract public class FilterListDrawer<T>
	{
		public T list;

		public FilterListDrawer(T list)
		{
			this.list = list;
		}
		public abstract string Name { get; }
		public virtual void Rename(string name) { }
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
			Find.WindowStack.Add(new Dialog_Name("Search Name", n =>
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
		public void DrawFindDescList(Listing_StandardIndent listing, Action onTrash = null)
		{
			// Name Header
			Text.Font = GameFont.Medium;
			Rect headerRect = listing.GetRect(Text.LineHeight);
			WidgetRow headerRow = new WidgetRow(headerRect.x, headerRect.y);
			headerRow.Label(Name + ":", height: Text.LineHeight);
			Text.Font = GameFont.Small;

			if (CanEdit)
			{
				headerRow.Gap(4);

				// Add new filter button
				if (headerRow.ButtonIcon(TexButton.Rename))
					Find.WindowStack.Add(new Dialog_Name(Name, Rename));


				// Add new filter button
				if (headerRow.ButtonIcon(TexButton.Plus))
					PopUpCreateFindDesc();


				// Delete Group button
				if (headerRow.ButtonIcon(FindTex.Trash))
				{
					if (Event.current.shift)
						onTrash?.Invoke();
					else
						Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
							"TD.Delete0".Translate(Name), () => onTrash?.Invoke()));
				}



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


			// List of FindDescs
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

	public class FilterGroupDrawer : FilterListDrawer<FilterGroup>
	{
		public FilterGroupDrawer(FilterGroup l) : base(l) { }

		public override string Name => list.name;
		public override void Rename(string name) => list.name = name;
		public override FindDescription DescAt(int i) => list[i];
		public override int Count => list.Count;

		public override void Add(FindDescription desc)
		{
			list.Add(desc);
		}
		public override void Reorder(int from, int to)
		{
			var desc = list[from];
			list.RemoveAt(from);
			list.Insert(from < to ? to - 1 : to, desc);
		}

		public override void DoWidgetButtons(WidgetRow row, FindDescription desc, int i)
		{
			if (row.ButtonIcon(FindTex.Edit))
			{
				Find.WindowStack.Add(new TDFindLibEditorWindow(desc.CloneForEdit(), delegate (FindDescription newDesc)
				{
					Action acceptAction = delegate ()
					{
						list[i] = newDesc;
						Mod.settings.Write();
					};
					Action copyAction = delegate ()
					{
						list.Insert(i + 1, newDesc);
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

			if (row.ButtonIcon(TexButton.Rename))
			{
				Find.WindowStack.Add(new Dialog_Name(desc.name, newName => desc.name = newName ));
			}

			if (row.ButtonIcon(FindTex.Trash))
			{
				if (Event.current.shift)
					list.Remove(desc);
				else
					Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
						"TD.Delete0".Translate(desc.name), () => list.Remove(desc)));
			}

			if (Current.Game != null &&
				row.ButtonIcon(FindTex.Copy))
				Verse.Log.Error("Todo!");// MainTabWindow_List.OpenWith(desc.CloneForUse(Find.CurrentMap), true);
		}
	}

	public class RefreshFilterGroupDrawer : FilterListDrawer<List<RefreshFindDesc>>
	{
		public RefreshFilterGroupDrawer(List<RefreshFindDesc> l) : base(l) { }

		public override string Name => "Active Filters";
		public override FindDescription DescAt(int i) => list[i].desc;
		public override int Count => list.Count;
		
		public override bool CanEdit => false;

		private string currentTag;
		public override void PreRowDraw(Listing_StandardIndent listing, int i)
		{
			if(list[i].tag != currentTag)
			{
				currentTag = list[i].tag;
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

			if (list[i].permanent)
			{
				row.Gap(WidgetRow.IconSize);
			}
			else
			{
				if (row.ButtonIcon(FindTex.Trash))
				{
					if (Event.current.shift)
						list.RemoveAt(i);
					else
						Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
							"TD.StopRefresh0".Translate(desc.name), () => list.RemoveAt(i)));
				}
			}
		}

		public override void DoRectExtra(Rect rowRect, FindDescription desc, int i)
		{
			Text.Anchor = TextAnchor.UpperRight;
			Widgets.Label(rowRect, $"Every {list[i].period} ticks");
			Text.Anchor = TextAnchor.UpperLeft;
		}
	}
}

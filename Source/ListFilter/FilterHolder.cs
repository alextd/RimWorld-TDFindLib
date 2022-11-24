using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using RimWorld;
using UnityEngine;

namespace TD_Find_Lib
{
	public interface IFilterHolder
	{
		public FilterHolder Children { get; }
		public FindDescription RootFindDesc { get; }
	}
	public class FilterHolder//	 : IExposable //Not IExposable because that means ctor FilterHolder() should exist.
	{
		private IFilterHolder parent;
		public List<ListFilter> filters = new List<ListFilter>() { };

		public FilterHolder(IFilterHolder p)
		{
			parent = p;
		}

		public void ExposeData()
		{
			Scribe_Collections.Look(ref filters, "filters");
			if(Scribe.mode == LoadSaveMode.LoadingVars)
				foreach (var f in filters)
					f.parent = parent;
		}

		public FilterHolder Clone(IFilterHolder newParent)
		{
			FilterHolder clone = new FilterHolder(newParent);
			foreach (var f in filters)
				clone.Add(f.Clone(), remake: false);
			return clone;
		}

		// Add filter and set its parent to this (well, the same parent IFilterHolder of this)
		public void Add(ListFilter newFilter, int index = -1, bool remake = true, bool focus = false)
		{
			newFilter.parent = parent;
			if(index == -1)
				filters.Add(newFilter);
			else
				filters.Insert(index, newFilter);

			if (focus) newFilter.Focus();
			if (remake) parent.RootFindDesc?.RemakeList();
		}

		public void Clear()
		{
			filters.Clear();
		}

		public void RemoveAll(HashSet<ListFilter> removedFilters)
		{
			filters.RemoveAll(f => removedFilters.Contains(f));
		}

		public bool Any(Predicate<ListFilter> predicate)
		{
			if (parent is ListFilter f)
				if (predicate(f))
					return true;

			foreach (var filter in filters)
			{
				if (filter is IFilterHolder childHolder)
				{
					if (childHolder.Children.Any(predicate)) //handles calling on itself
						return true;
				}
				else if (predicate(filter))
					return true;
			}

			return false;
		}

		public void Reorder(int from, int to, bool remake = true)
		{
			var draggerFilter = filters[from];
			filters.RemoveAt(from);
			Add(draggerFilter, from < to ? to - 1 : to, remake);
		}

		//Gather method that passes in both FindDescription and all ListFilters to selector
		public IEnumerable<T> Gather<T>(Func<IFilterHolder, T?> selector) where T : struct
		{
			if (selector(parent) is T result)
				yield return result;

			foreach (var filter in filters)
				if (filter is IFilterHolder childHolder)
					foreach (T r in childHolder.Children.Gather(selector))
						yield return r;
		}
		//sadly 100% copied from above, subtract the "?" oh gee.
		public IEnumerable<T> Gather<T>(Func<IFilterHolder, T> selector) where T : class
		{
			if (selector(parent) is T result)
				yield return result;

			foreach (var filter in filters)
				if (filter is IFilterHolder childHolder)
					foreach (T r in childHolder.Children.Gather(selector))
						yield return r;
		}

		//Gather method that passes in both FindDescription and all ListFilters to selector
		public void ForEach(Action<IFilterHolder> action)
		{
			action(parent);

			foreach (var filter in filters)
				if (filter is IFilterHolder childHolder)
					childHolder.Children.ForEach(action); //handles calling on itself
		}

		//Gather method that passes in both FindDescription and all ListFilters to selector
		public void ForEach(Action<ListFilter> action)
		{
			if(parent is ListFilter f)
				action(f);
			foreach (var filter in filters)
			{
				if (filter is IFilterHolder childHolder)
					childHolder.Children.ForEach(action); //handles calling on itself
				else //just a filter then
					action(filter);
			}
		}

		public void MasterReorder(int from, int fromGroup, int to, int toGroup)
		{
			Log.Message($"FilterHolder.MasterReorder(int from={from}, int fromGroup={fromGroup}, int to={to}, int toGroup={toGroup})");

			ListFilter draggedFilter = Gather(delegate (IFilterHolder holder)
			{
				if (holder.Children.reorderID == fromGroup)
					return holder.Children.filters.ElementAt(from);

				return null;
			}).First();

			IFilterHolder newHolder = null;
			ForEach(delegate (IFilterHolder holder)
			{
				if (holder.Children.reorderID == toGroup)
					// Hold up, don't drop inside yourself
					if (draggedFilter != holder)
						newHolder = holder;	//todo: abort early?
			});

			if (newHolder != null)
			{
				draggedFilter.parent.Children.filters.Remove(draggedFilter);
				newHolder.Children.Add(draggedFilter, to);
			}
		}

		//Draw filters completely, in a rect
		public bool DrawFiltersInRect(Rect listRect, bool locked, ref Vector2 scrollPositionFilt, ref float scrollHeight)
		{
			Listing_StandardIndent listing = new Listing_StandardIndent()
				{ maxOneColumn = true };

			float viewWidth = listRect.width;
			if (scrollHeight > listRect.height)
				viewWidth -= 16f;
			Rect viewRect = new Rect(0f, 0f, viewWidth, scrollHeight);

			listing.BeginScrollView(listRect, ref scrollPositionFilt, viewRect);

			bool changed = DrawFiltersListing(listing, locked);

			List<int> reorderIDs = new(Gather<int>(f => f.Children.reorderID));

			ReorderableWidget.NewMultiGroup(reorderIDs, parent.RootFindDesc.Children.MasterReorder);

			listing.EndScrollView(ref scrollHeight);

			return changed;
		}


		// draw filters continuing a Listing_StandardIndent
		public int reorderID;
		private float reorderRectHeight;

		public bool DrawFiltersListing(Listing_StandardIndent listing, bool locked)
		{
			Rect coveredRect = new Rect(0f, listing.CurHeight, listing.ColumnWidth, reorderRectHeight);
			if (Event.current.type == EventType.Repaint)
			{
				reorderID = ReorderableWidget.NewGroup(
					(int from, int to) => Reorder(from, to, true),
					ReorderableDirection.Vertical,
					coveredRect, 1f,
					extraDraggedItemOnGUI: (int index, Vector2 dragStartPos) =>
						DrawMouseAttachedFilter(filters[index], coveredRect.width - 100));

				// Turn off The Multigroup system assuming that if you're closer to group A but in group B's rect, that you want to insert at end of B.
				// That just doesn't apply here.
				// (it uses absRect to check mouseover group B and that overrides if you're in an okay place to drop in group A)
				var group = ReorderableWidget.groups[reorderID];	//immutable struct O_o
				group.absRect = new Rect();
				ReorderableWidget.groups[reorderID] = group;
			}

			bool changed = false;
			HashSet<ListFilter> removedFilters = new();
			foreach (ListFilter filter in filters)
			{
				Rect usedRect = listing.GetRect(0);

				(bool ch, bool d) = filter.Listing(listing, locked);
				changed |= ch;
				if (d)
					removedFilters.Add(filter);

				//Reorder box with only one line tall ;
				//TODO: make its yMax = filter.CurHeight,
				//but then you can't drag AWAY from subfilters,
				//though it's correct where you drag TO
				usedRect.height = Text.LineHeight;
				ReorderableWidget.Reorderable(reorderID, usedRect);

				// Highlight the filters that pass for selected objects (useful for "any" filters)
				if (!(filter is IFilterHolder) && Find.UIRoot is UIRoot_Play && Find.Selector.SelectedObjects.Any(o => o is Thing t && filter.AppliesTo(t)))
				{
					usedRect.yMax = listing.CurHeight;
					Widgets.DrawHighlight(usedRect);
				}
			}

			reorderRectHeight = listing.CurHeight - coveredRect.y;

			RemoveAll(removedFilters);

			if (!locked)
				DrawAddRow(listing);

			return changed;
		}

		public static void DrawMouseAttachedFilter(ListFilter dragFilter, float width)
		{
			Vector2 mousePositionOffset = Event.current.mousePosition + Vector2.one * 12;
			Rect dragRect = new Rect(mousePositionOffset, new(width, Text.LineHeight));

			//Same id 34003428 as GenUI.DrawMouseAttachment
			Find.WindowStack.ImmediateWindow(34003428, dragRect, WindowLayer.Super,
				() => dragFilter.DrawMain(dragRect.AtZero(), true),
				doBackground: false, absorbInputAroundWindow: false, 0f);
		}

		public void DrawAddRow(Listing_StandardIndent listing)
		{
			Rect addRow = listing.GetRect(Text.LineHeight);
			listing.Gap(listing.verticalSpacing);

			if (ReorderableWidget.Dragging)
				return;

			Rect butRect = addRow; butRect.width = Text.LineHeight;
			Widgets.DrawTextureFitted(butRect, TexButton.Plus, 1.0f);

			Rect textRect = addRow; textRect.xMin += Text.LineHeight + WidgetRow.DefaultGap;
			Widgets.Label(textRect, "TD.AddNewFilter...".Translate());

			Widgets.DrawHighlightIfMouseover(addRow);

			if (Widgets.ButtonInvisible(addRow))
			{
				DoFloatAllFilters();
			}
		}

		public void DoFloatAllFilters()
		{
			List<FloatMenuOption> options = new List<FloatMenuOption>();
			foreach (ListFilterSelectableDef def in ListFilterMaker.SelectableList)
			{
				if (def is ListFilterDef fDef)
					options.Add(new FloatMenuOption(
						fDef.LabelCap,
						() => Add(ListFilterMaker.MakeFilter(fDef), focus: true)
					));
				if (def is ListFilterCategoryDef cDef)
					options.Add(new FloatMenuOption(
						"+ " + cDef.LabelCap,
						() => DoFloatAllCategory(cDef)
					));
			}
			Find.WindowStack.Add(new FloatMenu(options));
		}

		public void DoFloatAllCategory(ListFilterCategoryDef cDef)
		{
			List<FloatMenuOption> options = new List<FloatMenuOption>();
			foreach (ListFilterDef def in cDef.SubFilters)
			{
				// I don't think we need to worry about double-nested filters
				options.Add(new FloatMenuOption(
					def.LabelCap,
					() => Add(ListFilterMaker.MakeFilter(def), focus: true)
				));
			}
			Find.WindowStack.Add(new FloatMenu(options));
		}
	}
}

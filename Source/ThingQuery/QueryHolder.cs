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
	public interface IQueryHolder
	{
		public QueryHolder Children { get; }
		public QuerySearch RootQuerySearch { get; }
	}
	public class QueryHolder//	 : IExposable //Not IExposable because that means ctor QueryHolder() should exist.
	{
		private IQueryHolder parent;
		public List<ThingQuery> queries = new List<ThingQuery>() { };

		public QueryHolder(IQueryHolder p)
		{
			parent = p;
		}

		public void ExposeData()
		{
			Scribe_Collections.Look(ref queries, "queries");
			if(Scribe.mode == LoadSaveMode.LoadingVars)
				foreach (var f in queries)
					f.parent = parent;
		}

		public QueryHolder Clone(IQueryHolder newParent)
		{
			QueryHolder clone = new QueryHolder(newParent);
			foreach (var f in queries)
				clone.Add(f.Clone(), remake: false);
			return clone;
		}

		// Add query and set its parent to this (well, the same parent IQueryHolder of this)
		public void Add(ThingQuery newQuery, int index = -1, bool remake = true, bool focus = false)
		{
			newQuery.parent = parent;
			if(index == -1)
				queries.Add(newQuery);
			else
				queries.Insert(index, newQuery);

			if (focus) newQuery.Focus();
			if (remake) parent.RootQuerySearch?.RemakeList();
		}

		public void Clear()
		{
			queries.Clear();
		}

		public void RemoveAll(HashSet<ThingQuery> removedQueries)
		{
			queries.RemoveAll(f => removedQueries.Contains(f));
		}

		public bool Any(Predicate<ThingQuery> predicate)
		{
			if (parent is ThingQuery f)
				if (predicate(f))
					return true;

			foreach (var query in queries)
			{
				if (query is IQueryHolder childHolder)
				{
					if (childHolder.Children.Any(predicate)) //handles calling on itself
						return true;
				}
				else if (predicate(query))
					return true;
			}

			return false;
		}

		public void Reorder(int from, int to, bool remake = true)
		{
			var draggerQuery = queries[from];
			queries.RemoveAt(from);
			Add(draggerQuery, from < to ? to - 1 : to, remake);
		}

		//Gather method that passes in both QuerySearch and all ThingQuerys to selector
		public IEnumerable<T> Gather<T>(Func<IQueryHolder, T?> selector) where T : struct
		{
			if (selector(parent) is T result)
				yield return result;

			foreach (var query in queries)
				if (query is IQueryHolder childHolder)
					foreach (T r in childHolder.Children.Gather(selector))
						yield return r;
		}
		//sadly 100% copied from above, subtract the "?" oh gee.
		public IEnumerable<T> Gather<T>(Func<IQueryHolder, T> selector) where T : class
		{
			if (selector(parent) is T result)
				yield return result;

			foreach (var query in queries)
				if (query is IQueryHolder childHolder)
					foreach (T r in childHolder.Children.Gather(selector))
						yield return r;
		}

		//Gather method that passes in both QuerySearch and all ThingQuerys to selector
		public void ForEach(Action<IQueryHolder> action)
		{
			action(parent);

			foreach (var query in queries)
				if (query is IQueryHolder childHolder)
					childHolder.Children.ForEach(action); //handles calling on itself
		}

		//Gather method that passes in both QuerySearch and all ThingQuerys to selector
		public void ForEach(Action<ThingQuery> action)
		{
			if(parent is ThingQuery f)
				action(f);
			foreach (var query in queries)
			{
				if (query is IQueryHolder childHolder)
					childHolder.Children.ForEach(action); //handles calling on itself
				else //just a query then
					action(query);
			}
		}

		public void MasterReorder(int from, int fromGroup, int to, int toGroup)
		{
			Log.Message($"QueryHolder.MasterReorder(int from={from}, int fromGroup={fromGroup}, int to={to}, int toGroup={toGroup})");

			ThingQuery draggedQuery = Gather(delegate (IQueryHolder holder)
			{
				if (holder.Children.reorderID == fromGroup)
					return holder.Children.queries.ElementAt(from);

				return null;
			}).First();

			IQueryHolder newHolder = null;
			ForEach(delegate (IQueryHolder holder)
			{
				if (holder.Children.reorderID == toGroup)
					// Hold up, don't drop inside yourself
					if (draggedQuery != holder)
						newHolder = holder;	//todo: abort early?
			});

			if (newHolder != null)
			{
				draggedQuery.parent.Children.queries.Remove(draggedQuery);
				newHolder.Children.Add(draggedQuery, to);
			}
		}

		//Draw queries completely, in a rect
		public bool DrawQueriesInRect(Rect listRect, bool locked, ref Vector2 scrollPositionFilt, ref float scrollHeight)
		{
			Listing_StandardIndent listing = new Listing_StandardIndent()
				{ maxOneColumn = true };

			float viewWidth = listRect.width;
			if (scrollHeight > listRect.height)
				viewWidth -= 16f;
			Rect viewRect = new Rect(0f, 0f, viewWidth, scrollHeight);

			listing.BeginScrollView(listRect, ref scrollPositionFilt, viewRect);

			bool changed = DrawQueriesListing(listing, locked);

			List<int> reorderIDs = new(Gather<int>(f => f.Children.reorderID));

			ReorderableWidget.NewMultiGroup(reorderIDs, parent.RootQuerySearch.Children.MasterReorder);

			listing.EndScrollView(ref scrollHeight);

			return changed;
		}


		// draw queries continuing a Listing_StandardIndent
		public int reorderID;
		private float reorderRectHeight;

		public bool DrawQueriesListing(Listing_StandardIndent listing, bool locked, string indentAfterFirst = null)
		{
			Rect coveredRect = new Rect(0f, listing.CurHeight, listing.ColumnWidth, reorderRectHeight);
			if (Event.current.type == EventType.Repaint)
			{
				reorderID = ReorderableWidget.NewGroup(
					(int from, int to) => Reorder(from, to, true),
					ReorderableDirection.Vertical,
					coveredRect, 1f,
					extraDraggedItemOnGUI: (int index, Vector2 dragStartPos) =>
						DrawMouseAttachedQuery(queries[index], coveredRect.width - 100));

				// Turn off The Multigroup system assuming that if you're closer to group A but in group B's rect, that you want to insert at end of B.
				// That just doesn't apply here.
				// (it uses absRect to check mouseover group B and that overrides if you're in an okay place to drop in group A)
				var group = ReorderableWidget.groups[reorderID];	//immutable struct O_o
				group.absRect = new Rect();
				ReorderableWidget.groups[reorderID] = group;
			}

			bool changed = false;
			HashSet<ThingQuery> removedQueries = new();
			bool first = true;
			foreach (ThingQuery query in queries)
			{
				Rect usedRect = listing.GetRect(0);

				(bool ch, bool d) = query.Listing(listing, locked);
				changed |= ch;
				if (d)
					removedQueries.Add(query);

				//Reorder box with only one line tall ;
				//TODO: make its yMax = query.CurHeight,
				//but then you can't drag AWAY from subqueries,
				//though it's correct where you drag TO
				usedRect.height = Text.LineHeight;
				ReorderableWidget.Reorderable(reorderID, usedRect);

				// Highlight the queries that pass for selected objects (useful for "any" queries)
				if (!(query is IQueryHolder) && Find.UIRoot is UIRoot_Play && Find.Selector.SelectedObjects.Any(o => o is Thing t && query.AppliesTo(t)))
				{
					usedRect.yMax = listing.CurHeight;
					Widgets.DrawHighlight(usedRect);
				}
				if(first)
				{
					first = false;
					if (indentAfterFirst != null)
						listing.NestedIndent(indentAfterFirst);
				}
			}
			// do the indent with no objects for the "Add new"
			if (first && indentAfterFirst != null)
					listing.NestedIndent(indentAfterFirst);

			reorderRectHeight = listing.CurHeight - coveredRect.y;

			RemoveAll(removedQueries);

			if (!locked)
				DrawAddRow(listing);

			if (indentAfterFirst != null)
				listing.NestedOutdent();

			return changed;
		}

		public static void DrawMouseAttachedQuery(ThingQuery dragQuery, float width)
		{
			Vector2 mousePositionOffset = Event.current.mousePosition + Vector2.one * 12;
			Rect dragRect = new Rect(mousePositionOffset, new(width, Text.LineHeight));

			//Same id 34003428 as GenUI.DrawMouseAttachment
			Find.WindowStack.ImmediateWindow(34003428, dragRect, WindowLayer.Super,
				() => dragQuery.DrawMain(dragRect.AtZero(), true),
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
			Widgets.Label(textRect, "TD.AddNewQuery...".Translate());

			Widgets.DrawHighlightIfMouseover(addRow);

			if (Widgets.ButtonInvisible(addRow))
			{
				DoFloatAllQueries();
			}
		}

		public void DoFloatAllQueries()
		{
			List<FloatMenuOption> options = new List<FloatMenuOption>();
			foreach (ThingQuerySelectableDef def in ThingQueryMaker.SelectableList)
			{
				if (def is ThingQueryDef fDef)
					options.Add(new FloatMenuOption(
						fDef.LabelCap,
						() => Add(ThingQueryMaker.MakeQuery(fDef), focus: true)
					));
				if (def is ThingQueryCategoryDef cDef)
					options.Add(new FloatMenuOption(
						"+ " + cDef.LabelCap,
						() => DoFloatAllCategory(cDef)
					));
			}
			Find.WindowStack.Add(new FloatMenu(options));
		}

		public void DoFloatAllCategory(ThingQueryCategoryDef cDef)
		{
			List<FloatMenuOption> options = new List<FloatMenuOption>();
			foreach (ThingQueryDef def in cDef.SubQueries)
			{
				// I don't think we need to worry about double-nested queries
				options.Add(new FloatMenuOption(
					def.LabelCap,
					() => Add(ThingQueryMaker.MakeQuery(def), focus: true)
				));
			}
			Find.WindowStack.Add(new FloatMenu(options));
		}
	}
}

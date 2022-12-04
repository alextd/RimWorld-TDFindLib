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
		public string Name { get; }
		public HeldQueries Children { get; }
		public IQueryHolder Parent { get; }
		public IQueryHolder RootHolder { get; }	//Either return this or a parent
		public void Root_NotifyUpdated();
		public void Root_NotifyRefUpdated();
		public bool Root_Active { get; }
	}

	// QueryHolder is actually one of the latest untested additions.
	// Everything went through QuerySearch which search on all things in a map
	//  Then I thought "Hey, maybe I should open this up to any list of things"
	// So that's QueryHolder, it simply holds and applies queries to given things.
	public class QueryHolder : IQueryHolder, IExposable
	{
		public string name = "Query Holder";
		public String Name => name;

		// What to search for
		protected HeldQueries children;

		// If you clone a QueryHolder it starts unchanged.
		// Not used directly but good to know if a save is needed.
		public bool changed;


		// from IQueryHolder:
		public IQueryHolder Parent => null;
		public virtual IQueryHolder RootHolder => this;
		public HeldQueries Children => children;
		public virtual void Root_NotifyUpdated() { }
		public virtual void Root_NotifyRefUpdated() => RebindMap();
		public virtual bool Root_Active => false;

		public QueryHolder()
		{
			children = new(this);
		}

		public virtual void ExposeData()
		{
			children.ExposeData();
		}


		public virtual void Reset()
		{
			changed = true;

			children.Clear();
			UnbindMap();
		}

		public QueryHolder Clone()
		{
			QueryHolder newHolder = new();

			newHolder.children = children.Clone(newHolder);

			return newHolder;
		}


		private Map boundMap;
		public void UnbindMap() => boundMap = null;

		public void RebindMap()
		{
			if (boundMap == null) return;

			children.DoResolveRef(boundMap);
		}
		public void BindToMap(Map map)
		{
			if (boundMap == map) return;

			boundMap = map;

			children.DoResolveRef(boundMap);
		}

		// Check if the thing passes the queries.
		// A Map is needed for certain filters like zones and areas.
		public bool AppliesTo(Thing thing, Map map = null)
		{
			if(map != null)
				BindToMap(map);

			return children.AppliesTo(thing);
		}
		public void Filter(ref List<Thing> newListedThings, Map map = null)
		{
			if (map != null)
				BindToMap(map);

			children.Filter(ref newListedThings);
		}


		// This is a roundabout way to hijack the esc-keypress from a window before it closes the window.
		// Any window displaying this has to override OnCancelKeyPressed and call this
		public bool OnCancelKeyPressed()
		{
			return children.Any(f => f.OnCancelKeyPressed());
		}
	}


	public class HeldQueries // : IExposable //Not IExposable because that means ctor QueryHolder() should exist.
	{
		private IQueryHolder parent;
		public List<ThingQuery> queries = new List<ThingQuery>() { };
		public bool matchAllQueries = true;	// or ANY

		public HeldQueries(IQueryHolder p)
		{
			parent = p;
		}

		public void ExposeData()
		{
			Scribe_Collections.Look(ref queries, "queries");
			Scribe_Values.Look(ref matchAllQueries, "matchAllQueries", forceSave: true);	//Force save because the default is different in different contexts

			if(Scribe.mode == LoadSaveMode.LoadingVars)
				foreach (var f in queries)
					f.parent = parent;
		}

		public HeldQueries Clone(IQueryHolder newParent)
		{
			HeldQueries clone = new HeldQueries(newParent);

			foreach (var f in queries)
				clone.Add(f.Clone(), remake: false);

			clone.matchAllQueries = matchAllQueries;

			return clone;
		}


		public void DoResolveRef(Map map)
		{
			queries.ForEach(f => f.DoResolveRef(map));
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
			if (remake) parent.RootHolder?.Root_NotifyUpdated();
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

		public void DoReorderQuery(int from, int to, bool remake = true)
		{
			var draggedQuery = queries[from];
			if (Event.current.control)
			{
				var newQuery = draggedQuery.Clone();
				Add(newQuery, to, remake);
			}
			else
			{
				queries.RemoveAt(from);
				Add(draggedQuery, from < to ? to - 1 : to, remake);
			}
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
				{
					// Hold up, don't drop inside yourself or any of your sacred lineage
					for(IQueryHolder ancestor = holder; ancestor != null; ancestor = ancestor.Parent)
						if (draggedQuery == ancestor)
							return;

					newHolder = holder; //todo: abort early?
				}
			});

			if (newHolder != null)
			{
				if (Event.current.control)
				{
					newHolder.Children.Add(draggedQuery.Clone(), to);
				}
				else
				{
					draggedQuery.parent.Children.queries.Remove(draggedQuery);
					newHolder.Children.Add(draggedQuery, to);
				}
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

			ReorderableWidget.NewMultiGroup(reorderIDs, parent.RootHolder.Children.MasterReorder);

			listing.EndScrollView(ref scrollHeight);

			return changed;
		}


		// draw queries continuing a Listing_StandardIndent
		public int reorderID;
		private float reorderRectHeight;

		public bool DrawQueriesListing(Listing_StandardIndent listing, bool locked, string indentAfterFirst = null)
		{
			float startHeight = listing.CurHeight;

			if (Event.current.type == EventType.Repaint)
			{
				Rect reorderRect = new Rect(0f, startHeight, listing.ColumnWidth, reorderRectHeight);
				reorderID = ReorderableWidget.NewGroup(
					(int from, int to) => DoReorderQuery(from, to, true),
					ReorderableDirection.Vertical,
					reorderRect, 1f,
					extraDraggedItemOnGUI: (int index, Vector2 dragStartPos) =>
						DrawMouseAttachedQuery(queries[index], reorderRect.width - 100));
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

			RemoveAll(removedQueries);

			if (!locked)
				DrawAddRow(listing);

			reorderRectHeight = listing.CurHeight - startHeight;

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



		// APPLY THE QUERIES!
		public bool AppliesTo(Thing t) =>
			matchAllQueries ? queries.All(f => !f.Enabled || f.AppliesTo(t)) :
				queries.Any(f => f.Enabled && f.AppliesTo(t));


		// Apply to a list of things
		private List<Thing> newFilteredThings = new();
		public void Filter(ref List<Thing> newListedThings)
		{
			var usedQueries = queries.FindAll(f => f.Enabled);
			if (matchAllQueries)
			{
				// ALL
				foreach (ThingQuery query in usedQueries)
				{
					// Clears newQueriedThings, fills with newListedThings which pass the query.
					query.Apply(newListedThings, newFilteredThings);

					// newQueriedThings is now the list of things ; swap them
					(newListedThings, newFilteredThings) = (newFilteredThings, newListedThings);
				}
			}
			else
			{
				// ANY
				newFilteredThings.Clear();
				foreach (Thing thing in newListedThings)
					if (usedQueries.Any(f => f.AppliesTo(thing)))
						newFilteredThings.Add(thing);

				(newListedThings, newFilteredThings) = (newFilteredThings, newListedThings);
			}

			newFilteredThings.Clear();
		}
	}
}

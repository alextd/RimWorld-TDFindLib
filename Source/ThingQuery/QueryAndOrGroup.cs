using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using RimWorld;

namespace TD_Find_Lib
{
	public class ThingQueryAndOrGroup : ThingQuery, IQueryHolder
	{
		public bool any = true; // or all
		private QueryHolder children;
		public QueryHolder Children => children;

		public ThingQueryAndOrGroup()
		{
			children = new QueryHolder(this);
		}
		public override bool ApplesDirectlyTo(Thing t) =>
			any ? Children.queries.Any(f => f.Enabled && f.ApplesDirectlyTo(t)) :
			Children.queries.All(f => !f.Enabled || f.ApplesDirectlyTo(t));

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref any, "any", true);

			Children.ExposeData();
		}
		public override ThingQuery Clone()
		{
			ThingQueryAndOrGroup clone = (ThingQueryAndOrGroup)base.Clone();
			clone.children = children.Clone(clone);
			clone.any = any;

			return clone;
		}

		public override bool DrawMain(Rect rect, bool locked)
		{
			bool changed = false;
			WidgetRow row = new WidgetRow(rect.x, rect.y);
			row.Label("TD.IncludeThingsThatMatch".Translate());
			if (row.ButtonTextNoGap(any ? "TD.AnyOption".Translate() : "TD.AllOptions".Translate()))
			{
				any = !any;
				changed = true;
			}
			row.Label("TD.OfTheseQueries".Translate());
			return changed;
		}

		protected override bool DrawUnder(Listing_StandardIndent listing, bool locked)
		{
			listing.NestedIndent();
			listing.Gap(listing.verticalSpacing);

			//Draw queries
			bool changed = Children.DrawQueriesListing(listing, locked, (any ? "OR" : "AND").Colorize(Color.green));

			listing.NestedOutdent();
			return changed;
		}
	}

	public class ThingQueryInventory : ThingQueryAndOrGroup
	{
		protected bool holdingThis;//or what I'm holding

		List<Thing> _containedThings = new();
		public override bool ApplesDirectlyTo(Thing t)
		{
			if (holdingThis)
			{
				IThingHolder parent = t.ParentHolder;
				while (parent.IsValidHolder())
				{
					if (parent is Thing parentThing && base.ApplesDirectlyTo(parentThing))
						return true;
					parent = parent.ParentHolder;
				}
			}
			else
			{
				if (t is IThingHolder holder)
				{
					//It wouldn't get this far it if were fogged so don't need to check that.
					ContentsUtility.AllKnownThingsInside(holder, _containedThings);

					foreach (Thing containedThing in _containedThings)
						if (base.ApplesDirectlyTo(containedThing))
							return true;
				}
			}
			return false;
		}
		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref holdingThis, "holdingThis", true);
		}
		public override ThingQuery Clone()
		{
			ThingQueryInventory clone = (ThingQueryInventory)base.Clone();
			clone.holdingThis = holdingThis;
			return clone;
		}

		public override bool DrawMain(Rect rect, bool locked)
		{
			bool changed = false;
			WidgetRow row = new WidgetRow(rect.x, rect.y);
			if (row.ButtonTextNoGap(holdingThis ? "TD.TheThingHoldingThis".Translate() : "TD.AnythingThisIsHolding".Translate()))
			{
				changed = true;
				holdingThis = !holdingThis;
			}
			row.Label("TD.Matches".Translate());
			if (row.ButtonTextNoGap(any ? "TD.AnyOption".Translate() : "TD.AllOptions".Translate()))
			{
				changed = true;
				any = !any;
			}
			row.Label("TD.Of".Translate());
			return changed;
		}
	}

	public class ThingQueryNearby: ThingQueryAndOrGroup
	{
		IntRange range;

		public override bool ApplesDirectlyTo(Thing t)
		{
			IntVec3 pos = t.PositionHeld;
			Map map = t.MapHeld;

			CellRect cells = new CellRect(pos.x - range.max, pos.z - range.max, range.max * 2 + 1, range.max * 2 + 1);
			foreach (IntVec3 p in cells)
				if (map.thingGrid.ThingsAt(p).Any(child => base.ApplesDirectlyTo(child)))
					return true;
			return false;
		}
		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref range, "range");
		}
		public override ThingQuery Clone()
		{
			ThingQueryNearby clone = (ThingQueryNearby)base.Clone();
			clone.range = range;
			return clone;
		}

		public override bool DrawMain(Rect rect, bool locked)
		{
			bool changed = false;
			WidgetRow row = new WidgetRow(rect.x, rect.y);

			row.Label("TD.AnythingXStepsNearbyMatches".Translate());
			if (row.ButtonText(any ? "TD.AnyOption".Translate() : "TD.AllOptions".Translate()))
			{
				any = !any;
				changed = true;
			}

			changed |= TDWidgets.IntRange(rect.RightHalfClamped(row.FinalX), id, ref range, max: 10);
			range.min = 0; // sorry we're not looking in a ring but we do want the slider UI

			return changed;
		}
	}
}

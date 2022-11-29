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
		protected HeldQueries children;
		public HeldQueries Children => children;

		public void Root_NotifyUpdated() { }
		public void Root_NotifyRefUpdated() { }
		public bool Root_Active => false;
		public string Name => "??QueryAndOrGroup??";	//Should not be used.

		public ThingQueryAndOrGroup()
		{
			children = new HeldQueries(this);
			children.matchAllQueries = false; //default match any in sub-group. Otherwise it's just more ANDing...
		}
		public override bool AppliesDirectlyTo(Thing t) =>
			children.AppliesTo(t);

		public override void ExposeData()
		{
			base.ExposeData();

			Children.ExposeData();
		}
		public override ThingQuery Clone()
		{
			ThingQueryAndOrGroup clone = (ThingQueryAndOrGroup)base.Clone();
			clone.children = children.Clone(clone);

			return clone;
		}


		protected bool ButtonToggleAny(WidgetRow row)
		{
			if (row.ButtonTextNoGap(children.matchAllQueries ? "TD.AllOptions".Translate() : "TD.AnyOption".Translate()))
			{
				children.matchAllQueries = !children.matchAllQueries;
				return true;
			}
			return false;
		}

		public override bool DrawMain(Rect rect, bool locked)
		{
			WidgetRow row = new WidgetRow(rect.x, rect.y);

			row.Label("TD.IncludeThingsThatMatch".Translate());
			bool changed = ButtonToggleAny(row);
			row.Label("TD.OfTheseQueries".Translate());

			return changed;
		}

		protected override bool DrawUnder(Listing_StandardIndent listing, bool locked)
		{
			listing.NestedIndent();
			listing.Gap(listing.verticalSpacing);

			//Draw queries
			bool changed = Children.DrawQueriesListing(listing, locked, (children.matchAllQueries ? "TD.AND".Translate() : "TD.OR".Translate()).Colorize(Color.green));

			listing.NestedOutdent();
			return changed;
		}
	}

	public class ThingQueryInventory : ThingQueryAndOrGroup
	{
		protected bool holdingThis;//or what I'm holding

		List<Thing> _containedThings = new();
		public override bool AppliesDirectlyTo(Thing t)
		{
			if (holdingThis)
			{
				IThingHolder parent = t.ParentHolder;
				while (parent.IsValidHolder())
				{
					if (parent is Thing parentThing && base.AppliesDirectlyTo(parentThing))
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
						if (base.AppliesDirectlyTo(containedThing))
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
			changed |= ButtonToggleAny(row);
			row.Label("TD.Of".Translate());
			return changed;
		}
	}

	public class ThingQueryNearby: ThingQueryAndOrGroup
	{
		IntRange range;

		public override bool AppliesDirectlyTo(Thing t)
		{
			IntVec3 pos = t.PositionHeld;
			Map map = t.MapHeld;

			CellRect cells = new CellRect(pos.x - range.max, pos.z - range.max, range.max * 2 + 1, range.max * 2 + 1);
			foreach (IntVec3 p in cells)
				if (map.thingGrid.ThingsAt(p).Any(child => base.AppliesDirectlyTo(child)))
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
			WidgetRow row = new WidgetRow(rect.x, rect.y);

			row.Label("TD.AnythingXStepsNearbyMatches".Translate());
			bool changed = ButtonToggleAny(row);

			changed |= TDWidgets.IntRange(rect.RightHalfClamped(row.FinalX), id, ref range, max: 10);
			range.min = 0; // sorry we're not looking in a ring but we do want the slider UI

			return changed;
		}
	}
}

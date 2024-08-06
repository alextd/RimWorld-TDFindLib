using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using RimWorld;

namespace TD_Find_Lib
{
	public enum SubmatchType { Has, Any, All }

	public class ThingQueryAndOrGroup : ThingQuery, IQueryHolderDroppable
	{
		// IQueryHolder 
		protected HeldQueries children;
		public HeldQueries Children => children;
		public virtual bool AcceptsDrops => true;
		//Parent and RootHolder handled by ThingQuery


		// ThingQueryAndOrGroup
		public ThingQueryAndOrGroup()
		{
			children = new HeldQueries(this);
			children.matchAllQueries = false; //override HeldQueries default "any". Otherwise it's just more ANDing...
		}
		public override bool AppliesDirectlyTo(Thing t) =>
			children.AppliesTo(t);

		public override void ExposeData()
		{
			base.ExposeData();

			Children.ExposeData();
		}
		protected override ThingQuery Clone()
		{
			ThingQueryAndOrGroup clone = (ThingQueryAndOrGroup)base.Clone();
			clone.children = children.Clone(clone);

			return clone;
		}


		protected bool ButtonToggleAny()
		{
			bool changed = false;
			if (row.ButtonText(children.matchAllQueries ? "TD.AllOptions".Translate() : "TD.AnyOption".Translate()))
			{
				changed = true;

				//Cycle All => Any => any X of => All
				if (children.matchAllQueries) // All
				{
					children.matchAllQueries = false; // Any
				}
				else if(children.anyMin == 0) // Any
				{
					children.anyMin = 2; // any X of
				}
				else // Any X of
				{
					children.matchAllQueries = true; // All
					children.anyMin = 0;
				}
			}
			if (children.anyMin >= 1)
			{
				changed |= TDWidgets.Slider(row.GetRect(40), ref children.anyMin, 1, children.queries.Count > 3 ? children.queries.Count - 1 : 3, label: children.anyMin.ToString());
			}
			return changed;
		}

		protected override float RowGap => 0;
		protected override bool DrawMain(Rect rect, bool locked, Rect fullRect)
		{
			// no base.DrawMain, don't want label in the filter row
			row.Label(include ? "TD.IncludeThingsThatMatch".Translate() : "TD.ExcludeThingsThatMatch".Translate());
			bool changed = ButtonToggleAny();
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
		protected override ThingQuery Clone()
		{
			ThingQueryInventory clone = (ThingQueryInventory)base.Clone();
			clone.holdingThis = holdingThis;
			return clone;
		}

		protected override bool DrawMain(Rect rect, bool locked, Rect fullRect)
		{
			RowPrependNOT();
			bool changed = row.ButtonTextToggleBool(ref holdingThis, "TD.TheThingHoldingThis".Translate(), "TD.AnythingThisIsHolding".Translate());

			row.Label("TD.Matches".Translate());
			changed |= ButtonToggleAny();
			row.Label("TD.Of".Translate());
			return changed;
		}
	}

	public enum EquipmentFilterType { Any, Primary, Equipment, Apparel, Inventory, Carried, Unused}
	public class ThingQueryEquipment : ThingQueryAndOrGroup
	// Should be named "Gear" since this was expanded to include inventory + carried
	{
		protected EquipmentFilterType filterType;
		protected SubmatchType hasType;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref filterType, "filterType");
			Scribe_Values.Look(ref hasType, "hasType");
		}
		protected override ThingQuery Clone()
		{
			ThingQueryEquipment clone = (ThingQueryEquipment)base.Clone();
			clone.filterType = filterType;
			clone.hasType = hasType;
			return clone;
		}

		public IEnumerable<Thing> GearFor(Pawn pawn)
		{
			// "Any" is old name from when this didn't search inventory. It should be "Used".
			// TODO: Backward compat converter.

			// Apparel
			if (pawn.apparel != null)
			{
				if (filterType is EquipmentFilterType.Apparel or EquipmentFilterType.Any)
					foreach (var app in pawn.apparel.WornApparel)
						yield return app;
			}

			// Equipment (Also sub-categorized as primary)
			if (pawn.equipment != null)
			{
				if (filterType is EquipmentFilterType.Any)
					foreach (var eq in pawn.equipment.AllEquipmentListForReading)
						yield return eq;

				else if (filterType is EquipmentFilterType.Equipment)
					foreach (var eq in pawn.equipment.AllEquipmentListForReading
						.Where(eq => eq.def.equipmentType != EquipmentType.Primary))
						yield return eq;

				else if (filterType is EquipmentFilterType.Primary)
					if (pawn.equipment.Primary != null)
						yield return pawn.equipment.Primary;
			}

			// Inventory
			if (pawn.inventory != null)
			{
				if (filterType is EquipmentFilterType.Inventory or EquipmentFilterType.Unused)
					foreach (var eq in pawn.inventory.GetDirectlyHeldThings())
						yield return eq;
			}

			// Carried
			if (pawn.carryTracker != null)
			{
				if (filterType is EquipmentFilterType.Carried or EquipmentFilterType.Unused)
					if (pawn.carryTracker.CarriedThing != null)
						yield return pawn.carryTracker.CarriedThing;
			}
		}
		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			return hasType switch
			{
				SubmatchType.Has => GearFor(pawn).Any(),
				SubmatchType.Any => GearFor(pawn).Any(app => base.AppliesDirectlyTo(app)),
				_ => GearFor(pawn).Count() > 0 && GearFor(pawn).All(app => base.AppliesDirectlyTo(app)),
			};
		}

		protected override bool DrawMain(Rect rect, bool locked, Rect fullRect)
		{
			RowPrependNOT();
			bool changed = row.ButtonCycleEnum(ref hasType);
			RowButtonFloatMenuEnum(filterType, newValue => filterType = newValue);

			if (hasType == SubmatchType.Has) return changed;

			row.Label("TD.Matches".Translate());
			changed |= ButtonToggleAny();
			row.Label("TD.Of".Translate());

			return changed;
		}

		public override bool AcceptsDrops => hasType != SubmatchType.Has;
		protected override bool DrawUnder(Listing_StandardIndent listing, bool locked)
		{
			if (hasType == SubmatchType.Has)
				return false;

			return base.DrawUnder(listing, locked);
		}
	}

	public class ThingQueryNearby: ThingQueryAndOrGroup
	{
		IntRange range;

		public override bool AppliesDirectlyTo(Thing t)
		{
			IntVec3 pos = t.PositionHeld;
			Map map = t.MapHeld;

			CellRect cells = new(pos.x - range.max, pos.z - range.max, range.max * 2 + 1, range.max * 2 + 1);
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
		protected override ThingQuery Clone()
		{
			ThingQueryNearby clone = (ThingQueryNearby)base.Clone();
			clone.range = range;
			return clone;
		}

		protected override bool DrawMain(Rect rect, bool locked, Rect fullRect)
		{
			RowPrependNOT();
			row.Label("TD.AnythingXStepsNearbyMatchesPre".Translate());
			Rect rangeRect = row.GetRect(80);
			row.Label("TD.AnythingXStepsNearbyMatchesPost".Translate());
			bool changed = ButtonToggleAny();

			changed |= TDWidgets.IntRange(rangeRect, id, ref range, max: 10);
			range.min = 0; // sorry we're not looking in a ring but we do want the slider UI

			return changed;
		}
	}
}

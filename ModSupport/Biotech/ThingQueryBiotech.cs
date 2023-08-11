using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TD_Find_Lib;
using UnityEngine;
using Verse;
using RimWorld;

namespace TDFindLib_Biotech
{
	public enum BandwidthFilterType { Total, Used, Available}
	public class ThingQueryMechanitorBandwidth : ThingQueryIntRange
	{
		public override int Max => 40;

		BandwidthFilterType filterType;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref filterType, "filterType");
		}
		protected override ThingQuery Clone()
		{
			ThingQueryMechanitorBandwidth clone = (ThingQueryMechanitorBandwidth)base.Clone();
			clone.filterType = filterType;
			return clone;
		}

		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			Pawn_MechanitorTracker mech = pawn.mechanitor;
			if (mech == null) return false;

			return sel.Includes(filterType switch
			{
				BandwidthFilterType.Total => mech.TotalBandwidth,
				BandwidthFilterType.Used => mech.UsedBandwidth,
				_ => mech.TotalBandwidth - mech.UsedBandwidth
			});
		}

		public override bool DrawMain(UnityEngine.Rect rect, bool locked, UnityEngine.Rect fullRect)
		{
			bool changed = base.DrawMain(rect, locked, fullRect);

			RowButtonFloatMenuEnum(filterType, newValue => filterType = newValue);

			return changed;
		}
	}

	public class ThingQueryMechOverseer : ThingQueryAndOrGroup
	{
		protected bool overseer;//or controlled mechs
		protected bool allMechs;// or any mech

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref overseer, "overseer", true);
			Scribe_Values.Look(ref allMechs, "all", true);
		}
		protected override ThingQuery Clone()
		{
			ThingQueryMechOverseer clone = (ThingQueryMechOverseer)base.Clone();
			clone.overseer = overseer;
			clone.allMechs = allMechs;
			return clone;
		}


		public override bool AppliesDirectlyTo(Thing thing)
		{
			if (overseer)
			{
				if ((thing as Pawn)?.OverseerSubject?.Overseer is Pawn overseer)
					return Children.AppliesTo(overseer);
			}
			else
			{
				if ((thing as Pawn)?.mechanitor?.ControlledPawns is List<Pawn> mechs)
					return allMechs ?
						mechs.All(m => Children.AppliesTo(m)) :
						mechs.Any(m => Children.AppliesTo(m));
			}
			return false;
		}

		public override bool DrawMain(Rect rect, bool locked, Rect fullRect)
		{
			bool changed = false;

			if (!overseer)
				changed |= row.ButtonTextToggleBool(ref allMechs, "TD.AllOptions".Translate(), "TD.AnyOption".Translate());

			changed |= row.ButtonTextToggleBool(ref overseer, "Mech's overseer", "Mechanitor's mechs");


			row.Label("TD.Matches".Translate());
			changed |= ButtonToggleAny();
			row.Label("TD.Of".Translate());

			return changed;
		}
	}

	public class ThingQueryMechWeightClass : ThingQueryDropDown<MechWeightClass>
	{
		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			if (!pawn.RaceProps.IsMechanoid) return false;

			return pawn.RaceProps.mechWeightClass == sel;
		}

		public override string NameFor(MechWeightClass w) => w.ToStringHuman().CapitalizeFirst();
	}

	public class ThingQueryMechBandwidthCost : ThingQueryIntRange
	{
		public static int _maxCost = DefDatabase<ThingDef>.AllDefs
			.Max(def => (int)def.GetStatValueAbstract(StatDefOf.BandwidthCost));

		public override int Min => 1;
		public override int Max => _maxCost;

		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			if (!pawn.RaceProps.IsMechanoid) return false;

			return sel.Includes((int)pawn.def.GetStatValueAbstract(StatDefOf.BandwidthCost));
		}
	}

	public class ThingQueryMechWorkMode : ThingQueryDropDown<MechWorkModeDef>
	{
		public ThingQueryMechWorkMode() => sel = MechWorkModeDefOf.Work;

		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			if (!pawn.RaceProps.IsMechanoid) return false;

			return pawn.GetMechWorkMode() == sel;
		}
	}

	public class ThingQueryMechFeral : ThingQueryIntRange
	{
		public static int _maxCost = DefDatabase<ThingDef>.AllDefs
			.Max(def => def.comps?.Select(p => (p as CompProperties_OverseerSubject)?.delayUntilFeralCheck ?? 0).MaxByWithFallback(x => x, 0) ?? 0);

		public override int Max => _maxCost;

		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			if (!pawn.RaceProps.IsMechanoid) return false;
			if (pawn.Faction != Faction.OfPlayer) return false;

			CompOverseerSubject comp = pawn.overseerSubject;  // lowercase o : don't create it if it doesn't exist.
			if (comp == null) return false;
			if (comp.State == OverseerSubjectState.Overseen) return false;

			int ticksUncontrolled = comp.Props.delayUntilFeralCheck - comp.delayUntilFeralCheck;

			return sel.Includes(ticksUncontrolled);
		}

		/*
		 * Flippin Widgets.IntRange takes a translation key, not a string itself.
		public override Func<int, string> Writer => (int ticks) =>
			ticks.ToStringTicksToPeriod(allowSeconds: true, shortForm: true);
		*/
		public override Func<int, string> Writer => ticks => $"{ticks * 1f / GenDate.TicksPerHour:0.0}";
	}

	public class ThingQueryXenotype : ThingQueryDropDown<XenotypeDef>
	{
		public ThingQueryXenotype() => sel = XenotypeDefOf.Baseliner;

		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			return pawn.genes?.xenotype == sel;
		}
	}
}

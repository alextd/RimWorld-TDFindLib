using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TD_Find_Lib;
using Verse;
using RimWorld;
using UnityEngine;

namespace TDFindLib_Royalty
{
	// It is a bit roundabout to have a project for this when there is no new dll dependency but, eh, organization I guess.
	public class ThingQueryRoyalTitle : ThingQueryDropDown<RoyalTitleDef>
	{
		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null || pawn.royalty == null) return false;

			if (extraOption > 0)
				return pawn.royalty.AllTitlesForReading.Count > 0;

			if (sel == null)
				return pawn.royalty.AllTitlesForReading.Count == 0;

			return pawn.royalty.HasTitle(sel);
		}

		public override string NullOption() => "None".Translate();
		public override int ExtraOptionsCount => 1;
		public override string NameForExtra(int ex) => "TD.AnyOption".Translate();
	}

	public class ThingQueryHonor : ThingQueryDropDown<Faction>
	{
		protected IntRangeUB favorRange;

		public static int maxFavor = DefDatabase<RoyalTitleDef>.DefCount == 0 ? 0 :
			2 * DefDatabase<RoyalTitleDef>.AllDefsListForReading.Max(d => d.favorCost);
		public ThingQueryHonor()
		{
			extraOption = 1;
			favorRange = new(0, maxFavor);//Probably reasonable to be 2x highest title cost (remember it's unbounded range so range.max means "or higher")(
		}

		protected override Faction ResolveRef(Map map) =>
			Current.Game.World.factionManager.AllFactionsVisibleInViewOrder.FirstOrDefault(f => f.Name == selName);

		public override string NameFor(Faction f) => f.Name;
		protected override string MakeSaveName() => sel.Name;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref favorRange.range, "range");
		}
		public override ThingQuery Clone()
		{
			ThingQueryHonor clone = (ThingQueryHonor)base.Clone();
			clone.favorRange = favorRange;
			return clone;
		}

		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null || pawn.royalty == null) return false;

			if(extraOption == 1) //Any
				return pawn.royalty.favor.Any(kvp => favorRange.Includes(kvp.Value));

			return favorRange.Includes(pawn.royalty.GetFavor(sel));
		}
		public override bool DrawCustom(Rect rect, WidgetRow row, Rect fullRect)
		{
			return TDWidgets.IntRangeUB(fullRect.RightHalfClamped(row.FinalX), id, ref favorRange);
		}

		public override IEnumerable<Faction> Options() =>
			Current.Game.World.factionManager.AllFactionsVisibleInViewOrder.Where(f => f.def.HasRoyalTitles);


		public override int ExtraOptionsCount => 1;
		public override string NameForExtra(int ex) => "TD.AnyOption".Translate();
	}

	public class ThingQueryPsyfocus : ThingQueryFloatRange
	{
		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null || !pawn.HasPsylink) return false;


			return sel.Includes(pawn.psychicEntropy.CurrentPsyfocus);
		}
	}

	public class ThingQueryEntropyValue : ThingQueryFloatRange
	{
		public override float Max => 80f;   //Base 30 * max buff of 2.667
		public override ToStringStyle Style => ToStringStyle.Integer;

		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null || !pawn.HasPsylink) return false;


			return sel.Includes(pawn.psychicEntropy.EntropyValue);
		}
	}

	[StaticConstructorOnStartup]
	public static class ExpansionHider
	{
		static ExpansionHider()
		{
			if (!ModsConfig.RoyaltyActive)
				foreach (ThingQueryDef def in DefDatabase<ThingQueryDef>.AllDefsListForReading)
					if (def.mod == "ludeon.rimworld.royalty")
						def.devOnly = true;
		}
	}
}

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

	public class ThingQueryRoyalTitleRange : ThingQueryDropDown<Faction>
	{
		protected IntRange seniorityRange;

		public ThingQueryRoyalTitleRange()
		{
			extraOption = 1;
		}

		public static List<RoyalTitleDef> RoyalTitleDefsFor(Faction fac) =>
			fac != null ? fac.def.RoyalTitlesAllInSeniorityOrderForReading :
			DefDatabase<RoyalTitleDef>.AllDefsListForReading;

		public List<RoyalTitleDef> RoyalTitleDefsForSel()
		{
			if (Current.Game?.World == null || extraOption == 1)
				return RoyalTitleDefsFor(null);

			return RoyalTitleDefsFor(sel);
		}

		public static IntRange SeniorityRangeFor(Faction fac)
		{
			var defs = RoyalTitleDefsFor(fac);
			return new IntRange(defs.Min(def => def.seniority), defs.Max(def => def.seniority));
		}

		public IntRange SeniorityRangeForSel()
		{
			if (Current.Game?.World == null || extraOption == 1)
				return SeniorityRangeFor(null);

			return SeniorityRangeFor(sel);
		}

		protected override Faction ResolveRef(Map map) =>
			Current.Game.World.factionManager.AllFactionsVisibleInViewOrder.FirstOrDefault(f => f.Name == selName);

		public override string NameFor(Faction f) => f.Name;
		protected override string MakeSaveName() => sel.Name;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref seniorityRange, "range");
		}
		public override ThingQuery Clone()
		{
			ThingQueryRoyalTitleRange clone = (ThingQueryRoyalTitleRange)base.Clone();
			clone.seniorityRange = seniorityRange;
			return clone;
		}

		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null || pawn.royalty == null) return false;

			if (extraOption == 1) //Any
				return pawn.royalty.highestTitles.Values.Any(def => seniorityRange.Includes(def.seniority));

			if(pawn.royalty.GetCurrentTitle(sel) is RoyalTitleDef def)
				return seniorityRange.Includes(def.seniority);

			return false;
		}
		public override bool DrawCustom(Rect rect, WidgetRow row, Rect fullRect)
		{
			Rect buttonRect = fullRect.RightHalfClamped(row.FinalX);
			DoSeniorityDropdown(buttonRect.LeftHalf(), seniorityRange.min, s => seniorityRange.min = s, max: seniorityRange.max);
			DoSeniorityDropdown(buttonRect.RightHalf(), seniorityRange.max, s => seniorityRange.max = s, min: seniorityRange.min);
			return false;
		}

		private void DoSeniorityDropdown(Rect rect, int seniority, Action<int> selectedAction, int min = 0, int max = int.MaxValue)
		{
			List<RoyalTitleDef> titles = RoyalTitleDefsForSel();
			RoyalTitleDef selectedDef = titles.FirstOrDefault(def => def.seniority == seniority);

			if (Widgets.ButtonText(rect, selectedDef?.LabelCap ?? "???"))
			{
				List<FloatMenuOption> options = new List<FloatMenuOption>();
				foreach (RoyalTitleDef def in titles)
				{
					if(def.seniority >= min && def.seniority <= max)
						options.Add(new FloatMenuOptionAndRefresh(def.LabelCap, () => selectedAction(def.seniority), this));
				}

				DoFloatOptions(options);
			}
		}


		public override IEnumerable<Faction> Options() =>
			Current.Game?.World?.factionManager.AllFactionsVisibleInViewOrder.Where(f => f.def.HasRoyalTitles) ?? Enumerable.Empty<Faction>();


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
			Current.Game?.World?.factionManager.AllFactionsVisibleInViewOrder.Where(f => f.def.HasRoyalTitles) ?? Enumerable.Empty<Faction>();


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

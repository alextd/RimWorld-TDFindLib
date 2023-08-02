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
		protected override ThingQuery Clone()
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
		public override bool DrawCustom(Rect fullRect)
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


		public override IEnumerable<Faction> AllOptions() =>
			Current.Game?.World?.factionManager.AllFactionsVisibleInViewOrder
			.Where(f => f.def.HasRoyalTitles);


		public override int ExtraOptionsCount => 1;
		public override string NameForExtra(int ex) => "TD.AnyOption".Translate();
	}

	// From Royalty Alerts.xml or Thoughts_RoyalTitles.xml
	public enum UnmetRequirementType { 
		NeedThroneAssigned, UndignifiedThroneroom, 
		NeedBedroomAssigned, UndignifiedBedroom, 
		RoyalNoAcceptableFood, 
		ApparelRequirementNotMet, ApparelMinQualityNotMet }
	public class ThingQueryRoyalTitleRequirementUnmet : ThingQueryDropDown<UnmetRequirementType>
	{
		public ThingQueryRoyalTitleRequirementUnmet() => extraOption = 1;

		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null || pawn.royalty == null) return false;

			if(extraOption == 1)
			{
				foreach (UnmetRequirementType type in Enum.GetValues(typeof(UnmetRequirementType)))
					if (UnmetRequirement(pawn, type))
						return true;
			}

			return UnmetRequirement(pawn, sel);
		}

		public static bool UnmetRequirement(Pawn pawn, UnmetRequirementType req)
		{
			switch (req)
			{
				case UnmetRequirementType.NeedThroneAssigned:
					//Alert_RoyalNoThroneAssigned
					if (pawn.Suspended || !pawn.royalty.CanRequireThroneroom())
						return false;

					foreach (var title in pawn.royalty.titles)
						if (!title.def.throneRoomRequirements.NullOrEmpty() && pawn.ownership.AssignedThrone == null)
							return true;

					return false;

				case UnmetRequirementType.UndignifiedThroneroom:
					//Alert_UndignifiedThroneroom
					return !pawn.Suspended && pawn.royalty.GetUnmetThroneroomRequirements(false).Any();


				case UnmetRequirementType.NeedBedroomAssigned:
					//Alert_TitleRequiresBedroom
					return pawn.royalty.HighestTitleWithBedroomRequirements() != null && !pawn.Suspended && !pawn.royalty.HasPersonalBedroom();

				case UnmetRequirementType.UndignifiedBedroom:
					//Alert_UndignifiedBedroom
					return !pawn.Suspended && pawn.royalty.GetUnmetBedroomRequirements(false).Any();


				case UnmetRequirementType.RoyalNoAcceptableFood:
					//Alert_RoyalNoAcceptableFood

					if (pawn.Spawned && (pawn.story == null || !pawn.story.traits.HasTrait(TraitDefOf.Ascetic)))
					{
						RoyalTitle royalTitle = pawn.royalty?.MostSeniorTitle;
						if (royalTitle != null && royalTitle.conceited && royalTitle.def.foodRequirement.Defined &&
							!FoodUtility.TryFindBestFoodSourceFor_NewTemp(pawn, pawn, desperate: false, out var _, out var _, allowCorpse: false, ignoreReservations: true, minPrefOverride: FoodPreferability.DesperateOnly))
						{
							return true;
						}
					}
					return false;


				case UnmetRequirementType.ApparelRequirementNotMet:
					//ThoughtWorker_RoyalTitleApparelRequirementNotMet
					if (!pawn.royalty.allowApparelRequirements)
						return false;


					foreach (RoyalTitle t in pawn.royalty.AllTitlesInEffectForReading)
					{
						if (t.def.requiredApparel == null || t.def.requiredApparel.Count <= 0)
							continue;

						for (int i = 0; i < t.def.requiredApparel.Count; i++)
						{
							ApparelRequirement apparelRequirement = t.def.requiredApparel[i];
							if (apparelRequirement.IsActive(pawn) && !apparelRequirement.IsMet(pawn))
								return true;
						}
					}
					return false;

				case UnmetRequirementType.ApparelMinQualityNotMet:
					//ThoughtWorker_RoyalTitleApparelMinQualityNotMet
					if (pawn.royalty.AllTitlesForReading.Count == 0)
						return false;

					QualityCategory minQuality = pawn.royalty.AllTitlesInEffectForReading.Max(t => t.def.requiredMinimumApparelQuality);

					foreach (var apparel in pawn.apparel.WornApparel)
						if (apparel.TryGetQuality(out var qc) && qc < minQuality)
							return true;

					return false;
			}

			return false;
		}


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
		protected override ThingQuery Clone()
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
		public override bool DrawCustom(Rect fullRect)
		{
			return TDWidgets.IntRangeUB(fullRect.RightHalfClamped(row.FinalX), id, ref favorRange);
		}

		public override IEnumerable<Faction> AllOptions() =>
			Current.Game?.World?.factionManager.AllFactionsVisibleInViewOrder
			.Where(f => f.def.HasRoyalTitles);


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

	public class ThingQueryPermit : ThingQueryDropDown<RoyalTitlePermitDef>
	{
		public Faction faction; //null = Any!
		public static RoyalTitlePermitDef TradeSettlement = DefDatabase<RoyalTitlePermitDef>.GetNamedSilentFail("TradeSettlement");
		public bool onlyReady;	// filter only if ready 

		public ThingQueryPermit()
		{
			if (TradeSettlement == null)
				extraOption = 1;
			else
				sel = TradeSettlement;
			faction = null;
			onlyReady = true;
		}

		// Given it exists, does this filter apply?
		// If we're not checking if it's ready, existence is enough
		// Otherwise, it only applies if it's ready aka not on cooldown
		public bool Applies(FactionPermit p) => !onlyReady || !p.OnCooldown;

		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null || pawn.royalty == null) return false;

			if (extraOption == 1) //ANY permit
			{
				if (faction == null)  //ANY faction
					return pawn.royalty.AllFactionPermits.Any(Applies);

				return pawn.royalty.PermitsFromFaction(faction).Any(Applies);
			}

			// sel only
			if(faction == null)	//ANY faction
				return pawn.royalty.AllFactionPermits.Any(p => p.Permit == sel && Applies(p));

			return pawn.royalty.GetPermit(sel, faction) is FactionPermit p && Applies(p);
		}

		public override bool DrawCustom(Rect fullRect)
		{
			if(row.ButtonText(faction?.Name ?? "TD.AnyOption".Translate()))
			{
				List<FloatMenuOption> options = new List<FloatMenuOption>();

				if (Current.Game?.World is RimWorld.Planet.World world)
					foreach (Faction fac in world.factionManager.AllFactionsVisibleInViewOrder.Where(f => f.def.HasRoyalTitles))
						options.Add(new FloatMenuOptionAndRefresh(fac.Name, () => faction = fac, this));

				options.Add(new FloatMenuOptionAndRefresh("TD.AnyOption".Translate(), () => faction = null, this, Color.yellow));

				DoFloatOptions(options);
			}
			if(row.ButtonText(onlyReady ? "TD.PermitIsReady".Translate() : "TD.HoldsPermit".Translate()))
			{
				onlyReady = !onlyReady;
				return true;
			}
			return false;
		}

		public override int ExtraOptionsCount => 1;
		public override string NameForExtra(int ex) => "TD.AnyOption".Translate();
	}

	public class ThingQueryPermitPoints : ThingQueryIntRange
	{
		public override int Min => 0;
		public override int Max => 5; //Seems enough

		public ThingQueryPermitPoints() => _sel.range.min = 1;

		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null || pawn.royalty == null) return false;

			int points = 0;
			foreach (Faction faction in Find.FactionManager.AllFactionsVisible)
				points += pawn.royalty.GetPermitPoints(faction);

			return sel.Includes(points);
		}
	}



	[StaticConstructorOnStartup]
	public static class ExpansionHider
	{
		static ExpansionHider()
		{
			if (!ModsConfig.RoyaltyActive)
				foreach (ThingQuerySelectableDef def in DefDatabase<ThingQuerySelectableDef>.AllDefsListForReading)
					if (def.mod == ModContentPack.RoyaltyModPackageId)
						def.devOnly = true;
		}
	}
}

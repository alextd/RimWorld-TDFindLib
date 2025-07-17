using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Verse;
using RimWorld;
using UnityEngine;

namespace TD_Find_Lib
{
	public enum RacePropsQuery { Predator, Prey, Herd, Pack, Wildness, Petness, Trainability, Intelligence, RevengeTame, RevengeHarm}
	public class ThingQueryRaceProps : ThingQueryDropDown<RacePropsQuery>
	{
		Intelligence intelligence;
		FloatRangeUB valueRange = new FloatRange(0, 1);
		TrainabilityDef trainability;

		protected override void PostChosen()
		{
			switch (sel)
			{
				case RacePropsQuery.Intelligence: intelligence = Intelligence.Humanlike; return;
				case RacePropsQuery.Wildness:
				case RacePropsQuery.Petness: valueRange.range = new FloatRange(0.25f, 0.75f); return;
				case RacePropsQuery.Trainability: trainability = TrainabilityDefOf.Advanced; return;
			}
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref intelligence, "intelligence");
			Scribe_Values.Look(ref valueRange.range, "valueRange");
			Scribe_Defs.Look(ref trainability, "trainability");
		}
		protected override ThingQuery Clone()
		{
			ThingQueryRaceProps clone = (ThingQueryRaceProps)base.Clone();
			clone.intelligence = intelligence;
			clone.valueRange = valueRange;
			clone.trainability = trainability;
			return clone;
		}

		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			RaceProperties props = pawn.RaceProps;
			if (props == null) return false;

			switch (sel)
			{
				case RacePropsQuery.Intelligence: return props.intelligence == intelligence;
				case RacePropsQuery.Herd:
					return props.herdAnimal;
				case RacePropsQuery.Pack:
					return props.packAnimal;
				case RacePropsQuery.Predator:
					return props.predator;
				case RacePropsQuery.Prey:
					return props.canBePredatorPrey;
				case RacePropsQuery.Wildness:
					return valueRange.Includes(pawn.GetStatValue(StatDefOf.Wildness));
				case RacePropsQuery.Petness:
					return valueRange.Includes(props.petness);
				case RacePropsQuery.RevengeTame:
					return valueRange.Includes(props.manhunterOnTameFailChance);
				case RacePropsQuery.RevengeHarm:
					return valueRange.Includes(props.manhunterOnDamageChance);
				case RacePropsQuery.Trainability:
					return props.trainability == trainability;
			}
			return false;
		}

		public override bool DrawCustom(Rect fullRect)
		{
			List<FloatMenuOption> options = new();
			switch (sel)
			{
				case RacePropsQuery.Intelligence:
					RowButtonFloatMenuEnum(intelligence, newValue => intelligence = newValue);
					break;

				case RacePropsQuery.Wildness:
				case RacePropsQuery.Petness:
				case RacePropsQuery.RevengeTame:
				case RacePropsQuery.RevengeHarm:
					return TDWidgets.FloatRangeUB(fullRect.RightHalfClamped(row.FinalX), id, ref valueRange, valueStyle: ToStringStyle.PercentZero);

				case RacePropsQuery.Trainability:
					RowButtonFloatMenuDef(trainability, newValue => trainability = newValue);
					break;
			}
			return false;
		}
	}

	abstract public class ThingQueryProduct : ThingQueryDropDown<ThingDef>
	{
		protected IntRangeUB countRange;

		public ThingQueryProduct()
		{
			extraOption = 1;
			countRange = new IntRangeUB(0, Max);  //Not PostChosen as this depends on subclass, not selection
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref countRange.range, "countRange");
		}
		protected override ThingQuery Clone()
		{
			ThingQueryProduct clone = (ThingQueryProduct)base.Clone();
			clone.countRange = countRange;
			return clone;
		}

		public abstract ThingDef DefFor(Pawn pawn);
		public abstract int CountFor(Pawn pawn);

		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			ThingDef productDef = DefFor(pawn);

			if (extraOption == 1)
				return productDef != null && countRange.Includes(CountFor(pawn));

			if (sel == null)
				return productDef == null;
			
			return sel == productDef && countRange.Includes(CountFor(pawn));
		}

		public abstract int Max { get; }
		public override bool DrawCustom(Rect fullRect)
		{
			//TODO: write 'IsNull' method to handle confusing extraOption == 1 but Sel == null
			if (extraOption == 0 && sel == null) return false;

			return TDWidgets.IntRangeUB(fullRect.RightHalfClamped(row.FinalX), id, ref countRange);
		}

		public override IEnumerable<ThingDef> AvailableOptions() =>
			ContentsUtility.AvailableInGame(t => t is Pawn pawn ? DefFor(pawn) : null);
		/*
	{
		HashSet<ThingDef> ret = new HashSet<ThingDef>();
		foreach (Map map in Find.Maps)
			foreach (Pawn p in map.mapPawns.AllPawns)
				if (DefFor(p) is ThingDef def)
					ret.Add(def);

		return ret;
	}
		*/

		public override bool Ordered => true;
		public override ThingDef IconDefFor(ThingDef o) => o;

		public override string NullOption() => "None".Translate();

		public override int ExtraOptionsCount => 1;
		public override string NameForExtra(int ex) => "TD.AnyOption".Translate();
	}

	public class ThingQueryMeat : ThingQueryProduct
	{
		public override ThingDef DefFor(Pawn pawn) => pawn.RaceProps.meatDef;
		public override int CountFor(Pawn pawn) => Mathf.RoundToInt(pawn.GetStatValue(StatDefOf.MeatAmount));

		public static List<ThingDef> allMeats = DefDatabase<ThingDef>.AllDefs.Where(d => d.IsMeat).ToList();
		public override IEnumerable<ThingDef> AllOptions() => allMeats;

		public static int mostMeat = DefDatabase<ThingDef>.AllDefs.Select(d => Mathf.RoundToInt(AnimalProductionUtility.AdultMeatAmount(d))).Max();
		public override int Max => mostMeat;
	}

	public class ThingQueryLeather : ThingQueryProduct
	{
		public override ThingDef DefFor(Pawn pawn) => pawn.RaceProps.leatherDef;
		public override int CountFor(Pawn pawn) => Mathf.RoundToInt(pawn.GetStatValue(StatDefOf.LeatherAmount));

		public static List<ThingDef> allLeathers = DefDatabase<ThingDef>.AllDefs.Where(d => d.IsLeather).ToList();
		public override IEnumerable<ThingDef> AllOptions() => allLeathers;

		public static int mostLeather = DefDatabase<ThingDef>.AllDefs.Select(d => Mathf.RoundToInt(AnimalProductionUtility.AdultLeatherAmount(d))).Max();
		public override int Max => mostLeather;
	}

	public class ThingQueryEgg : ThingQueryProduct //Per Year
	{
		public override ThingDef DefFor(Pawn pawn)
		{
			var props = pawn.def.GetCompProperties<CompProperties_EggLayer>();

			if (props == null)
				return null;
			if (props.eggLayFemaleOnly && pawn.gender != Gender.Female)
				return null;

			return props.eggUnfertilizedDef;
		}
		public override int CountFor(Pawn pawn) => Mathf.RoundToInt(AnimalProductionUtility.EggsPerYear(pawn.def));

		public static HashSet<ThingDef> allEggs = DefDatabase<ThingDef>.AllDefs.Select(d => d.GetCompProperties<CompProperties_EggLayer>()?.eggUnfertilizedDef).Where(d => d != null).ToHashSet();
		public override IEnumerable<ThingDef> AllOptions() => allEggs;

		public static int mostEggs = DefDatabase<ThingDef>.AllDefs.Select(d => Mathf.RoundToInt(AnimalProductionUtility.EggsPerYear(d))).Max();
		public override int Max => mostEggs;
	}


	public class ThingQueryMilk : ThingQueryProduct //Per Year
	{
		public override ThingDef DefFor(Pawn pawn)
		{
			var props = pawn.def.GetCompProperties<CompProperties_Milkable>();

			if (props == null)
				return null;
			if (props.milkFemaleOnly && pawn.gender != Gender.Female)
				return null;

			return props.milkDef;
		}
		public override int CountFor(Pawn pawn) => Mathf.RoundToInt(AnimalProductionUtility.MilkPerYear(pawn.def));

		public static HashSet<ThingDef> allMilks = DefDatabase<ThingDef>.AllDefs.Select(d => d.GetCompProperties<CompProperties_Milkable>()?.milkDef).Where(d => d != null).ToHashSet();
		public override IEnumerable<ThingDef> AllOptions() => allMilks;

		public static int mostMilk = DefDatabase<ThingDef>.AllDefs.Select(d => Mathf.RoundToInt(AnimalProductionUtility.MilkPerYear(d))).Max();
		public override int Max => mostMilk;
	}

	public class ThingQueryWool : ThingQueryProduct //Per Year
	{
		public override ThingDef DefFor(Pawn pawn) => pawn.def.GetCompProperties<CompProperties_Shearable>()?.woolDef;
		public override int CountFor(Pawn pawn) => Mathf.RoundToInt(AnimalProductionUtility.WoolPerYear(pawn.def));

		public static HashSet<ThingDef> allWools = DefDatabase<ThingDef>.AllDefs.Select(d => d.GetCompProperties<CompProperties_Shearable>()?.woolDef).Where(d => d != null).ToHashSet();
		public override IEnumerable<ThingDef> AllOptions() => allWools;

		public static int mostWool = DefDatabase<ThingDef>.AllDefs.Select(d => Mathf.RoundToInt(AnimalProductionUtility.WoolPerYear(d))).Max();
		public override int Max => mostWool;
	}

	//Enum values matching existing translation keys
	public enum ProgressType { Milkable, Shearable, MilkFullness, WoolGrowth, EggProgress, EggHatch }
	public class ThingQueryProductProgress : ThingQueryDropDown<ProgressType>
	{
		protected FloatRangeUB progressRange = new(0, 1);

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref progressRange.range, "progressRange");
		}
		protected override ThingQuery Clone()
		{
			ThingQueryProductProgress clone = (ThingQueryProductProgress)base.Clone();
			clone.progressRange = progressRange;
			return clone;
		}

		public float ProgressFor(Thing thing) =>
			(float)
			(sel switch
			{
				ProgressType.EggProgress => thing.TryGetComp<CompEggLayer>()?.eggProgress,
				ProgressType.EggHatch => thing.TryGetComp<CompHatcher>()?.gestateProgress,
				ProgressType.MilkFullness => thing.TryGetComp<CompMilkable>()?.Fullness,
				ProgressType.WoolGrowth => thing.TryGetComp<CompShearable>()?.Fullness,
				ProgressType.Milkable => thing.TryGetComp<CompMilkable>()?.ActiveAndFull ?? false ? 1 : 0,
				ProgressType.Shearable => thing.TryGetComp<CompShearable>()?.ActiveAndFull ?? false ? 1 : 0,
				_ => 0,
			});

		public override bool AppliesDirectlyTo(Thing thing)
		{
			if (!thing.def.HasComp(sel switch
			{
				ProgressType.EggProgress => typeof(CompEggLayer),
				ProgressType.EggHatch => typeof(CompHatcher),
				ProgressType.MilkFullness => typeof(CompMilkable),
				ProgressType.WoolGrowth => typeof(CompShearable),
				ProgressType.Milkable => typeof(CompMilkable),
				ProgressType.Shearable => typeof(CompShearable),
				_ => null
			}))
				return false;

			if (sel == ProgressType.EggProgress)
			{
				if (thing.def.GetCompProperties<CompProperties_EggLayer>().eggLayFemaleOnly && (thing as Pawn).gender != Gender.Female)
					return false;
			}
			if (sel == ProgressType.MilkFullness || sel == ProgressType.Milkable)
			{
				if (thing.def.GetCompProperties<CompProperties_Milkable>().milkFemaleOnly && (thing as Pawn).gender != Gender.Female)
					return false;
			}

			float progress = ProgressFor(thing);
			if (sel == ProgressType.Milkable || sel == ProgressType.Shearable)
				return progress == 1;
			else
				return progressRange.Includes(progress);
		}

		public override bool DrawCustom(Rect fullRect)
		{
			if (sel == ProgressType.Milkable || sel == ProgressType.Shearable)
				return false;

			return TDWidgets.FloatRangeUB(fullRect.RightHalfClamped(row.FinalX), id, ref progressRange, valueStyle: ToStringStyle.PercentZero);
		}
	}


	public class ThingQueryTrained : ThingQueryDropDown<TrainableDef>
	{
		protected IntRangeUB stepRange;

		public ThingQueryTrained() => sel = TrainableDefOf.Tameness;

		protected override void PostProcess()
		{
			stepRange.absRange = new(0, sel?.steps ?? 1);
		}
		protected override void PostChosen()
		{
			stepRange.range = new(1, sel.steps);
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref stepRange.range, "stepRange");
		}
		protected override ThingQuery Clone()
		{
			ThingQueryTrained clone = (ThingQueryTrained)base.Clone();
			clone.stepRange = stepRange;
			return clone;
		}

		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			var training = pawn.training;
			if (training == null) return false;

			return stepRange.Includes(training.GetSteps(sel));
		}

		public override bool DrawCustom(Rect fullRect)
		{
			if (sel == null) return false;

			return TDWidgets.IntRangeUB(fullRect.RightHalfClamped(row.FinalX), id, ref stepRange);
		}
	}
}

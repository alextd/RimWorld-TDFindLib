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
	public class ListFilterName : ListFilterWithOption<string>
	{
		public ListFilterName() => sel = "";

		public override bool ApplesDirectlyTo(Thing thing) =>
			//thing.Label.Contains(sel, CaseInsensitiveComparer.DefaultInvariant);	//Contains doesn't accept comparer with strings. okay.
			sel == "" || thing.Label.IndexOf(sel, StringComparison.OrdinalIgnoreCase) >= 0;

		public const string namedLabel = "Named: ";
		public static float? namedLabelWidth;
		public static float NamedLabelWidth =>
			namedLabelWidth.HasValue ? namedLabelWidth.Value :
			(namedLabelWidth = Text.CalcSize(namedLabel).x).Value;

		public override bool DrawMain(Rect rect, bool locked)
		{
			Widgets.Label(rect, namedLabel);
			rect.xMin += NamedLabelWidth;

			if (locked)
			{
				Widgets.Label(rect, '"' + sel + '"');
				return false;
			}

			if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Tab
				&& GUI.GetNameOfFocusedControl() == $"LIST_FILTER_NAME_INPUT{id}")
					Event.current.Use();

			GUI.SetNextControlName($"LIST_FILTER_NAME_INPUT{id}");
			string newStr = Widgets.TextField(rect.LeftPart(0.9f), sel);
			if (newStr != sel)
			{
				sel = newStr;
				return true;
			}
			if (Widgets.ButtonImage(rect.RightPartPixels(rect.height), TexUI.RotLeftTex))
			{
				GUI.FocusControl("");
				sel = "";
				return true;
			}
			return false;
		}

		protected override void DoFocus()
		{
			GUI.FocusControl($"LIST_FILTER_NAME_INPUT{id}");
		}
		public override bool OnCancelKeyPressed()
		{
			if (GUI.GetNameOfFocusedControl() == $"LIST_FILTER_NAME_INPUT{id}")
			{
				GUI.FocusControl("");
				return true;
			}

			return false;
		}
	}

	public enum ForbiddenType { Forbidden, Allowed, Forbiddable }
	public class ListFilterForbidden : ListFilterDropDown<ForbiddenType>
	{
		public override bool ApplesDirectlyTo(Thing thing)
		{
			bool forbiddable = thing.def.HasComp(typeof(CompForbiddable)) && thing.Spawned;
			if (!forbiddable) return false;
			bool forbidden = thing.IsForbidden(Faction.OfPlayer);
			switch (sel)
			{
				case ForbiddenType.Forbidden: return forbidden;
				case ForbiddenType.Allowed: return !forbidden;
			}
			return true;  //forbiddable
		}
	}

	public class ListFilterDesignation : ListFilterDropDown<DesignationDef>
	{
		public override bool ApplesDirectlyTo(Thing thing) =>
			sel != null ?
			(sel.targetType == TargetType.Thing ? thing.MapHeld.designationManager.DesignationOn(thing, sel) != null :
			thing.MapHeld.designationManager.DesignationAt(thing.PositionHeld, sel) != null) :
			(thing.MapHeld.designationManager.DesignationOn(thing) != null ||
			thing.MapHeld.designationManager.AllDesignationsAt(thing.PositionHeld).Count() > 0);

		public override string NullOption() => "TD.AnyOption".Translate();

		public override bool Ordered => true;
		public override IEnumerable<DesignationDef> Options() =>
			Mod.settings.OnlyAvailable ?
				Find.CurrentMap.designationManager.AllDesignations.Select(d => d.def).Distinct() :
				base.Options();

		public override string NameFor(DesignationDef o) => o.defName; // no labels on Designation def
	}

	public class ListFilterFreshness : ListFilterDropDown<RotStage>
	{
		public override bool ApplesDirectlyTo(Thing thing)
		{
			CompRottable rot = thing.TryGetComp<CompRottable>();
			return
				extraOption == 1 ? rot != null :
				extraOption == 2 ? GenTemperature.RotRateAtTemperature(thing.AmbientTemperature) is float r && r > 0 && r < 1 :
				extraOption == 3 ? GenTemperature.RotRateAtTemperature(thing.AmbientTemperature) <= 0 :
				rot?.Stage == sel;
		}

		public override string NameFor(RotStage o) => ("RotState" + o.ToString()).Translate();

		public override int ExtraOptionsCount => 3;
		public override string NameForExtra(int ex) =>
			ex == 1 ? "TD.Spoils".Translate() :
			ex == 2 ? "TD.Refrigerated".Translate() :
			"TD.Frozen".Translate();
	}

	public class ListFilterTimeToRot : ListFilter
	{
		public const int MinReasonable = 0;
		public const int MaxReasonable = GenDate.TicksPerDay * 20;

		IntRangeUB ticksRange;
		
		public ListFilterTimeToRot()
		{
			ticksRange = new IntRangeUB(MinReasonable, MaxReasonable);
			ticksRange.max = MaxReasonable / 2;
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref ticksRange.range, "ticksRange");
		}
		public override ListFilter Clone()
		{
			ListFilterTimeToRot clone = (ListFilterTimeToRot)base.Clone();
			clone.ticksRange = ticksRange;
			return clone;
		}

		public override bool ApplesDirectlyTo(Thing thing) =>
			thing.TryGetComp<CompRottable>()?.TicksUntilRotAtCurrentTemp is int t && ticksRange.Includes(t);

		public override bool DrawMain(Rect rect, bool locked)
		{
			base.DrawMain(rect, locked);
			return TDWidgets.IntRangeUB(rect.RightHalfClamped(Text.CalcSize(Label).x), id, ref ticksRange, ticks => $"{ticks * 1f / GenDate.TicksPerDay:0.0}");
		}
	}

	public class ListFilterGrowth : ListFilterFloatRange
	{
		public override bool ApplesDirectlyTo(Thing thing) =>
			thing is Plant p && sel.Includes(p.Growth);
	}

	public class ListFilterGrowthRate : ListFilterFloatRange
	{
		public override bool ApplesDirectlyTo(Thing thing) =>
			thing is Plant p && sel.Includes(p.GrowthRate);

		public override float Max => maxGrowthRate;
		public static float maxGrowthRate;
		static ListFilterGrowthRate()
		{
			float bestFertility = 0f;
			foreach (BuildableDef def in DefDatabase<BuildableDef>.AllDefs)
				bestFertility = Mathf.Max(bestFertility, def.fertility);

			float bestSensitivity = 0f;
			foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
				bestSensitivity = Mathf.Max(bestSensitivity, def.plant?.fertilitySensitivity ?? 0);

			maxGrowthRate = 1 + bestSensitivity * (bestFertility - 1);
		}
	}

	public class ListFilterPlantHarvest : ListFilterDropDown<ThingDef>
	{
		public ListFilterPlantHarvest() => extraOption = 1;

		public override bool ApplesDirectlyTo(Thing thing)
		{
			Plant plant = thing as Plant;
			if (plant == null)
				return false;

			ThingDef yield = plant.def.plant.harvestedThingDef;

			if (extraOption == 1)
				return yield != null;
			if (extraOption == 2)
				return yield == null;

			return sel == yield;
		}

		public static List<ThingDef> allHarvests;
		static ListFilterPlantHarvest()
		{
			HashSet<ThingDef> singleDefs = new();
			foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
			{
				if (def.plant?.harvestedThingDef is ThingDef harvestDef)
					singleDefs.Add(harvestDef);
			}
			allHarvests = singleDefs.OrderBy(d => d.label).ToList();
		}
		public override IEnumerable<ThingDef> Options()
		{
			if (Mod.settings.OnlyAvailable)
			{
				HashSet<ThingDef> available = new HashSet<ThingDef>();
				foreach (Map map in Find.Maps)
					foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.HarvestablePlant))
						if ((t as Plant)?.def.plant.harvestedThingDef is ThingDef harvestDef)
							available.Add(harvestDef);

				return allHarvests.Intersect(available);
			}
			return allHarvests;
		}
		public override bool Ordered => true;

		public override int ExtraOptionsCount => 2;
		public override string NameForExtra(int ex) => // or FleshTypeDef but this works
			ex == 1 ? "TD.AnyOption".Translate() :
			"None".Translate();
	}


	public class ListFilterPlantHarvestable : ListFilter
	{
		public override bool ApplesDirectlyTo(Thing thing) =>
			thing is Plant plant && plant.HarvestableNow;
	}

	public class ListFilterPlantCrop : ListFilter
	{
		public override bool ApplesDirectlyTo(Thing thing) =>
			thing is Plant plant && plant.IsCrop;
	}

	public class ListFilterPlantDies : ListFilter
	{
		public override bool ApplesDirectlyTo(Thing thing) =>
			thing is Plant plant && (plant.def.plant?.dieIfLeafless ?? false);
	}

	public class ListFilterFaction : ListFilterDropDown<FactionRelationKind>
	{
		public bool host; // compare host faction instead of thing's faction
		public ListFilterFaction() => extraOption = 1;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref host, "host");
		}
		public override ListFilter Clone()
		{
			ListFilterFaction clone = (ListFilterFaction)base.Clone();
			clone.host = host;
			return clone;
		}

		public override bool ApplesDirectlyTo(Thing thing)
		{
			Faction fac = thing.Faction;
			if (host)
			{
				if (thing is Pawn p && p.guest != null)
					fac = p.guest.HostFaction;
				else
					return false;
			}

			return
				extraOption == 1 ? fac == Faction.OfPlayer :
				extraOption == 2 ? fac == Faction.OfMechanoids :
				extraOption == 3 ? fac == Faction.OfInsects :
				extraOption == 4 ? fac != null && !fac.def.hidden :
				extraOption == 5 ? fac == null || fac.def.hidden :
				(fac != null && fac != Faction.OfPlayer && fac.PlayerRelationKind == sel);
		}

		public override string NameFor(FactionRelationKind o) => o.GetLabel();

		public override int ExtraOptionsCount => 5;
		public override string NameForExtra(int ex) => // or FleshTypeDef but this works
			ex == 1 ? "TD.Player".Translate() :
			ex == 2 ? "TD.Mechanoid".Translate() :
			ex == 3 ? "TD.Insectoid".Translate() :
			ex == 4 ? "TD.AnyOption".Translate() :
			"TD.NoFaction".Translate();

		public override bool DrawMain(Rect rect, bool locked)
		{
			//This is not DrawCustom because then the faction button would go on the left.
			bool changed = base.DrawMain(rect, locked);

			Rect hostRect = rect.LeftPart(0.6f);
			hostRect.xMin = hostRect.xMax - 60;
			if (Widgets.ButtonText(hostRect, host ? "Host Is" : "Is"))
			{
				host = !host;
				changed = true;
			}

			return changed;
		}

	}

	enum ListCategory
	{
		Person,
		Animal,
		Item,
		Building,
		Natural,
		Plant,
		Other
	}
	class ListFilterCategory : ListFilterDropDown<ListCategory>
	{
		public override bool ApplesDirectlyTo(Thing thing)
		{
			switch (sel)
			{
				case ListCategory.Person: return thing is Pawn pawn && !pawn.NonHumanlikeOrWildMan();
				case ListCategory.Animal: return thing is Pawn animal && animal.NonHumanlikeOrWildMan();
				case ListCategory.Item: return thing.def.alwaysHaulable;
				case ListCategory.Building: return thing is Building building && building.def.filthLeaving != ThingDefOf.Filth_RubbleRock;
				case ListCategory.Natural: return thing is Building natural && natural.def.filthLeaving == ThingDefOf.Filth_RubbleRock;
				case ListCategory.Plant: return thing is Plant;
				case ListCategory.Other: return !(thing is Pawn) && !(thing is Building) && !(thing is Plant) && !thing.def.alwaysHaulable;
			}
			return false;
		}
	}

	// This includes most things but not minifiable buildings.
	public class ListFilterItemCategory : ListFilterDropDown<ThingCategoryDef>
	{
		public ListFilterItemCategory() => sel = ThingCategoryDefOf.Root;

		public override bool ApplesDirectlyTo(Thing thing) =>
			thing.def.IsWithinCategory(sel);

		public override IEnumerable<ThingCategoryDef> Options() =>
			Mod.settings.OnlyAvailable ?
				base.Options().Intersect(ContentsUtility.AvailableInGame(ThingCategoryDefsOfThing)) :
				base.Options();

		public static IEnumerable<ThingCategoryDef> ThingCategoryDefsOfThing(Thing thing)
		{
			if (thing.def.thingCategories == null)
				yield break;
			foreach (var def in thing.def.thingCategories)
			{
				yield return def;
				foreach (var pDef in def.Parents)
					yield return pDef;
			}
		}

		public override string DropdownNameFor(ThingCategoryDef def) =>
			string.Concat(Enumerable.Repeat("- ", def.Parents.Count())) + base.NameFor(def);
	}

	public class ListFilterSpecialFilter : ListFilterDropDown<SpecialThingFilterDef>
	{
		public ListFilterSpecialFilter() => sel = SpecialThingFilterDefOf.AllowFresh;

		public override bool ApplesDirectlyTo(Thing thing) =>
			sel.Worker.Matches(thing);
	}

	public enum MineableType { Resource, Rock, All }
	public class ListFilterMineable : ListFilterDropDown<MineableType>
	{
		public override bool ApplesDirectlyTo(Thing thing)
		{
			switch (sel)
			{
				case MineableType.Resource: return thing.def.building?.isResourceRock ?? false;
				case MineableType.Rock: return (thing.def.building?.isNaturalRock ?? false) && (!thing.def.building?.isResourceRock ?? true);
				case MineableType.All: return thing.def.mineable;
			}
			return false;
		}
	}

	public class ListFilterHP : ListFilterFloatRange
	{
		public override bool ApplesDirectlyTo(Thing thing)
		{
			if (thing is Pawn pawn)
				return sel.Includes(pawn.health.summaryHealth.SummaryHealthPercent);

			if (thing.def.useHitPoints)
				return sel.Includes((float)thing.HitPoints / thing.MaxHitPoints);

			return false;
		}
	}

	public class ListFilterQuality : ListFilterWithOption<QualityRange>
	{
		public ListFilterQuality() => sel = QualityRange.All;

		public override bool ApplesDirectlyTo(Thing thing) =>
			thing.TryGetQuality(out QualityCategory qc) &&
			sel.Includes(qc);

		public override bool DrawMain(Rect rect, bool locked)
		{
			base.DrawMain(rect, locked);

			QualityRange newRange = sel;
			Widgets.QualityRange(rect.RightHalfClamped(Text.CalcSize(Label).x), id, ref newRange);
			if (sel != newRange)
			{
				sel = newRange;
				return true;
			}
			return false;
		}
	}

	public class ListFilterStuff : ListFilterDropDown<ThingDef>
	{
		public override bool ApplesDirectlyTo(Thing thing)
		{
			ThingDef stuff = thing is IConstructible c ? c.EntityToBuildStuff() : thing.Stuff;
			return
				extraOption == 1 ? !thing.def.MadeFromStuff :
				extraOption > 1 ? stuff?.stuffProps?.categories?.Contains(DefDatabase<StuffCategoryDef>.AllDefsListForReading[extraOption - 2]) ?? false :
				sel == null ? stuff != null :
				stuff == sel;
		}

		public override string NullOption() => "TD.AnyOption".Translate();
		private static List<ThingDef> stuffList = DefDatabase<ThingDef>.AllDefs.Where(d => d.IsStuff).ToList();
		public override IEnumerable<ThingDef> Options() =>
			Mod.settings.OnlyAvailable
				? stuffList.Intersect(ContentsUtility.AvailableInGame(t => t.Stuff))
				: stuffList;

		public override int ExtraOptionsCount => DefDatabase<StuffCategoryDef>.DefCount + 1;
		public override string NameForExtra(int ex) =>
			ex == 1 ? "TD.NotMadeFromStuff".Translate() :
			DefDatabase<StuffCategoryDef>.AllDefsListForReading[ex - 2]?.LabelCap;
	}

	public class ListFilterMissingBodyPart : ListFilterDropDown<BodyPartDef>
	{
		public override bool ApplesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			return
				extraOption == 1 ? pawn.health.hediffSet.GetMissingPartsCommonAncestors().NullOrEmpty() :
				sel == null ? !pawn.health.hediffSet.GetMissingPartsCommonAncestors().NullOrEmpty() :
				pawn.RaceProps.body.GetPartsWithDef(sel).Any(r => pawn.health.hediffSet.PartIsMissing(r));
		}

		public override string NullOption() => "TD.AnyOption".Translate();
		public override IEnumerable<BodyPartDef> Options() =>
			Mod.settings.OnlyAvailable
				? base.Options().Intersect(ContentsUtility.AvailableInGame(
					t => (t as Pawn)?.health.hediffSet.GetMissingPartsCommonAncestors().Select(h => h.Part.def) ?? Enumerable.Empty<BodyPartDef>()))
				: base.Options();

		public override string NameFor(BodyPartDef def)
		{
			string name = def.LabelCap;
			string special = def.defName; //best we got
			if (name == special)
				return name;

			return $"{name} ({special})";
		}

		public override int ExtraOptionsCount => 1;
		public override string NameForExtra(int ex) => "None".Translate();
	}


	public enum BaseAreas { Home, BuildRoof, NoRoof, SnowClear };
	public class ListFilterArea : ListFilterDropDown<Area>
	{
		public ListFilterArea()
		{
			extraOption = 1;
		}

		protected override Area ResolveRef(Map map) =>
			map.areaManager.GetLabeled(selName);

		public override bool ApplesDirectlyTo(Thing thing)
		{
			Map map = thing.MapHeld;
			IntVec3 pos = thing.PositionHeld;

			if (extraOption == 5)
				return pos.Roofed(map);

			if (extraOption == 0)
				return sel != null ? sel[pos] :
				map.areaManager.AllAreas.Any(a => a[pos]);

			switch ((BaseAreas)(extraOption - 1))
			{
				case BaseAreas.Home: return map.areaManager.Home[pos];
				case BaseAreas.BuildRoof: return map.areaManager.BuildRoof[pos];
				case BaseAreas.NoRoof: return map.areaManager.NoRoof[pos];
				case BaseAreas.SnowClear: return map.areaManager.SnowClear[pos];
			}
			return false;
		}

		public override string NullOption() => "TD.AnyOption".Translate();
		public override IEnumerable<Area> Options() => Find.CurrentMap?.areaManager.AllAreas.Where(a => a is Area_Allowed) ?? Enumerable.Empty<Area>();
		public override string NameFor(Area o) => o.Label;

		public override int ExtraOptionsCount => 5;
		public override string NameForExtra(int ex)
		{
			if (ex == 5) return "Roofed".Translate().CapitalizeFirst();
			switch ((BaseAreas)(ex - 1))
			{
				case BaseAreas.Home: return "Home".Translate();
				case BaseAreas.BuildRoof: return "BuildRoof".Translate().CapitalizeFirst();
				case BaseAreas.NoRoof: return "NoRoof".Translate().CapitalizeFirst();
				case BaseAreas.SnowClear: return "SnowClear".Translate().CapitalizeFirst();
			}
			return "???";
		}
	}

	public class ListFilterZone : ListFilterDropDown<Zone>
	{
		protected override Zone ResolveRef(Map map) =>
			map.zoneManager.AllZones.FirstOrDefault(z => z.label == selName);

		public override bool ApplesDirectlyTo(Thing thing)
		{
			IntVec3 pos = thing.PositionHeld;
			Zone zoneAtPos = thing.MapHeld.zoneManager.ZoneAt(pos);
			return
				extraOption == 1 ? zoneAtPos is Zone_Stockpile :
				extraOption == 2 ? zoneAtPos is Zone_Growing :
				sel != null ? zoneAtPos == sel :
				zoneAtPos != null;
		}

		public override string NullOption() => "TD.AnyOption".Translate();
		public override IEnumerable<Zone> Options() => Find.CurrentMap?.zoneManager.AllZones ?? Enumerable.Empty<Zone>();

		public override int ExtraOptionsCount => 2;
		public override string NameForExtra(int ex) => ex == 1 ? "TD.AnyStockpile".Translate() : "TD.AnyGrowingZone".Translate();
	}

	public class ListFilterDeterioration : ListFilter
	{
		public override bool ApplesDirectlyTo(Thing thing) =>
			SteadyEnvironmentEffects.FinalDeteriorationRate(thing) >= 0.001f;
	}

	public enum DoorOpenFilter { Open, Close, HoldOpen, BlockedOpenMomentary }
	public class ListFilterDoorOpen : ListFilterDropDown<DoorOpenFilter>
	{
		public override bool ApplesDirectlyTo(Thing thing)
		{
			Building_Door door = thing as Building_Door;
			if (door == null) return false;
			switch (sel)
			{
				case DoorOpenFilter.Open: return door.Open;
				case DoorOpenFilter.Close: return !door.Open;
				case DoorOpenFilter.HoldOpen: return door.HoldOpen;
				case DoorOpenFilter.BlockedOpenMomentary: return door.BlockedOpenMomentary;
			}
			return false;//???
		}
		public override string NameFor(DoorOpenFilter o)
		{
			switch (o)
			{
				case DoorOpenFilter.Open: return "TD.Opened".Translate();
				case DoorOpenFilter.Close: return "VentClosed".Translate();
				case DoorOpenFilter.HoldOpen: return "CommandToggleDoorHoldOpen".Translate().CapitalizeFirst();
				case DoorOpenFilter.BlockedOpenMomentary: return "TD.BlockedOpen".Translate();
			}
			return "???";
		}
	}

	public class ListFilterThingDef : ListFilterDropDown<ThingDef>
	{
		public IntRangeUB stackRange;//unknown until sel set

		public ListFilterThingDef()
		{
			sel = ThingDefOf.WoodLog;
		}
		protected override void PostProcess()
		{
			stackRange.absRange = new(1, sel?.stackLimit ?? 1);
		}
		protected override void PostChosen()
		{
			stackRange.range = new(1, sel.stackLimit);
		}

		public override void ExposeData()
		{
			base.ExposeData();
			
			if(Scribe.mode != LoadSaveMode.Saving || sel.stackLimit > 1)
				Scribe_Values.Look(ref stackRange.range, "stackRange");
		}
		public override ListFilter Clone()
		{
			ListFilterThingDef clone = (ListFilterThingDef)base.Clone();
			clone.stackRange = stackRange;
			return clone;
		}


		public override bool ApplesDirectlyTo(Thing thing) =>
			sel == thing.def &&
			(sel.stackLimit <= 1 || stackRange.Includes(thing.stackCount));


		public override bool Ordered => true;

		public override IEnumerable<ThingDef> Options() =>
			(Mod.settings.OnlyAvailable ?
				base.Options().Intersect(ContentsUtility.AvailableInGame(t => t.def)) :
				base.Options())
			.Where(def => FindDescription.ValidDef(def));

		public override string CategoryFor(ThingDef def)
		{
			if (typeof(Blueprint_Install).IsAssignableFrom(def.thingClass))
				return "(Installing)";

			if (def.IsBlueprint)
				return "(Blueprint)";

			if (def.IsFrame)
				return "(Frame)";

			if (def.FirstThingCategory?.LabelCap.ToString() is string label)
			{
				if (label == "Misc")
					return $"{label} ({def.FirstThingCategory.parent.LabelCap})";
				return label;
			}

			//catchall for unminifiable buildings.
			if (def.designationCategory?.LabelCap.ToString() is string label2)
			{
				if (label2 == "Misc")
					return $"{label2} ({ThingCategoryDefOf.Buildings.LabelCap})";
				return label2;
			}

			if (typeof(Pawn).IsAssignableFrom(def.thingClass))
				return "Living";

			if (typeof(Mineable).IsAssignableFrom(def.thingClass))
				return "Mineable";

			return "(Other)";
		}


		public override bool DrawCustom(Rect rect, WidgetRow row, Rect fullRect)
		{
			if (sel == null) return false;

			if (sel.stackLimit > 1)
				return TDWidgets.IntRangeUB(rect.RightHalfClamped(row.FinalX), id, ref stackRange);

			return false;
		}
	}


	public class ListFilterModded : ListFilterDropDown<ModContentPack>
	{
		public ListFilterModded()
		{
			sel = LoadedModManager.RunningMods.First(mod => mod.IsCoreMod);
		}


		public override bool UsesResolveName => true;
		protected override string MakeSaveName() => sel.PackageIdPlayerFacing;

		protected override ModContentPack ResolveName() =>
			LoadedModManager.RunningMods.FirstOrDefault(mod => mod.PackageIdPlayerFacing == selName);


		public override bool ApplesDirectlyTo(Thing thing) =>
			sel == thing.ContentSource;

		public override IEnumerable<ModContentPack> Options() =>
			LoadedModManager.RunningMods.Where(mod => mod.AllDefs.Any(d => d is ThingDef));

		public override string NameFor(ModContentPack o) => o.Name;
	}


	public class ListFilterOnScreen : ListFilter
	{
		public override bool ApplesDirectlyTo(Thing thing) =>
			thing.OccupiedRect().Overlaps(Find.CameraDriver.CurrentViewRect);

		public override bool CurMapOnly => true;
	}


	public class ListFilterSelectable : ListFilter
	{
		public override bool ApplesDirectlyTo(Thing thing) =>
			thing.def.selectable;
	}


	public class ListFilterStat : ListFilterDropDown<StatDef>
	{
		FloatRange valueRange;

		public ListFilterStat()
		{
			sel = StatDefOf.GeneralLaborSpeed;
		}

		protected override void PostChosen()
		{
			valueRange = new FloatRange(sel.minValue, sel.maxValue);
			lBuffer = rBuffer = null;
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref valueRange, "valueRange");
		}
		public override ListFilter Clone()
		{
			ListFilterStat clone = (ListFilterStat)base.Clone();
			clone.valueRange = valueRange;
			return clone;
		}

		public override bool ApplesDirectlyTo(Thing t) =>
			sel.Worker.ShouldShowFor(StatRequest.For(t)) &&
			valueRange.Includes(t.GetStatValue(sel, cacheStaleAfterTicks: 1));


		public override IEnumerable<StatDef> Options() =>
			base.Options().Where(d => !d.alwaysHide);


		public override string CategoryFor(StatDef def) =>
			def.category.LabelCap;


		public override string NameFor(StatDef def) =>
			def.LabelForFullStatListCap;


		public override bool DrawCustom(Rect rect, WidgetRow row, Rect fullRect)
		{
			if (sel == null) return false;

			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(fullRect.RightHalfClamped(row.FinalX),
				$"{valueRange.min.ToStringByStyle(sel.toStringStyle, sel.toStringNumberSense)} - {valueRange.max.ToStringByStyle(sel.toStringStyle, sel.toStringNumberSense)}");
			Text.Anchor = default;

			return false;
		}

		private string lBuffer, rBuffer;
		private string controlNameL, controlNameR;
		protected override bool DrawUnder(Listing_StandardIndent listing, bool locked)
		{
			if (sel == null) return false;

			if (locked) return false;

			listing.NestedIndent(Listing_Standard.DefaultIndent);
			listing.Gap(listing.verticalSpacing);

			Rect rect = listing.GetRect(Text.LineHeight);
			Rect lRect = rect.LeftPart(.45f);
			Rect rRect = rect.RightPart(.45f);

			// these wil be the names generated inside TextFieldNumeric
			controlNameL = "TextField" + lRect.y.ToString("F0") + lRect.x.ToString("F0");
			controlNameR = "TextField" + rRect.y.ToString("F0") + rRect.x.ToString("F0");

			FloatRange oldRange = valueRange;
			if (sel.toStringStyle == ToStringStyle.PercentOne || sel.toStringStyle == ToStringStyle.PercentTwo || sel.toStringStyle == ToStringStyle.PercentZero)
			{
				Widgets.TextFieldPercent(lRect, ref valueRange.min, ref lBuffer, float.MinValue, float.MaxValue);
				Widgets.TextFieldPercent(rRect, ref valueRange.max, ref rBuffer, float.MinValue, float.MaxValue);
			}
			/*			else if(sel.toStringStyle == ToStringStyle.Integer)
			{
				Widgets.TextFieldNumeric<int>(lRect, ref valueRangeI.min, ref lBuffer, float.MinValue, float.MaxValue);
				Widgets.TextFieldNumeric<int>(rRect, ref valueRangeI.max, ref rBuffer, float.MinValue, float.MaxValue);
			}*/
			else
			{
				Widgets.TextFieldNumeric<float>(lRect, ref valueRange.min, ref lBuffer, float.MinValue, float.MaxValue);
				Widgets.TextFieldNumeric<float>(rRect, ref valueRange.max, ref rBuffer, float.MinValue, float.MaxValue);
			}


			if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Tab
				 && (GUI.GetNameOfFocusedControl() == controlNameL || GUI.GetNameOfFocusedControl() == controlNameR))
					Event.current.Use();



			listing.NestedOutdent();

			return valueRange != oldRange;
		}

		protected override void DoFocus()
		{
			GUI.FocusControl(controlNameL);
		}

		public override bool OnCancelKeyPressed()
		{
			if (GUI.GetNameOfFocusedControl() == controlNameL || GUI.GetNameOfFocusedControl() == controlNameR)
			{
				GUI.FocusControl("");
				return true;
			}

			return false;
		}
	}

	public class ListFilterBuildingCategory : ListFilterDropDown<DesignationCategoryDef>
	{
		public ListFilterBuildingCategory() => sel = DesignationCategoryDefOf.Production;

		public override bool ApplesDirectlyTo(Thing thing) =>
			sel == thing.def.designationCategory;

		public override IEnumerable<DesignationCategoryDef> Options() =>
			base.Options().Where(desCatDef => desCatDef.AllResolvedDesignators.Any(d => d is Designator_Build));
	}
}

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
	public enum QueryPawnProperty
	{
		IsColonist
	, IsFreeColonist
	, IsPrisonerOfColony
	, IsSlaveOfColony
	, IsPrisoner
	, IsSlave
	, IsColonyMech
	, Downed
	, Dead
	, HasPsylink
	}
	public class ThingQueryBasicProperty : ThingQueryDropDown<QueryPawnProperty>
	{
		public override bool AppliesDirectlyTo(Thing thing) =>
			thing is Pawn pawn ? sel switch
			{
				QueryPawnProperty.IsColonist => pawn.IsColonist
			, QueryPawnProperty.IsFreeColonist => pawn.IsFreeColonist
			, QueryPawnProperty.IsPrisonerOfColony => pawn.IsPrisonerOfColony
			, QueryPawnProperty.IsSlaveOfColony => pawn.IsSlaveOfColony
			, QueryPawnProperty.IsPrisoner => pawn.IsPrisoner
			, QueryPawnProperty.IsSlave => pawn.IsSlave
			, QueryPawnProperty.IsColonyMech => pawn.IsColonyMech
			, QueryPawnProperty.Downed => pawn.Downed
			, QueryPawnProperty.Dead => pawn.Dead
			, QueryPawnProperty.HasPsylink => pawn.HasPsylink
			,	_ => false
			} : false;
	}

	public class ThingQuerySkill : ThingQueryDropDown<SkillDef>
	{
		IntRangeUB skillRange = new IntRangeUB(SkillRecord.MinLevel, SkillRecord.MaxLevel);
		int passion = 4;

		static string[] passionText = new string[]
		{ "PassionNone", "PassionMinor", "PassionMajor", "TD.EitherOption", "TD.AnyOption" };//notranslate
		public static string GetPassionText(int x) => passionText[x].Translate().ToString().Split(' ')[0];

		public ThingQuerySkill()
		{
			sel = SkillDefOf.Animals;
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref skillRange.range, "skillRange");
			Scribe_Values.Look(ref passion, "passion");
		}
		public override ThingQuery Clone()
		{
			ThingQuerySkill clone = (ThingQuerySkill)base.Clone();
			clone.skillRange = skillRange;
			clone.passion = passion;
			return clone;
		}

		public override string NullOption() => "TD.AnyOption".Translate();

		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn_SkillTracker skills = (thing as Pawn)?.skills;
			if (skills == null) return false;

			return sel == null ? skills.skills.Any(AppliesRecord) :
				skills.GetSkill(sel) is SkillRecord rec && AppliesRecord(rec);
		}

		private bool AppliesRecord(SkillRecord rec) =>
			!rec.TotallyDisabled &&
			skillRange.Includes(rec.Level) &&
			(passion == 4 ? true :
			 passion == 3 ? rec.passion != Passion.None :
			 (int)rec.passion == passion);


		public override bool DrawCustom(Rect rect, WidgetRow row, Rect fullRect)
		{
			if (row.ButtonText(GetPassionText(passion)))
			{
				DoFloatOptions(Enumerable.Range(0, 5).Select(
					p => new FloatMenuOptionAndRefresh(GetPassionText(p), () => passion = p, this)).Cast<FloatMenuOption>().ToList());
			}

			return TDWidgets.IntRangeUB(fullRect.RightHalfClamped(row.FinalX), id, ref skillRange);
		}
	}

	public class ThingQueryTrait : ThingQueryDropDown<TraitDef>
	{
		int traitDegree;

		public ThingQueryTrait()
		{
			sel = TraitDefOf.Beauty;  //Todo: beauty shows even if it's not on map
		}
		protected override void PostChosen()
		{
			traitDegree = sel.degreeDatas.First().degree;
		}

		public override string NameFor(TraitDef def) =>
			def.degreeDatas.Count == 1
				? def.degreeDatas.First().label.CapitalizeFirst()
				: def.defName + "*";//TraitDefs don't have labels

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref traitDegree, "traitDegree");
		}
		public override ThingQuery Clone()
		{
			ThingQueryTrait clone = (ThingQueryTrait)base.Clone();
			clone.traitDegree = traitDegree;
			return clone;
		}

		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			return pawn.story?.traits.GetTrait(sel) is Trait trait &&
				trait.Degree == traitDegree;
		}

		public override IEnumerable<TraitDef> Options() =>
			Mod.settings.OnlyAvailable
				? ContentsUtility.AvailableInGame(t => (t as Pawn)?.story?.traits.allTraits.Select(tr => tr.def) ?? Enumerable.Empty<TraitDef>())
				: base.Options();

		public override bool Ordered => true;

		public override bool DrawCustom(Rect rect, WidgetRow row, Rect fullRect)
		{
			if (sel == null) return false;

			if (sel.degreeDatas.Count > 1 &&
				row.ButtonTextNoGap(sel.DataAtDegree(traitDegree).label.CapitalizeFirst()))
			{
				List<FloatMenuOption> options = new List<FloatMenuOption>();
				foreach (TraitDegreeData deg in sel.degreeDatas)
				{
					options.Add(new FloatMenuOptionAndRefresh(deg.label.CapitalizeFirst(), () => traitDegree = deg.degree, this));
				}
				DoFloatOptions(options);
			}
			return false;
		}
	}

	public class ThingQueryThought: ThingQueryDropDown<ThoughtDef>
	{
		IntRange stageRange;	//Indexes of orderedStages
		List<int> orderedStages = new();

		// There is a confusing translation between stage index and ordered index.
		// The xml defines stages inconsistently so we order them to orderedStages
		// The selection is done with the ordered index
		// But of course this has to be translated from and to the actual stage index

		public ThingQueryThought()
		{
			sel = ThoughtDefOf.AteWithoutTable;
		}


		// stageI = orderedStages[orderI], so
		// gotta reverse index search to find orderI from stageI
		private int OrderedIndex(int stageI) =>
			orderedStages.IndexOf(stageI);

		private bool Includes(int stageI) =>
			stageRange.Includes(OrderedIndex(stageI));


		// Multistage UI is only shown when when there's >1 stage
		// Some hidden stages are not shown (unless you have godmode on)
		public static bool VisibleStage(ThoughtStage stage) =>
			DebugSettings.godMode || (stage?.visible ?? false);

		public static bool ShowMultistage(ThoughtDef def) =>
			def.stages.Count(VisibleStage) > 1;

		public IEnumerable<int> SelectableStages =>
			orderedStages.Where(i => VisibleStage(sel.stages[i]));

		protected override void PostProcess()
		{
			orderedStages.Clear();
			if (sel == null) return;

			orderedStages.AddRange(Enumerable.Range(0, sel.stages.Count)
				.OrderBy(i => sel.stages[i] == null ? i : i + 1000 * sel.stages[i].baseOpinionOffset + 1000000 * sel.stages[i].baseMoodEffect));
		}

		protected override void PostChosen()
		{
			stageRange = new IntRange(0, SelectableStages.Count() - 1);
		}

		public override void ExposeData()
		{
			base.ExposeData();

			Scribe_Values.Look(ref stageRange, "stageRange");
		}
		public override ThingQuery Clone()
		{
			ThingQueryThought clone = (ThingQueryThought)base.Clone();
			clone.stageRange = stageRange;
			clone.PostProcess();
			return clone;
		}

		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			if (pawn.needs?.TryGetNeed<Need_Mood>() is Need_Mood mood)
			{
				//memories
				if (mood.thoughts.memories.Memories.Any(t => t.def == sel && Includes(t.CurStageIndex)))
					return true;

				//situational
				List<Thought> thoughts = new List<Thought>();
				mood.thoughts.situational.AppendMoodThoughts(thoughts);
				if (thoughts.Any(t => t.def == sel && Includes(t.CurStageIndex)))
					return true;
			}
			return false;
		}

		public override string NameFor(ThoughtDef def)
		{
			string label =
				def.label?.CapitalizeFirst() ??
				def.stages.FirstOrDefault(d => d?.label != null).label.CapitalizeFirst() ??
				def.stages.FirstOrDefault(d => d?.labelSocial != null).labelSocial.CapitalizeFirst() ?? "???";

			label = label.Replace("{0}", "_").Replace("{1}", "_");

			return ShowMultistage(def) ? label + "*" : label;
		}

		public override IEnumerable<ThoughtDef> Options() =>
			Mod.settings.OnlyAvailable
				? ContentsUtility.AvailableInGame(ThoughtsForThing)
				: base.Options();

		public override bool Ordered => true;

		private string NameForStage(int stageI)
		{
			ThoughtStage stage = sel.stages[stageI];
			if (stage == null || !stage.visible)
				return "TD.Invisible".Translate();

			StringBuilder str = new(stage.label.CapitalizeFirst().Replace("{0}", "_").Replace("{1}", "_"));

			if (stage.baseMoodEffect != 0)
				str.Append($" : ({stage.baseMoodEffect})");

			if (stage.baseOpinionOffset != 0)
				str.Append($" : ({stage.baseOpinionOffset})");

			return str.ToString();
		}

		private string TipForStage(int stageI) =>
			sel.stages[stageI]?.description;

		protected override bool DrawUnder(Listing_StandardIndent listing, bool locked)
		{
			if (sel == null) return false;

			if (!ShowMultistage(sel)) return false;

			//Buttons apparently are too tall for the line height?
			listing.Gap(listing.verticalSpacing);

			listing.NestedIndent();
			Rect nextRect = listing.GetRect(Text.LineHeight);
			listing.NestedOutdent();

			WidgetRow row = new WidgetRow(nextRect.x, nextRect.y);
			
			row.Label("TD.From".Translate());
			DoStageDropdown(row, stageRange.min, i => stageRange.min = i);

			row.Label("RangeTo".Translate());
			DoStageDropdown(row, stageRange.max, i => stageRange.max = i);
			
			return false;
		}

		private void DoStageDropdown(WidgetRow row, int setI, Action<int> selectedAction)
		{
			int setStageI = orderedStages[setI];
			if (row.ButtonTextNoGap(NameForStage(setStageI), TipForStage(setStageI)))
			{
				List<FloatMenuOption> options = new List<FloatMenuOption>();
				foreach (int stageI in SelectableStages)
				{
					int localI = OrderedIndex(stageI);
					options.Add(new FloatMenuOptionAndRefresh(NameForStage(stageI), () => selectedAction(localI), this));
				}

				DoFloatOptions(options);
			}
		}

		public static IEnumerable<ThoughtDef> ThoughtsForThing(Thing t)
		{
			Pawn pawn = t as Pawn;
			if (pawn == null) yield break;

			IEnumerable<ThoughtDef> memories = pawn.needs?.TryGetNeed<Need_Mood>()?.thoughts.memories.Memories.Where(th => VisibleStage(th.CurStage)).Select(th => th.def);
			if (memories != null)
				foreach (ThoughtDef def in memories)
					yield return def;

			List<Thought> thoughts = new List<Thought>();
			pawn.needs?.TryGetNeed<Need_Mood>()?.thoughts.situational.AppendMoodThoughts(thoughts);
			foreach (Thought thought in thoughts.Where(th => VisibleStage(th.CurStage)))
				yield return thought.def;
		}
	}

	public class ThingQueryNeed : ThingQueryDropDown<NeedDef>
	{
		FloatRangeUB needRange = new FloatRangeUB(0, 1, 0, 0.5f);

		public ThingQueryNeed() => sel = NeedDefOf.Food;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref needRange.range, "needRange");
		}
		public override ThingQuery Clone()
		{
			ThingQueryNeed clone = (ThingQueryNeed)base.Clone();
			clone.needRange = needRange;
			return clone;
		}

		public override bool AppliesDirectlyTo(Thing thing) =>
			thing is Pawn pawn &&
			(!pawn.RaceProps.Animal || pawn.Faction != null || DebugSettings.godMode) &&
				pawn.needs?.TryGetNeed(sel) is Need need && needRange.Includes(need.CurLevelPercentage);

		public override bool DrawCustom(Rect rect, WidgetRow row, Rect fullRect)
		{
			return TDWidgets.FloatRangeUB(fullRect.RightHalfClamped(row.FinalX), id, ref needRange, valueStyle: ToStringStyle.PercentOne);
		}
	}

	public class ThingQueryHealth : ThingQueryDropDown<HediffDef>
	{
		FloatRangeUB severityRange;//unknown until sel set
		bool usesSeverity;

		public ThingQueryHealth()
		{
			sel = null;
		}
		protected override void PostProcess()
		{
			FloatRange? r = SeverityRangeFor(sel);
			usesSeverity = r.HasValue;
			if (usesSeverity)
				severityRange.absRange = r.Value;
		}
		protected override void PostChosen()
		{
			if (SeverityRangeFor(sel) is FloatRange range)
				severityRange.range = range;
		}

		public override void ExposeData()
		{
			base.ExposeData();

			Scribe_Values.Look(ref severityRange.range, "severityRange");
		}
		public override ThingQuery Clone()
		{
			ThingQueryHealth clone = (ThingQueryHealth)base.Clone();
			clone.severityRange = severityRange;
			clone.usesSeverity = usesSeverity;
			return clone;
		}

		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			return
				extraOption == 1 ? pawn.health.hediffSet.hediffs.Any(h => h.Visible || DebugSettings.godMode) :
				sel == null ? !pawn.health.hediffSet.hediffs.Any(h => h.Visible || DebugSettings.godMode) :
				(pawn.health.hediffSet.GetFirstHediffOfDef(sel, !DebugSettings.godMode) is Hediff hediff &&
				(!usesSeverity || severityRange.Includes(hediff.Severity)));
		}

		public override string NullOption() => "None".Translate();
		public override IEnumerable<HediffDef> Options() =>
			Mod.settings.OnlyAvailable
				? ContentsUtility.AvailableInGame(t => (t as Pawn)?.health.hediffSet.hediffs.Select(h => h.def) ?? Enumerable.Empty<HediffDef>())
				: base.Options();

		public override bool Ordered => true;

		public override int ExtraOptionsCount => 1;
		public override string NameForExtra(int ex) => "TD.AnyOption".Translate();

		public override bool DrawCustom(Rect rect, WidgetRow row, Rect fullRect)
		{
			if (sel != null && usesSeverity)
				return TDWidgets.FloatRangeUB(fullRect.RightHalfClamped(row.FinalX), id, ref severityRange, valueStyle: ToStringStyle.FloatOne);

			return false;
		}

		public static FloatRange? SeverityRangeFor(HediffDef hediffDef)
		{
			if (hediffDef == null) return null;

			float min = hediffDef.minSeverity;
			float max = hediffDef.maxSeverity;
			if (hediffDef.lethalSeverity != -1f)
				max = Math.Min(max, hediffDef.lethalSeverity);

			if (max == float.MaxValue) return null;
			return new FloatRange(min, max);
		}
	}

	public class ThingQueryIncapable : ThingQueryDropDown<WorkTags>
	{
		public override string NameFor(WorkTags tags) =>
			tags.LabelTranslated().CapitalizeFirst();

		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			return 
				extraOption == 1 ? pawn.CombinedDisabledWorkTags != WorkTags.None :
				sel == WorkTags.None ? pawn.CombinedDisabledWorkTags == WorkTags.None :
				pawn.WorkTagIsDisabled(sel);
		}

		public override int ExtraOptionsCount => 1;
		public override string NameForExtra(int ex) => "TD.AnyOption".Translate();
	}

	public class ThingQueryTemp : ThingQueryFloatRange
	{
		public ThingQueryTemp() => _sel.range = new FloatRange(10, 30);

		public override float Min => -100f;
		public override float Max => 100f;
		public override ToStringStyle Style => ToStringStyle.Temperature;

		public override bool AppliesDirectlyTo(Thing thing) =>
			sel.Includes(thing.AmbientTemperature);
	}

	public enum ComfyTemp { Cold, Cool, Okay, Warm, Hot }
	public class ThingQueryComfyTemp : ThingQueryDropDown<ComfyTemp>
	{
		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;
			float temp = pawn.AmbientTemperature;
			FloatRange safeRange = pawn.SafeTemperatureRange();
			FloatRange comfRange = pawn.ComfortableTemperatureRange();
			switch (sel)
			{
				case ComfyTemp.Cold: return temp < safeRange.min;
				case ComfyTemp.Cool: return temp >= safeRange.min && temp < comfRange.min;
				case ComfyTemp.Okay: return comfRange.Includes(temp);
				case ComfyTemp.Warm: return temp <= safeRange.max && temp > comfRange.max;
				case ComfyTemp.Hot: return temp > safeRange.max;
			}
			return false;//???
		}
		public override string NameFor(ComfyTemp o)
		{
			switch (o)
			{
				case ComfyTemp.Cold: return "TD.Cold".Translate();
				case ComfyTemp.Cool: return "TD.ALittleCold".Translate();
				case ComfyTemp.Okay: return "TD.Comfortable".Translate();
				case ComfyTemp.Warm: return "TD.ALittleHot".Translate();
				case ComfyTemp.Hot: return "TD.Hot".Translate();
			}
			return "???";
		}
	}

	public class ThingQueryRestricted : ThingQueryDropDown<Area>
	{
		protected override Area ResolveRef(Map map) =>
			map.areaManager.GetLabeled(selName);

		public override bool AppliesDirectlyTo(Thing thing)
		{
			Area selectedArea = extraOption == 1 ? thing.MapHeld.areaManager.Home : sel;
			return thing is Pawn pawn && pawn.playerSettings is Pawn_PlayerSettings set && set.AreaRestriction == selectedArea;
		}

		public override string NullOption() => "NoAreaAllowed".Translate();
		public override IEnumerable<Area> Options() => Find.CurrentMap?.areaManager.AllAreas.Where(a => a is Area_Allowed) ?? Enumerable.Empty<Area>();//a.AssignableAsAllowed());
		public override string NameFor(Area o) => o.Label;

		public override int ExtraOptionsCount => 1;
		public override string NameForExtra(int ex) => "Home".Translate();
	}

	public class ThingQueryMentalState : ThingQueryDropDown<MentalStateDef>
	{
		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			return
				extraOption == 1 ? pawn.MentalState != null: 
				extraOption == 2 ? pawn.MentalState?.def is MentalStateDef def && def.IsAggro : 
				sel == null ? pawn.MentalState == null : 
				pawn.MentalState?.def is MentalStateDef sDef && sDef == sel;
		}

		public override IEnumerable<MentalStateDef> Options() =>
			Mod.settings.OnlyAvailable
				? ContentsUtility.AvailableInGame(t => (t as Pawn)?.MentalState?.def)
				: base.Options();

		public override bool Ordered => true;

		public override string NullOption() => "None".Translate();

		public override int ExtraOptionsCount => 2;
		public override string NameForExtra(int ex) =>
			ex == 1 ? "TD.AnyOption".Translate() : "TD.AnyAggresive".Translate();
	}

	public class ThingQueryPrisoner : ThingQueryDropDown<PrisonerInteractionModeDef>
	{
		public ThingQueryPrisoner()
		{
			sel = PrisonerInteractionModeDefOf.NoInteraction;
		}

		public override bool AppliesDirectlyTo(Thing thing)
		{
			if (extraOption == 2)
				return thing.GetRoom()?.IsPrisonCell ?? false;

			Pawn pawn = thing as Pawn;
			if (pawn == null)
				return false;

			if (extraOption == 1)
				return pawn.IsPrisoner;

			return pawn.IsPrisoner && pawn.guest?.interactionMode == sel;
		}
		
		public override int ExtraOptionsCount => 2;
		public override string NameForExtra(int ex) =>
			ex == 1 ? "TD.IsPrisoner".Translate() : "TD.InCell".Translate();
	}

	// the option here is default true = "Lodger", true= "Helper"
	public class ThingQueryQuest : ThingQueryWithOption<bool>
	{
		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null)
				return false;

			return sel ? pawn.IsQuestHelper() : pawn.IsQuestLodger();
		}

		public override bool DrawMain(Rect rect, bool locked, Rect fullRect)
		{
			base.DrawMain(rect, locked, fullRect);
			Rect buttRect = fullRect.RightPartClamped(0.4f, Text.CalcSize(Label).x);
			string label = sel ? "TD.IsHelper".Translate() : "TD.IsLodger".Translate();
			if (Widgets.ButtonText(buttRect, label))
			{
				sel = !sel;
				return true;
			}
			return false;
		}
	}

	public enum DraftQuery { Drafted, Undrafted, Controllable }
	public class ThingQueryDrafted : ThingQueryDropDown<DraftQuery>
	{
		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			switch (sel)
			{
				case DraftQuery.Drafted: return pawn.Drafted;
				case DraftQuery.Undrafted: return pawn.drafter != null && !pawn.Drafted;
				case DraftQuery.Controllable: return pawn.drafter != null;
			}
			return false;
		}
	}

	public class ThingQueryJob : ThingQueryDropDown<JobDef>
	{
		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			return pawn.CurJobDef == sel;
		}

		public override string NameFor(JobDef o) =>
			Regex.Replace(o.reportString.Replace(".",""), "Target(A|B|C)", "...");

		public override string NullOption() => "None".Translate();

		public override IEnumerable<JobDef> Options() =>
			Mod.settings.OnlyAvailable
				? ContentsUtility.AvailableInGame(t => (t as Pawn)?.CurJobDef)
			: base.Options();
		public override bool Ordered => true;
	}

	public class ThingQueryGuestStatus : ThingQueryDropDown<GuestStatus>
	{
		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			if (pawn.GuestStatus is GuestStatus status)
			{
				if (extraOption == 1) return status == GuestStatus.Prisoner && (pawn.guest?.PrisonerIsSecure ?? false);
				if (extraOption == 2) return status == GuestStatus.Slave && (pawn.guest?.SlaveIsSecure ?? false);
				return status == sel;
			}
			return false;
		}

		public override int ExtraOptionsCount => 2;
		public override string NameForExtra(int ex) =>
			ex == 1 ? "TD.SecurePrisoner".Translate() : "TD.SecureSlave".Translate();
	}

	public enum RacePropsQuery { Predator, Prey, Herd, Pack, Wildness, Petness, Trainability, Intelligence }
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
		public override ThingQuery Clone()
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
					return valueRange.Includes(props.wildness);
				case RacePropsQuery.Petness: 
					return valueRange.Includes(props.petness);
				case RacePropsQuery.Trainability:
					return props.trainability == trainability;
			}
			return false;
		}

		public override bool DrawCustom(Rect rect, WidgetRow row, Rect fullRect)
		{
			List<FloatMenuOption> options = new List<FloatMenuOption>();
			switch (sel)
			{
				case RacePropsQuery.Intelligence:
					if (row.ButtonTextNoGap(intelligence.TranslateEnum()))
					{
						foreach (Intelligence intel in Enum.GetValues(typeof(Intelligence)))
						{
							options.Add(new FloatMenuOptionAndRefresh(intel.TranslateEnum(), () => intelligence = intel, this));
						}
						DoFloatOptions(options);
					}
					break;

				case RacePropsQuery.Wildness:
				case RacePropsQuery.Petness:
					return TDWidgets.FloatRangeUB(fullRect.RightHalfClamped(row.FinalX), id, ref valueRange, valueStyle: ToStringStyle.PercentZero);

				case RacePropsQuery.Trainability:
					if (row.ButtonTextNoGap(trainability.LabelCap))
					{
						foreach (TrainabilityDef def in DefDatabase<TrainabilityDef>.AllDefs)
						{
							options.Add(new FloatMenuOptionAndRefresh(def.LabelCap, () => trainability = def, this));
						}
						DoFloatOptions(options);
					}
					break;
			}
			return false;
		}
	}

	public class ThingQueryGender : ThingQueryDropDown<Gender>
	{
		public ThingQueryGender() => sel = Gender.Male;

		public override bool AppliesDirectlyTo(Thing thing) =>
			thing is Pawn pawn && pawn.gender == sel;
	}

	public class ThingQueryDevelopmentalStage : ThingQueryDropDown<DevelopmentalStage>
	{
		public ThingQueryDevelopmentalStage() => sel = DevelopmentalStage.Adult;

		public override bool AppliesDirectlyTo(Thing thing) =>
			thing is Pawn pawn && pawn.DevelopmentalStage == sel;
	}

	// -------------------------
	// Animal Details
	// -------------------------


	abstract public class ThingQueryProduct : ThingQueryDropDown<ThingDef>
	{
		protected IntRangeUB countRange;

		public ThingQueryProduct()
		{
			extraOption = 1;
			countRange = new IntRangeUB(0, Max);	//Not PostChosen as this depends on subclass, not selection
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref countRange.range, "countRange");
		}
		public override ThingQuery Clone()
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

			if (extraOption == 0 && sel == null)
				return productDef == null;

			if(extraOption == 1 ? productDef != null : sel == productDef)
				return countRange.Includes(CountFor(pawn));

			return false;
		}

		public abstract int Max { get;  }
		public override bool DrawCustom(Rect rect, WidgetRow row, Rect fullRect)
		{
			//TODO: write 'IsNull' method to handle confusing extraOption == 1 but Sel == null
			if (extraOption == 0 && sel == null) return false;

			return TDWidgets.IntRangeUB(fullRect.RightHalfClamped(row.FinalX), id, ref countRange);
		}

		public abstract IEnumerable<ThingDef> AllOptions();
		public override IEnumerable<ThingDef> Options()
		{
			if (Mod.settings.OnlyAvailable)
			{
				HashSet<ThingDef> ret = new HashSet<ThingDef>();
				foreach (Map map in Find.Maps)
					foreach (Pawn p in map.mapPawns.AllPawns)
						if (DefFor(p) is ThingDef def)
							ret.Add(def);

				return ret;
			}
			return AllOptions();
		}
		public override bool Ordered => true;

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
	public enum ProgressType { Milkable, Shearable, MilkFullness, WoolGrowth, EggProgress, EggHatch}
	public class ThingQueryProductProgress : ThingQueryDropDown<ProgressType>
	{
		protected FloatRangeUB progressRange = new FloatRangeUB(0, 1);

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref progressRange.range, "progressRange");
		}
		public override ThingQuery Clone()
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

		public override bool DrawCustom(Rect rect, WidgetRow row, Rect fullRect)
		{
			if (sel == ProgressType.Milkable || sel == ProgressType.Shearable)
				return false;

			return TDWidgets.FloatRangeUB(fullRect.RightHalfClamped(row.FinalX), id, ref progressRange, valueStyle: ToStringStyle.PercentZero);
		}
	}


	public class ThingQueryInspiration : ThingQueryDropDown<InspirationDef>
	{
		public override bool AppliesDirectlyTo(Thing thing) =>
			thing is Pawn p && 
			(extraOption == 1 ?
				p.InspirationDef != null :
				sel == p.InspirationDef);

		public override string NullOption() => "None".Translate();

		public override int ExtraOptionsCount => 1;
		public override string NameForExtra(int ex) => "TD.AnyOption".Translate();
	}


	public class ThingQueryCapacity : ThingQueryDropDown<PawnCapacityDef>
	{
		public const float MaxReasonable = 4;
		FloatRangeUB capacityRange = new FloatRangeUB(0, MaxReasonable, 1, 1);

		public ThingQueryCapacity()
		{
			sel = PawnCapacityDefOf.Manipulation;
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref capacityRange.range, "capacityRange");
		}
		public override ThingQuery Clone()
		{
			ThingQueryCapacity clone = (ThingQueryCapacity)base.Clone();
			clone.capacityRange = capacityRange;
			return clone;
		}

		public override string NullOption() => "TD.AnyOption".Translate();

		private bool Includes(Pawn pawn, PawnCapacityDef def) =>
			capacityRange.Includes(pawn.health.capacities.GetLevel(def));

		public override bool AppliesDirectlyTo(Thing thing)
		{
			if (thing is Pawn pawn)
			{
				if(sel != null)
					return Includes(pawn, sel);

				foreach (PawnCapacityDef def in DefDatabase<PawnCapacityDef>.AllDefs)
					if (Includes(pawn, def))
						return true;
			}
			return false;
		}

		public override bool DrawCustom(Rect rect, WidgetRow row, Rect fullRect)
		{
			if(TDWidgets.FloatRangeUB(fullRect.RightHalfClamped(row.FinalX), id, ref capacityRange, valueStyle: ToStringStyle.PercentZero))
			{
				//round down to 1%
				capacityRange.min = (int)(100 * capacityRange.min) / 100f;
				capacityRange.max = (int)(100 * capacityRange.max) / 100f;
				return true;
			}
			return false;
		}
	}

}
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
		public override int ExtraOptionsCount => 1;
		public override string NameForExtra(int ex) => "TD.AnyOption".Translate();

		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn_SkillTracker skills = (thing as Pawn)?.skills;
			if (skills == null) return false;

			return extraOption == 1 ? skills.skills.Any(AppliesRecord) :
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
				extraOption == 2 ? pawn.health.hediffSet.hediffs.Any(h => (h.Visible || DebugSettings.godMode) && h.Bleeding) :
				extraOption == 3 ? pawn.health.hediffSet.hediffs.Any(h => (h.Visible || DebugSettings.godMode) && h.TendableNow()) :
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

		public override int ExtraOptionsCount => 3;
		public override string NameForExtra(int ex) =>
			ex switch
			{
				1 => "TD.AnyOption".Translate(),
				2 => "Any bleeding",
				_ => "CanTendNow".Translate()
			};

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
			if (pawn == null) return false;

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
			if (pawn == null) return false;

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

		public override int ExtraOptionsCount => 1;
		public override string NameForExtra(int ex) => "TD.AnyOption".Translate();

		private bool Includes(Pawn pawn, PawnCapacityDef def) =>
			capacityRange.Includes(pawn.health.capacities.GetLevel(def));

		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			if(extraOption == 1)
				foreach (PawnCapacityDef def in DefDatabase<PawnCapacityDef>.AllDefs)
					if (Includes(pawn, def))
						return true;

			return Includes(pawn, sel);
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
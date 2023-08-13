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
	public enum BandwidthFilterType { Total, Used, Available }
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
		public string xenotypeName;

		public ThingQueryXenotype() => sel = XenotypeDefOf.Baseliner;

		public override int extraOption {
			set
			{
				base.extraOption = value;
				if (value == 4)
					xenotypeName = CustomXenotypes().FirstOrFallback("???");
			}
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref xenotypeName, "xenotypeName");
		}
		protected override ThingQuery Clone()
		{
			ThingQueryXenotype clone = (ThingQueryXenotype)base.Clone();
			clone.xenotypeName = xenotypeName;
			return clone;
		}

		public override bool AppliesDirectlyTo(Thing thing)
		{
			switch (extraOption)
			{
				case 1: // Any Common
					{
						if (thing is Pawn pawn)
							return pawn.genes != null && pawn.genes.xenotypeName == null;
					}
					break;
				case 2: // Unique
					{
						if (thing is Pawn pawn)
							return pawn.genes?.UniqueXenotype ?? false;
						//if (thing is HumanEmbryo emb)
						//	return emb.GeneSet.xenp // Hey the xenotype is not set yet.
					}
					break;
				case 3: // Hybrid
					{
						if (thing is Pawn pawn)
							return pawn.genes?.hybrid ?? false;
					}
					break;
				case 4: // Named Custom
					{
						if (thing is Pawn pawn)
							return pawn.genes?.xenotypeName == xenotypeName;
						if (thing is Building_GeneAssembler ass)
							return ass.xenotypeName == xenotypeName;
						if (thing is Xenogerm xenog)
							return xenog.xenotypeName == xenotypeName;
					}
					break;
				default: // By Def
					{
						if (thing is Pawn pawn)
							return pawn.genes?.xenotype == sel;
					}
					break;
			}

			return false;
		}

		public override int ExtraOptionsCount => 4;
		public override string NameForExtra(int ex) =>
			ex switch
			{
				1 => "Any Common",
				2 => "Unique".Translate().CapitalizeFirst(),
				3 => "Hybrid".Translate().CapitalizeFirst(),
				_ => "Custom".Translate().CapitalizeFirst()
			};

		public static IEnumerable<string> CustomXenotypes()
		{
			foreach (string xenot in ContentsUtility.AvailableInGame<string>(
				t => (t as Pawn)?.genes?.xenotypeName
				?? (t as Building_GeneAssembler)?.xenotypeName
				?? (t as Xenogerm)?.xenotypeName))
			{
				if (xenot != "")
					yield return xenot;
			}

			if (Current.Game?.customXenogermDatabase != null)
				foreach (var custo in Current.Game.customXenogermDatabase.CustomXenogermsForReading)
					yield return custo.name;
		}

		public override bool DrawMain(Rect rect, bool locked, Rect fullRect)
		{
			bool changed = base.DrawMain(rect, locked, fullRect);

			if (extraOption != 4) return false;

			if (row.ButtonText(xenotypeName))
			{
				floatOptions.Clear();

				foreach (string xenoOption in CustomXenotypes().Distinct())
					floatOptions.Add(new FloatMenuOptionAndRefresh(xenoOption, () => xenotypeName = xenoOption, this));

				DoFloatOptions(floatOptions);
			}

			return changed;// it'll be false but who knows
		}
	}

	// public class ThingQueryXenogerm // Is this needed? Is this just "Unique"?


	public class ThingQueryGene : ThingQueryCategorizedDropdown<GeneDef, GeneCategoryDef, ThingQueryGene, ThingQueryGene.ThingQueryGeneCategory>
	{
		public class ThingQueryGeneCategory : ThingQueryCategorizedDropdownHelper<GeneDef, GeneCategoryDef, ThingQueryGene, ThingQueryGeneCategory>
		{
			private static Dictionary<GeneCategoryDef, List<GeneDef>> categoryGenes;
			static ThingQueryGeneCategory()
			{
				categoryGenes = new();

				foreach (var val in DefDatabase<GeneDef>.AllDefsListForReading)
				{
					var key = val.displayCategory;
					if (!categoryGenes.TryGetValue(key, out List<GeneDef> genes))
					{
						genes = new();
						categoryGenes[key] = genes;
					}
					genes.Add(val);
				}
			}

			public override bool AppliesDirectlyTo(Thing thing)
			{
				foreach (GeneDef geneDef in categoryGenes[sel])
				{
					ParentQuery._sel = geneDef;
					if (ParentQuery.AppliesDirectly2(thing))
						return true;
				}

				return false;
			}
		}


		public override GeneCategoryDef CategoryFor(GeneDef def) => def.displayCategory;

		public enum GeneType { Either, Endogene, Xenogene }
		public GeneType geneType;
		public enum GeneHavingType { Has, Active, Inactive }
		public GeneHavingType haveType;


		public ThingQueryGene() => sel = GeneDefOf.Inbred;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref geneType, "geneType");
			Scribe_Values.Look(ref haveType, "haveType");
		}
		protected override ThingQuery Clone()
		{
			ThingQueryGene clone = (ThingQueryGene)base.Clone();
			clone.geneType = geneType;
			clone.haveType = haveType;
			return clone;
		}


		public override string NullOption() => "No gene";

		public override int ExtraOptionsCount => 1;
		public override string NameForExtra(int ex) => "Any gene";


		private static List<GeneSetHolderBase> _geneHolders = new();
		public static List<GeneSetHolderBase> GetGeneholdersFrom(Thing thing, GeneType geneType = default)
		{
			// If not in a pawn, genes will be in a GeneSetHolderBase
			_geneHolders.Clear();

			// A genepack or xenogerm (or embryo but that case is handled earlier in Applies)
			if (thing is GeneSetHolderBase holder)
				_geneHolders.Add(holder);

			// some GeneSetHolderBase might be contained in a genebank
			if (geneType != GeneType.Endogene &&
				thing.TryGetComp<CompGenepackContainer>() is CompGenepackContainer comp)
			{
				// gene bank, aka CompGenepackContainer
				_geneHolders.AddRange(comp.ContainedGenepacks);
			}

			// Some genebanks may be an attached facility to a gene assembler
			if (geneType != GeneType.Endogene &&
				thing is Building_GeneAssembler geneAss)
			{
				_geneHolders.AddRange(geneAss.GetGenepacks(true, true));
			}

			return _geneHolders;
		}
		public override bool AppliesDirectly2(Thing thing)
		{
			// Check pawns genes, easy:
			if (thing is Pawn pawn)
			{
				if (pawn.genes == null) return false;

				List<Gene> geneList = geneType switch
				{
					GeneType.Xenogene => pawn.genes.Xenogenes,
					GeneType.Endogene => pawn.genes.Endogenes,
					_ => pawn.genes.GenesListForReading
				};

				Predicate<Gene> pawnGeneFilter = haveType switch
				{
					GeneHavingType.Active => g => g.Active,
					GeneHavingType.Inactive => g => !g.Active,
					_ => g => true
				};

				// Find if Any gene matches
				if (extraOption == 1)
					return geneList.Any(pawnGeneFilter);

				// Find if no gene matches
				// But, when searching active/inactive, don't report "Yes, you have no inactive genes"
				// If you have genes at all.
				if (sel == null)
					return (haveType == GeneHavingType.Has || geneList.Any()) && !geneList.Any(pawnGeneFilter);

				// find GeneDef sel
				return geneList.Any(g => g.def == sel && pawnGeneFilter(g));
			}



			// Bebe
			if (thing is HumanEmbryo emb)
			{
				if (geneType == GeneType.Xenogene)
					return false; // Xenogenes are N/A for bebe


				Predicate<GeneSetHolderBase, GeneDef> geneFilter = (holder, gene) =>
				haveType switch
				{
					GeneHavingType.Active => !holder.geneSet.IsOverridden(gene),
					GeneHavingType.Inactive => holder.geneSet.IsOverridden(gene),
					_ => true
				};

				// Find if Any gene matches
				if (extraOption == 1)
					return emb.geneSet.genes.Any(geneDef => geneFilter(emb, geneDef));

				// Find if any gene matches, and abort!
				if (sel == null)
					return !emb.geneSet.genes.Any(geneDef => geneFilter(emb, geneDef));

				// Check for GeneDef sel
				return emb.geneSet.genes.Contains(sel) && geneFilter(emb, sel);
			}


			// Remaining geneholders are Genepacks, Genebanks, Gene Assembers, Xenogenes
			// All of which will be xenogenes as they are not people
			if (geneType == GeneType.Endogene)
				return false;

			var holders = GetGeneholdersFrom(thing, geneType);

			// No geneholders means skip this thing, it might be a wall or something
			if (holders.Any())
			{
				// Copied from above hnnng
				Predicate<GeneSetHolderBase, GeneDef> geneFilter = (holder, gene) =>
				haveType switch
				{
					GeneHavingType.Active => !holder.geneSet.IsOverridden(gene),
					GeneHavingType.Inactive => holder.geneSet.IsOverridden(gene),
					_ => true
				};

				foreach (var geneHolder in holders)
				{
					if (extraOption == 1)
					{
						// Find if Any gene matches
						if (geneHolder.geneSet.genes.Any(geneDef => geneFilter(geneHolder, geneDef)))
							return true;
					}
					else if (sel == null)
					{
						// Find if any gene matches, and abort!
						if (geneHolder.geneSet.genes.Any(geneDef => geneFilter(geneHolder, geneDef)))
							return false;
					}
					else
					{
						// Check for GeneDef sel
						if (geneHolder.geneSet.genes.Contains(sel) && geneFilter(geneHolder, sel))
						{
							return true;
						}
					}
				}

				// false: we don't have "any"
				if (extraOption == 1)
					return false;

				// true: we have "none"
				if (sel == null)
					return true;

				// false: we don't have "sel"
				return false;
			}


			return false;
		}


		/*
		 * So many genes are ingame that each category is used but each category is nearly empty.
		 * 
		public override IEnumerable<GeneDef> AvailableOptions() => 
			ContentsUtility.AvailableInGame(thing =>
				thing is Pawn pawn ? pawn.genes?.GenesListForReading.Select(g => g.def) :
				GetGeneholdersFrom(thing).SelectMany(holder => holder.GeneSet.genes));
		*/

		protected override float RowGap => 0;
		public override bool DrawCustom(Rect fullRect)
		{
			row.Label("is");
			row.Gap(RowGap);
			RowButtonFloatMenuEnum(haveType, newValue => haveType = newValue);
			row.Label("as");
			row.Gap(RowGap);
			RowButtonFloatMenuEnum(geneType, newValue => geneType = newValue);

			return false;
		}

		public override Texture2D IconTexFor(GeneDef def) => def.Icon;
		public override Color IconColorFor(GeneDef def) => def.IconColor;

		// GeneUtility.GenesInOrder but include melanin
		private static List<GeneDef> _options = DefDatabase<GeneDef>.AllDefs
			.OrderBy((GeneDef x) => 0f - x.displayCategory.displayPriorityInXenotype)
			.ThenBy((GeneDef x) => x.displayCategory.label)
			.ThenBy((GeneDef x) => x.displayOrderInCategory)
			.ToList();
		public override IEnumerable<GeneDef> AllOptions() => _options;
	}
}

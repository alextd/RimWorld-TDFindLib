using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TD_Find_Lib;
using Verse;
using RimWorld;
using UnityEngine;

namespace TDFindLib_Ideology
{

	//helper class for category
	public class ThingQueryStyleCategory : ThingQueryCategorizedDropdownHelper<ThingStyleDef, StyleCategoryDef, ThingQueryThingStyle, ThingQueryStyleCategory>
	{
		public override bool AppliesDirectlyTo(Thing thing) =>
			// Not ?.StyleDef because null .Category is an option
			thing.StyleDef is ThingStyleDef styleDef && styleDef.Category == sel;


		public override string NameFor(StyleCategoryDef cat) => cat.LabelCap;
		public override string NullOption() => "TD.OtherCategory".Translate();
	}

	public class ThingQueryThingStyle : ThingQueryCategorizedDropdown<ThingStyleDef, StyleCategoryDef, ThingQueryThingStyle, ThingQueryStyleCategory>
	{
		public ThingQueryThingStyle() => extraOption = 1;

		public override bool AppliesDirectly2(Thing thing)
		{
			if (extraOption == 1)
				return thing.StyleDef != null;

			// redundant with check below
			//if (sel == null)
			//	return thing.StyleDef == null;

			return thing.StyleDef == sel;
		}

		public static readonly Dictionary<ThingStyleDef, string> styleNames = new();
		public static readonly Dictionary<ThingStyleDef, ThingDef> baseDefForStyle = new();
		static ThingQueryThingStyle()
		{
			foreach (StyleCategoryDef styleDef in DefDatabase<StyleCategoryDef>.AllDefsListForReading)
				foreach (ThingDefStyle style in styleDef.thingDefStyles)
				{
					baseDefForStyle[style.StyleDef] = style.ThingDef;

					styleNames[style.StyleDef] = styleDef.LabelCap + " " + style.ThingDef.LabelCap;
				}

			
			foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefsListForReading)
				if (thingDef.randomStyle != null)
					foreach (ThingStyleChance styleChance in thingDef.randomStyle)
					{
						baseDefForStyle[styleChance.StyleDef] = thingDef;

						// Probably redundant since this seems to be overriden below:
						styleNames[styleChance.StyleDef] = thingDef.LabelCap;
					}

			// overrides
			foreach (ThingStyleDef styleDef in DefDatabase<ThingStyleDef>.AllDefsListForReading)
				if (styleDef.overrideLabel != null)
					styleNames[styleDef] = styleDef.overrideLabel.CapitalizeFirst();
		}
		public override string NameFor(ThingStyleDef def) => styleNames[def];


		public override string NullOption() => "None".Translate();
		public override int ExtraOptionsCount => 1;
		public override string NameForExtra(int ex) => "TD.AnyOption".Translate();
		public override StyleCategoryDef CategoryFor(ThingStyleDef def) => def.Category;

		public override Texture2D IconTexFor(ThingStyleDef def) => def.UIIcon;
		public override Color IconColorFor(ThingStyleDef def)
		{
			if (def.color != default)
				return def.color; // though def.color is never set without mods..

			ThingDef baseDef = baseDefForStyle[def];
			if (baseDef.MadeFromStuff)
				return baseDef.GetColorForStuff(GenStuff.DefaultStuffFor(baseDef));
			return baseDef.uiIconColor;
		}

		public override IEnumerable<ThingStyleDef> AvailableOptions() =>
			ContentsUtility.AvailableInGame(t => t.StyleDef);
	}


	public class ThingQueryIdeoligion : ThingQueryDropDown<Ideo>
	{
		public ThingQueryIdeoligion() => extraOption = 1;

		protected override Ideo ResolveRef(Map map) => 
			Find.IdeoManager.ideos.FirstOrDefault(i => NameFor(i) == selName);

		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			Ideo ideo = pawn.Ideo;
			if (ideo == null) return false;

			if (extraOption == 1)
			{
				return pawn.Ideo == Faction.OfPlayer.ideos.PrimaryIdeo;
			}

			return pawn.Ideo == sel;
		}

		public override IEnumerable<Ideo> AllOptions() =>
			Current.Game?.World?.ideoManager?.IdeosInViewOrder;

		public override Color IconColorFor(Ideo ideo) => ideo.Color;
		public override Texture2D IconTexFor(Ideo ideo) => ideo.Icon;

		public override int ExtraOptionsCount => 1;
		public override string NameForExtra(int ex) => "Player's Ideoligion";
	}


	//Non-structure memes.
	public class ThingQueryMeme : ThingQueryDropDown<MemeDef>
	{
		public ThingQueryMeme() => sel = MemeDefOf.Transhumanist;

		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			Ideo ideo = pawn.Ideo;
			if (ideo == null) return false;


			return pawn.Ideo.memes.Contains(sel);
		}

		public override Texture2D IconTexFor(MemeDef d) => d.Icon;

		public override IEnumerable<MemeDef> AllOptions() =>
				// Only Structore or Normal, ehhhh
				base.AllOptions().Where(m => m.category != MemeCategory.Structure);
	}

	public class ThingQueryPrecept : ThingQueryCategorizedDropdown <PreceptDef, IssueDef>
	{
		public ThingQueryPrecept() => sel = PreceptDefOf.Slavery_Honorable;

		public override string NameForCat(IssueDef cat) => cat?.LabelCap ?? "TD.OtherCategory".Translate();

		public static readonly HashSet<PreceptDef> singlePreceptIssues =
			DefDatabase<PreceptDef>.AllDefs.Where(p => !DefDatabase<PreceptDef>.AllDefs.Any(other => p != other && other.issue == p.issue)).ToHashSet();
		public override IssueDef CategoryFor(PreceptDef def) => singlePreceptIssues.Contains(def) ? null : def.issue;

		public override Texture2D IconTexForCat(IssueDef cat) => cat?.Icon ?? null;

		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			Ideo ideo = pawn.Ideo;
			if (ideo == null) return false;


			return pawn.Ideo.precepts.Any(precept => precept.def == sel);
		}

		public override Texture2D IconTexFor(PreceptDef d) => d.Icon;
		public override string NameFor(PreceptDef d) => d.tipLabelOverride ?? ((string)(d.issue.LabelCap + ": " + d.LabelCap)); //Precept.TipLabel

		public override bool Ordered => true;
		public override IEnumerable<PreceptDef> AllOptions() =>
			base.AllOptions().Where(p => p.visible && p.preceptClass == typeof(Precept));
	}


	[StaticConstructorOnStartup]
	public static class ExpansionHider
	{
		static ExpansionHider()
		{
			if (!ModsConfig.IdeologyActive)
				foreach (ThingQuerySelectableDef def in DefDatabase<ThingQuerySelectableDef>.AllDefsListForReading)
					if (def.mod.EqualsIgnoreCase("ludeon.rimworld.ideology"))
						def.devOnly = true;
		}
	}
}

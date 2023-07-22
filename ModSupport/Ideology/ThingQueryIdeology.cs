using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TD_Find_Lib;
using Verse;
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
		static ThingQueryThingStyle()
		{
			foreach (StyleCategoryDef styleDef in DefDatabase<StyleCategoryDef>.AllDefsListForReading)
				foreach (ThingDefStyle style in styleDef.thingDefStyles)
					styleNames[style.StyleDef] = styleDef.LabelCap + " " + style.ThingDef.LabelCap;

			// Probably redundant since this seems to be overriden below:
			foreach(ThingDef thingDef in DefDatabase<ThingDef>.AllDefsListForReading)
				if(thingDef.randomStyle != null)
					foreach(ThingStyleChance styleChance in thingDef.randomStyle)
						styleNames[styleChance.StyleDef] = thingDef.LabelCap;

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

		public override IEnumerable<ThingStyleDef> Options() =>
			TD_Find_Lib.Mod.settings.OnlyAvailable
				? base.Options().Intersect(ContentsUtility.AvailableInGame(t => t.StyleDef))
				: base.Options();
	}



	[StaticConstructorOnStartup]
	public static class ExpansionHider
	{
		static ExpansionHider()
		{
			if (!ModsConfig.IdeologyActive)
				foreach (ThingQuerySelectableDef def in DefDatabase<ThingQuerySelectableDef>.AllDefsListForReading)
					if (def.mod == "ludeon.rimworld.ideology")
						def.devOnly = true;
		}
	}
}

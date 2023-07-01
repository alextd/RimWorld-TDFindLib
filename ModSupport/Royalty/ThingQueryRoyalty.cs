using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TD_Find_Lib;
using Verse;
using RimWorld;

namespace TDFindLib_Royalty
{
	// It is a bit roundabout to have a project for this when there is no new dll dependency but, eh, organization I gues.
	public class ThingQueryRoyalTitle : ThingQueryDropDown<RoyalTitleDef>
	{
		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null || pawn.royalty == null) return false;

			if (sel == null)
				return pawn.royalty.AllTitlesForReading.Count == 0;

			return pawn.royalty?.HasTitle(sel) ?? false;
		}

		public override string NullOption() => "None".Translate();
	}

	[StaticConstructorOnStartup]
	public static class ExpansionHider
	{
		static ExpansionHider()
		{
			if(!ModsConfig.RoyaltyActive)
			{
				//TODO: Foreach all classes in this namespace
				ThingQueryDef def = ThingQueryMaker.QueryDefForType(typeof(ThingQueryRoyalTitle));
				def.devOnly = true;
			}
		}
	}
}

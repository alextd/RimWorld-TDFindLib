using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TD_Find_Lib;
using Verse;
using CombatAI;

namespace TDFindLib_CAIFOW
{
	public class ThingQueryCAIFOW : ThingQuery
	{
		public override bool AppliesDirectlyTo(Thing thing) =>
			Finder.Settings.FogOfWar_Enabled
			&& !(thing.MapHeld.GetComp_Fast<MapComponent_FogGrid>()?.IsFogged(thing.PositionHeld) ?? false); // default false => not fogged => return true
	}
}

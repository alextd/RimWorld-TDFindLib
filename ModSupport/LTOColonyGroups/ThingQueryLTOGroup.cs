using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TD_Find_Lib;
using Verse;
using TacticalGroups;

namespace LTOColonyGroupsSupport
{
	public class ThingQueryLTOGroup : ThingQueryDropDown<ColonistGroup>
	{
		public override bool AppliesDirectlyTo(Thing thing) =>
			sel == null ? thing is Pawn pawn && pawn.TryGetGroups(out HashSet<ColonistGroup> groups) && groups.Count(g => g is PawnGroup) == 0
			: sel.pawns.Contains(thing);

		public override string NameFor(ColonistGroup o) => o.curGroupName;

		protected override ColonistGroup ResolveRef(Map map) =>
			TacticUtils.AllPawnGroups.FirstOrDefault(g => g.curGroupName == selName);

		public override string NullOption() => "None".Translate();

		public override IEnumerable<ColonistGroup> AllOptions() =>
			TacticUtils.AllPawnGroups;
	}
}

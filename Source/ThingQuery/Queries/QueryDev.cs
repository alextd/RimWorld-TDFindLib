using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;

namespace TD_Find_Lib
{
	public class ThingQueryClassType : ThingQueryDropDown<Type>
	{
		public ThingQueryClassType() => sel = typeof(Thing);

		public override bool AppliesDirectlyTo(Thing thing) =>
			sel.IsAssignableFrom(thing.GetType());

		public static List<Type> types = typeof(Thing).AllSubclassesNonAbstract().OrderBy(t => t.ToString()).ToList();
		public override IEnumerable<Type> AllOptions() => types;
		public override IEnumerable<Type> AvailableOptions() =>
			ContentsUtility.AvailableInGame(t => t.GetType());
	}

	public class ThingQueryDrawerType : ThingQueryDropDown<DrawerType>
	{
		public override bool AppliesDirectlyTo(Thing thing) =>
			thing.def.drawerType == sel;
	}

	public class ThingQueryFogged : ThingQuery
	{
		public override bool AppliesDirectlyTo(Thing thing) =>
			thing.PositionHeld.Fogged(thing.MapHeld);
	}
}

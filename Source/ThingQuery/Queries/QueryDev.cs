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

		public override bool ApplesDirectlyTo(Thing thing) =>
			sel.IsAssignableFrom(thing.GetType());

		public static List<Type> types = typeof(Thing).AllSubclassesNonAbstract().OrderBy(t => t.ToString()).ToList();
		public override IEnumerable<Type> Options() =>
			Mod.settings.OnlyAvailable ?
				ContentsUtility.AvailableInGame(t => t.GetType()).OrderBy(NameFor).ToList() :
				types;
	}

	public class ThingQueryDrawerType : ThingQueryDropDown<DrawerType>
	{
		public override bool ApplesDirectlyTo(Thing thing) =>
			thing.def.drawerType == sel;
	}

	public class ThingQueryFogged : ThingQuery
	{
		public override bool ApplesDirectlyTo(Thing thing) =>
			thing.PositionHeld.Fogged(thing.MapHeld);
	}
}

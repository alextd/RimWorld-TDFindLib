using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;

namespace TD_Find_Lib
{
	public class ListFilterClassType : ListFilterDropDown<Type>
	{
		public ListFilterClassType() => sel = typeof(Thing);

		protected override bool FilterApplies(Thing thing) =>
			sel.IsAssignableFrom(thing.GetType());

		public static List<Type> types = typeof(Thing).AllSubclassesNonAbstract().OrderBy(t => t.ToString()).ToList();
		public override IEnumerable<Type> Options() =>
			Mod.settings.OnlyAvailable ?
				ContentsUtility.AvailableInGame(t => t.GetType()).OrderBy(NameFor).ToList() :
				types;
	}

	public class ListFilterDrawerType : ListFilterDropDown<DrawerType>
	{
		protected override bool FilterApplies(Thing thing) =>
			thing.def.drawerType == sel;
	}

	public class ListFilterFogged : ListFilter
	{
		protected override bool FilterApplies(Thing thing) =>
			thing.PositionHeld.Fogged(thing.MapHeld);
	}
}

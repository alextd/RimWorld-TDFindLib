using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using RimWorld;

namespace TD_Find_Lib
{
	[DefOf]
	[StaticConstructorOnStartup]
	public static class ListFilterMaker
	{
		public static ListFilterDef Filter_Area;
		public static ListFilterDef Filter_AreaRestriction;
		public static ListFilterDef Filter_Capacity;
		public static ListFilterDef Filter_Category;
		public static ListFilterDef Filter_ClassType;
		public static ListFilterDef Filter_Crop;
		public static ListFilterDef Filter_Def;
		public static ListFilterDef Filter_Designation;
		public static ListFilterDef Filter_Deterioration;
		public static ListFilterDef Filter_DiesLeafless;
		public static ListFilterDef Filter_Door;
		public static ListFilterDef Filter_Drafted;
		public static ListFilterDef Filter_DrawerType;
		public static ListFilterDef Filter_Egg;
		public static ListFilterDef Filter_Faction;
		public static ListFilterDef Filter_Fogged;
		public static ListFilterDef Filter_Forbidden;
		public static ListFilterDef Filter_Freshness;
		public static ListFilterDef Filter_Gender;
		public static ListFilterDef Filter_Group;
		public static ListFilterDef Filter_Growth;
		public static ListFilterDef Filter_Guest;
		public static ListFilterDef Filter_HP;
		public static ListFilterDef Filter_Harvestable;
		public static ListFilterDef Filter_Health;
		public static ListFilterDef Filter_Incapable;
		public static ListFilterDef Filter_Inspiration;
		public static ListFilterDef Filter_Inventory;
		public static ListFilterDef Filter_Job;
		public static ListFilterDef Filter_Leather;
		public static ListFilterDef Filter_ListFilterDevelopmentalStage;
		public static ListFilterDef Filter_Meat;
		public static ListFilterDef Filter_MentalState;
		public static ListFilterDef Filter_Milk;
		public static ListFilterDef Filter_Mineable;
		public static ListFilterDef Filter_MissingBodyPart;
		public static ListFilterDef Filter_Mod;
		public static ListFilterDef Filter_Name;
		public static ListFilterDef Filter_Nearby;
		public static ListFilterDef Filter_Need;
		public static ListFilterDef Filter_Onscreen;
		public static ListFilterDef Filter_Prisoner;
		public static ListFilterDef Filter_ProductProgress;
		public static ListFilterDef Filter_Quality;
		public static ListFilterDef Filter_RaceProps;
		public static ListFilterDef Filter_Skill;
		public static ListFilterDef Filter_SpecialFilter;
		public static ListFilterDef Filter_Stat;
		public static ListFilterDef Filter_Stuff;
		public static ListFilterDef Filter_Temp;
		public static ListFilterDef Filter_Thought;
		public static ListFilterDef Filter_TimeToRot;
		public static ListFilterDef Filter_Trait;
		public static ListFilterDef Filter_Wool;
		public static ListFilterDef Filter_Zone;

		// The result is to be added to a IFilterHolder with Add()
		// (Either a FindDescription or a ListFilterGroup)
		public static ListFilter MakeFilter(ListFilterDef def)
		{
			ListFilter filter = (ListFilter)Activator.CreateInstance(def.filterClass);
			filter.def = def;
			return filter;
		}

		// Categories and Filters that aren't grouped under a Category
		private static readonly List<ListFilterSelectableDef> rootFilters;

		static ListFilterMaker()
		{
			rootFilters = DefDatabase<ListFilterSelectableDef>.AllDefs.ToList();
			foreach (var listDef in DefDatabase<ListFilterCategoryDef>.AllDefs)
				foreach (var subDef in listDef.SubFilters)
					rootFilters.Remove(subDef);
		}

		public static IEnumerable<ListFilterSelectableDef> SelectableList =>
			rootFilters.Where(d => (DebugSettings.godMode || !d.devOnly));
	}
}

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
	public static class ThingQueryMaker
	{
		public static ThingQueryDef Query_Area;
		public static ThingQueryDef Query_AreaRestriction;
		public static ThingQueryDef Query_Capacity;
		public static ThingQueryDef Query_ClassType;
		public static ThingQueryDef Query_Crop;
		public static ThingQueryDef Query_Def;
		public static ThingQueryDef Query_Designation;
		public static ThingQueryDef Query_Deterioration;
		public static ThingQueryDef Query_DiesLeafless;
		public static ThingQueryDef Query_Door;
		public static ThingQueryDef Query_Drafted;
		public static ThingQueryDef Query_DrawerType;
		public static ThingQueryDef Query_Egg;
		public static ThingQueryDef Query_Faction;
		public static ThingQueryDef Query_Fogged;
		public static ThingQueryDef Query_Forbidden;
		public static ThingQueryDef Query_Freshness;
		public static ThingQueryDef Query_Gender;
		public static ThingQueryDef Query_AndOrGroup;
		public static ThingQueryDef Query_Growth;
		public static ThingQueryDef Query_Guest;
		public static ThingQueryDef Query_HP;
		public static ThingQueryDef Query_Harvestable;
		public static ThingQueryDef Query_Health;
		public static ThingQueryDef Query_Incapable;
		public static ThingQueryDef Query_Inspiration;
		public static ThingQueryDef Query_Inventory;
		public static ThingQueryDef Query_ItemCategory;
		public static ThingQueryDef Query_Job;
		public static ThingQueryDef Query_Leather;
		public static ThingQueryDef Query_ThingQueryDevelopmentalStage;
		public static ThingQueryDef Query_Meat;
		public static ThingQueryDef Query_MentalState;
		public static ThingQueryDef Query_Milk;
		public static ThingQueryDef Query_Mineable;
		public static ThingQueryDef Query_MissingBodyPart;
		public static ThingQueryDef Query_Mod;
		public static ThingQueryDef Query_Name;
		public static ThingQueryDef Query_Nearby;
		public static ThingQueryDef Query_Need;
		public static ThingQueryDef Query_Onscreen;
		public static ThingQueryDef Query_Prisoner;
		public static ThingQueryDef Query_ProductProgress;
		public static ThingQueryDef Query_Quality;
		public static ThingQueryDef Query_RaceProps;
		public static ThingQueryDef Query_Skill;
		public static ThingQueryDef Query_SpecialFilter;
		public static ThingQueryDef Query_Stat;
		public static ThingQueryDef Query_Stuff;
		public static ThingQueryDef Query_Temp;
		public static ThingQueryDef Query_Thought;
		public static ThingQueryDef Query_TimeToRot;
		public static ThingQueryDef Query_Trait;
		public static ThingQueryDef Query_Wool;
		public static ThingQueryDef Query_Zone;

		// The result is to be added to a IQueryHolder with Add()
		// (Probably a QuerySearch or a ThingQueryGrouping)
		public static ThingQuery MakeQuery(ThingQueryDef def)
		{
			ThingQuery query = (ThingQuery)Activator.CreateInstance(def.queryClass);
			query.def = def;
			return query;
		}

		// Categories, and Queries that aren't grouped under a Category
		private static readonly List<ThingQuerySelectableDef> rootSelectableQueries;

		static ThingQueryMaker()
		{
			rootSelectableQueries = DefDatabase<ThingQuerySelectableDef>.AllDefs.ToList();
			foreach (var listDef in DefDatabase<ThingQueryCategoryDef>.AllDefs)
				foreach (var subDef in listDef.SubQueries)
					rootSelectableQueries.Remove(subDef);
		}

		public static IEnumerable<ThingQuerySelectableDef> SelectableList =>
			rootSelectableQueries.Where(d => (DebugSettings.godMode || !d.devOnly));
	}
}

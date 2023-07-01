using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using RimWorld;

namespace TD_Find_Lib
{
	// Create a ThingQuery in code wth ThingQueryMaker.MakeQuery<ThingQueryWhatever>();

	[StaticConstructorOnStartup]
	public static class ThingQueryMaker
	{
		// Construct a ThingQuery subclass, automatically assigning the appropriate Def
		// (This mod doesn't use it but other mods will)
		// The result ThingQuery is to be added to a IQueryHolder with Add()
		// (Probably a QuerySearch or a ThingQueryGrouping)
		private static readonly Dictionary<Type, ThingQueryDef> queryDefForType = new();
		public static ThingQueryDef QueryDefForType(Type t) =>
			queryDefForType[t];

		public static T MakeQuery<T>() where T : ThingQuery =>
			(T)MakeQuery(QueryDefForType(typeof(T)));

		public static ThingQuery MakeQuery(ThingQueryDef def)
		{
			ThingQuery query = (ThingQuery)Activator.CreateInstance(def.queryClass);
			query.def = def;
			return query;
		}

		// Categories, and Queries that aren't grouped under a Category
		private static readonly List<ThingQuerySelectableDef> rootSelectableQueries;

		// moddedQueiries was gonna be used to smart save them. But that never happened.
		public static readonly List<ThingQueryDef> moddedQueries;

		static ThingQueryMaker()
		{
			rootSelectableQueries = DefDatabase<ThingQuerySelectableDef>.AllDefs.ToList();


			// Remove any query that's in a subcategory from the main menu
			foreach (var listDef in DefDatabase<ThingQueryCategoryDef>.AllDefs)
				foreach (ThingQuerySelectableDef subDef in listDef.SubQueries)
					if(!subDef.topLevelSelectable)
						rootSelectableQueries.Remove(subDef);


			// Find modded queries
			var basePack = LoadedModManager.GetMod<Mod>().Content;
			List<ThingQuerySelectableDef> moddedSelections = rootSelectableQueries.FindAll(
				def => def.modContentPack != basePack || def.mod != null);
			moddedQueries = moddedSelections.Where(tq => tq is ThingQueryDef).Cast<ThingQueryDef>().ToList();

			// Remove the mod category if there's no modded filters
			if (moddedSelections.Count == 0)
				rootSelectableQueries.Remove(ThingQueryDefOf.Category_Mod);
			else
			{
				// Move Query_Mod, and all queries from mods, into Category_Mod
				rootSelectableQueries.Remove(ThingQueryDefOf.Query_Mod);
				moddedSelections.ForEach(d => rootSelectableQueries.Remove(d));
				ThingQueryDefOf.Category_Mod.subQueries = moddedSelections;
				ThingQueryDefOf.Category_Mod.subQueries.Insert(0, ThingQueryDefOf.Query_Mod);

				// Also insert where requested. The filter can end up in two places (as we already do for things like Stuff)
				foreach(var def in moddedSelections)
					if(def.insertCategory != null)
						def.insertCategory.subQueries.Add(def);

				//TODO: Multiple filters for a mod => Mod sublist by def.mod/def.modContentPack
			}


			// Construct the Def=>Query class dictionary so we can create Queries from MakeQuery<T> above
			foreach (var queryDef in DefDatabase<ThingQueryDef>.AllDefsListForReading)
				queryDefForType[queryDef.queryClass] = queryDef;


			// Config Error check
			foreach (var queryType in GenTypes.AllSubclassesNonAbstract(typeof(ThingQuery)))
				if (!queryDefForType.ContainsKey(queryType))
					Verse.Log.Error($"TDFindLib here, uhhh, there is no ThingQueryDef for {queryType}, thought you should know.");
		}

		public static IEnumerable<ThingQuerySelectableDef> RootQueries => rootSelectableQueries;
	}

	[DefOf]
	public static class ThingQueryDefOf
	{
		public static ThingQueryCategoryDef Category_Mod;
		public static ThingQueryDef Query_Mod;
		public static ThingQueryDef Query_Ability;
	}
}

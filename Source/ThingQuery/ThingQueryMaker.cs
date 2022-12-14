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
		// The result is to be added to a IQueryHolder with Add()
		// (Probably a QuerySearch or a ThingQueryGrouping)
		private static readonly Dictionary<Type, ThingQueryDef> defForQueryClasses = new();
		public static T MakeQuery<T>() where T : ThingQuery =>
			(T)MakeQuery(defForQueryClasses[typeof(T)]);

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


			// Remove any query that's in a subcategory from the main menu
			foreach (var listDef in DefDatabase<ThingQueryCategoryDef>.AllDefs)
				foreach (ThingQuerySelectableDef subDef in listDef.SubQueries)
					if(!subDef.topLevelSelectable)
						rootSelectableQueries.Remove(subDef);

			// Find modded queries
			var vanillaQueries = LoadedModManager.GetMod<Mod>().Content;
			List<ThingQuerySelectableDef> moddedQueries = rootSelectableQueries.FindAll(
				def => def.modContentPack != vanillaQueries || def.mod != null);

			// Remove the mod category if there's no modded filters
			if (moddedQueries.Count == 0)
				rootSelectableQueries.Remove(ThingQuerySelectableDefOf.Category_Mod);
			else
			{
				// Move Query_Mod, and all queries from mods, into Category_Mod
				rootSelectableQueries.Remove(ThingQuerySelectableDefOf.Query_Mod);
				ThingQuerySelectableDefOf.Category_Mod.subQueries = moddedQueries;
				ThingQuerySelectableDefOf.Category_Mod.subQueries.Insert(0, ThingQuerySelectableDefOf.Query_Mod);
			}

			// Construct the Def=>Query class dictionary so we can create Queries from MakeQuery<T> above
			foreach (var queryDef in DefDatabase<ThingQueryDef>.AllDefsListForReading)
				defForQueryClasses[queryDef.queryClass] = queryDef;

			// Config Error check
			foreach (var queryType in GenTypes.AllSubclassesNonAbstract(typeof(ThingQuery)))
				if (!defForQueryClasses.ContainsKey(queryType))
					Verse.Log.Error($"TDFindLib here, uhhh, there is no ThingQueryDef for {queryType}, thought you should know.");
		}

		public static IEnumerable<ThingQuerySelectableDef> SelectableList =>
			rootSelectableQueries.Where(d => (DebugSettings.godMode || !d.devOnly));
	}

	[DefOf]
	public static class ThingQuerySelectableDefOf
	{
		public static ThingQueryCategoryDef Category_Mod;
		public static ThingQueryDef Query_Mod;
	}
}

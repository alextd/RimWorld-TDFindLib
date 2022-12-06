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
			foreach (var listDef in DefDatabase<ThingQueryCategoryDef>.AllDefs)
				foreach (var subDef in listDef.SubQueries)
					rootSelectableQueries.Remove(subDef);

			foreach (var queryDef in DefDatabase<ThingQueryDef>.AllDefsListForReading)
				defForQueryClasses[queryDef.queryClass] = queryDef;

			foreach (var queryType in GenTypes.AllSubclassesNonAbstract(typeof(ThingQuery)))
				if (!defForQueryClasses.ContainsKey(queryType))
					Verse.Log.Error($"TDFindLib here, uhhh, there is no ThingQueryDef for {queryType}, thought you should know.");
		}

		public static IEnumerable<ThingQuerySelectableDef> SelectableList =>
			rootSelectableQueries.Where(d => (DebugSettings.godMode || !d.devOnly));
	}
}

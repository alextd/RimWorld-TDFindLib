using System;
using System.Reflection;
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
		// (Probably a QuerySearch or a ThingQueryAndOrGroup)
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

		public static ThingQuery MakeQuery(ThingQueryPreselectDef preDef)
		{
			ThingQuery query = MakeQuery(preDef.queryDef);

			foreach (var kvp in preDef.defaultValues)
			{
				if (DirectXmlToObject.GetFieldInfoForType(preDef.queryDef.queryClass, kvp.key, null) is FieldInfo field)
				{
					object obj = ConvertHelper.Convert(kvp.value, field.FieldType);
					field.SetValue(query, obj);
					continue;
				}

				//todo: store/save these for speed. meh.
				if (preDef.queryDef.queryClass.GetProperty(kvp.key, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty) is PropertyInfo prop)
				{
					object obj = ConvertHelper.Convert(kvp.value, prop.PropertyType);
					prop.SetMethod.Invoke(query, new object[] { obj });
					continue;
				}


				Verse.Log.Error($"Couldn't find how to set {preDef.queryDef.queryClass}.{kvp.key}");
			}

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

				Dictionary<string, List<ThingQuerySelectableDef>> modGroups = new();
				foreach(var def in moddedSelections)
				{
					string packageId = def.mod ?? def.modContentPack.PackageId;
					if(modGroups.TryGetValue(packageId, out var defs))
					{
						defs.Add(def);
					}
					else
					{
						modGroups[packageId] = new List<ThingQuerySelectableDef>() { def };
					}
				}

				List<ThingQuerySelectableDef> modMenu = new() { ThingQueryDefOf.Query_Mod };
				foreach ((string packageId, var defs) in modGroups)
				{
					if (defs.Count == 1)
						modMenu.AddRange(defs);
					else
					{
						ThingQueryCategoryDef catMenuSelectable = new() { mod = packageId };
						catMenuSelectable.label = LoadedModManager.RunningMods.FirstOrDefault(mod => mod.PackageId == packageId)?.Name ?? packageId;
						catMenuSelectable.subQueries = new();
						catMenuSelectable.subQueries.AddRange(defs);
						modMenu.Add(catMenuSelectable);
					}
				}

				ThingQueryDefOf.Category_Mod.subQueries = modMenu;

				// Also insert where requested. The filter can end up in two places (as we already do for things like Stuff)
				foreach (var def in moddedSelections)
				{
					if (def.insertCategory != null)
						def.insertCategory.subQueries.Add(def);

					if (def.insertCategories != null)
						foreach(var cat in def.insertCategories)
							cat.subQueries.Add(def);
				}
			}


			// Construct the Def=>Query class dictionary so we can create Queries from MakeQuery<T> above
			foreach (var queryDef in DefDatabase<ThingQueryDef>.AllDefsListForReading)
				queryDefForType[queryDef.queryClass] = queryDef;

			// Make dummy defs for All ThingQueryCategorizedDropdownHelper so they trigger warning below
			var modContentPack = LoadedModManager.GetMod<Mod>().Content;
			
			// GenTypes.AllSubclassesNonAbstract doesnt check generics properly so:
			foreach (Type helperType in (from x in GenTypes.AllTypes
																	 where !x.IsAbstract && x.IsSubclassOfRawGeneric(typeof(ThingQueryCategorizedDropdownHelper<,,,>))
																	 select x).ToList())
			{
				ThingQueryDef dummyDef = new();
				dummyDef.defName = "ThingQueryHelper_" + helperType.Name;
				dummyDef.queryClass = helperType;
				dummyDef.modContentPack = modContentPack;

				queryDefForType[helperType] = dummyDef;

				DefGenerator.AddImpliedDef(dummyDef);
			}


			// Config Error check
			foreach (var queryType in GenTypes.AllSubclassesNonAbstract(typeof(ThingQuery)))
				if (!queryDefForType.ContainsKey(queryType))
					Verse.Log.Error($"TDFindLib here, uhhh, there is no ThingQueryDef for {queryType}, thought you should know.");
		}

		public static IEnumerable<ThingQuerySelectableDef> RootQueries => rootSelectableQueries;


		// am I dabblin so far into the arcane I need this helper?
		static bool IsSubclassOfRawGeneric(this Type toCheck, Type generic)
		{
			while (toCheck != null && toCheck != typeof(object))
			{
				var cur = toCheck.IsGenericType ? toCheck.GetGenericTypeDefinition() : toCheck;
				if (generic == cur)
				{
					return true;
				}
				toCheck = toCheck.BaseType;
			}
			return false;
		}
	}

	[DefOf]
	public static class ThingQueryDefOf
	{
		public static ThingQueryCategoryDef Category_Mod;
		public static ThingQueryDef Query_Mod;
		public static ThingQueryDef Query_Ability;
	}
}

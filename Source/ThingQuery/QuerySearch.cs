using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;

namespace TD_Find_Lib
{
	// QuerySearch is the class to do a TDFindLib Thing Query Search, 
	// There's a few parts:
	// - The SearchListType narrows the basic type of thing you're searching
	// - The ThingQueries (the bulk of the lib) are countless options
	//    to select from every detail about a thing.
	// - And then, which map/maps to run the search on.


	// SearchListType:
	// What basic type of thing are you searching.
	public enum SearchListType
	{
		Selectable,	// Known as "Map", requires processing on All things.

		// Direct references to listerThings lists
		Everyone,
		Items,
		Buildings,
		Plants,

		Natural,	// Extra processing required
		ItemsAndJunk, // "Haulable" things, because "Items" above doesn't include e.g. chunks
		All,  // Very long including every blade of grass
		Inventory,	// Actually fast since there aren't many ThingHolders on a map

		//devmode options
		Haulables,
		Mergables,
		FilthInHomeArea
	}

	// QueryMapType:
	// What map or maps you're searching on.
	// For ChosenMaps, QueryParameters.searchMaps is set by the user
	// For CurMap / AllMaps, QueryParameters.searchMaps is null, but QueryResult.resultMaps is set when a search is run
	// TODO query for colony/raid maps.
	public enum QueryMapType	{ CurMap, AllMaps, ChosenMaps}
	public class BasicQueryParameters
	{
		// What basic list to search (TODO: list of types.)
		public SearchListType listType; //default is Selectable

		// How to look
		public bool matchAllQueries = true;

		// Where to look
		public QueryMapType mapType; //default is CurMap
		public List<Map> searchMaps = new();

		public BasicQueryParameters Clone(bool includeMaps = true)
		{
			BasicQueryParameters result = new();

			result.listType = listType;
			result.matchAllQueries = matchAllQueries;
			result.mapType = mapType;

			if(includeMaps)
				result.searchMaps.AddRange(searchMaps);
			
			return result;
		}
		public void ExposeData()
		{
			Scribe_Values.Look(ref listType, "listType");
			Scribe_Values.Look(ref matchAllQueries, "matchAllQueries", true);
			Scribe_Values.Look(ref mapType, "mapType");

			Scribe_Collections.Look(ref searchMaps, "searchMaps", LookMode.Reference);
		}
	}

	// What was found, and from where.
	public class QueryResult
	{
		public List<Map> resultMaps = new();

		public List<Thing> allThings = new();
		public Dictionary<Map, List<Thing>> mapThings = new();
		//Todo things by def/map?

		public bool godMode;
	}

	// The QuerySearch is the root of a TDFindLib search
	// - SearchListType which narrows what that things to look at
	// - owner of a set of queries
	// - What maps to search on
	// - Performs the search
	// - Holds the list of found things.
	public class QuerySearch : IExposable, IQueryHolder
	{
		public string name = "??NAME??";

		// Basic query settings:
		private BasicQueryParameters parameters = new();
		// What to search for
		public QueryHolder children;
		// Resulting things
		public QueryResult result = new();


		// "Inactive" is for the saved library of searches to Clone from.
		// inactive won't actually fill their lists,
		// which normally happens whenever queries are edited
		public bool active;

		// If you clone a QuerySearchiption it starts unchanged.
		// Not used directly but good to know if a save is needed.
		public bool changed;


		// from IQueryHolder:
		public QuerySearch RootQuerySearch => this;

		public SearchListType ListType
		{
			get => parameters.listType;
			set
			{
				parameters.listType = value;

				RemakeList();
			}
		}

		public bool MatchAllQueries
		{
			get => parameters.matchAllQueries;
			set
			{
				parameters.matchAllQueries = value;

				RemakeList();
			}
		}

		public QueryHolder Children => children;


		//A new QuerySearch, inactive, current map
		public QuerySearch()
		{
			children = new(this);
		}

		//A new QuerySearch, active, with this map
		// (Or just calls base constructor when null)
		public QuerySearch(Map map = null) : this()
		{
			if (map != null)
			{
				active = true;
				SetSearchMap(map, false);
			}
		}


		public void Reset()
		{
			changed = true;

			parameters = new();
			children.Clear();
			result = new();
		}


		// This is a roundabout way to hijack the esc-keypress from a window before it closes the window.
		// Any window displaying this has to override OnCancelKeyPressed and call this
		public bool OnCancelKeyPressed()
		{
			return children.Any(f => f.OnCancelKeyPressed());
		}


		public void ExposeData()
		{
			Scribe_Values.Look(ref name, "name");
			Scribe_Values.Look(ref active, "active");
			parameters.ExposeData();

			Children.ExposeData();
		}



		//Map shenanigans setters
		public void SetSearchChosenMaps()
		{
			//Pretty much for inactive queries right?
			parameters.mapType = QueryMapType.ChosenMaps;
			parameters.searchMaps.Clear();
		}

		public void SetSearchMap(Map newMap, bool remake = true)
		{
			parameters.mapType = QueryMapType.ChosenMaps;
			parameters.searchMaps.Clear();
			parameters.searchMaps.Add(newMap);

			if (remake) RemakeList();
		}

		public void SetSearchMaps(IEnumerable<Map> newMaps, bool remake = true)
		{
			parameters.mapType = QueryMapType.ChosenMaps;
			parameters.searchMaps.Clear();
			parameters.searchMaps.AddRange(newMaps);

			if (remake) RemakeList();
		}

		public void AddSearchMap(Map newMap, bool remake = true)
		{
			parameters.mapType = QueryMapType.ChosenMaps;
			parameters.searchMaps.Add(newMap);

			if (remake) RemakeList();
		}

		public void RemoveSearchMap(Map oldMap, bool remake = true)
		{
			if (parameters.mapType != QueryMapType.ChosenMaps) return; //Huh?

			parameters.searchMaps.Remove(oldMap);

			if (remake) RemakeList();
		}

		public void ToggleSearchMap(Map toggleMap, bool remake = true)
		{
			if (parameters.mapType != QueryMapType.ChosenMaps)
			{
				SetSearchMap(toggleMap, remake);
				return;
			}

			if (parameters.searchMaps.Contains(toggleMap))
			{
				if (parameters.searchMaps.Count == 1)
					Messages.Message("Hey man we have to search somewhere", MessageTypeDefOf.RejectInput, false);
				else
					parameters.searchMaps.Remove(toggleMap);
			}
			else
				parameters.searchMaps.Add(toggleMap);

			if (remake) RemakeList();
		}

		public void SetSearchCurrentMap(bool remake = true)
		{
			if (parameters.mapType == QueryMapType.CurMap) return;

			parameters.mapType = QueryMapType.CurMap;
			parameters.searchMaps.Clear();

			if (remake) RemakeList();
		}

		public void SetSearchAllMaps(bool remake = true)
		{
			if (parameters.mapType == QueryMapType.AllMaps) return;

			parameters.mapType = QueryMapType.AllMaps;
			parameters.searchMaps.Clear();

			if (remake) RemakeList();
		}


		// Get maps shenanigans
		public QueryMapType MapType => parameters.mapType;

		public List<Map> ChosenMaps =>
			parameters.mapType == QueryMapType.ChosenMaps ? parameters.searchMaps : null;

		public bool AllMaps => parameters.mapType == QueryMapType.AllMaps;

		// Certain queries only work on the current map, so the entire tree will only work on the current map
		public bool CurMapOnly() =>
			parameters.mapType == QueryMapType.CurMap || Children.Any(f => f.CurMapOnly);


		public string GetMapNameSuffix()
		{
			StringBuilder sb = new(" <i>(");

			// override requested map if a query only works on current map
			if (parameters.mapType == QueryMapType.AllMaps)
				sb.Append("TD.AllMaps".Translate());
			else if (result.resultMaps.Count > 0)
				sb.Append(string.Join(", ", result.resultMaps.Select(m => m.Parent.LabelCap)));
			else if (parameters.searchMaps.Count > 0)
				sb.Append(string.Join(", ", parameters.searchMaps.Select(m => m.Parent.LabelCap)));
			else return "";

			//Don't write "Current Map", doesn't look good. It is "unknown" until searched anyway

			sb.Append(")</i>");

			return sb.ToString();
		}


		public string GetMapOptionLabel()
		{
			StringBuilder sb = new("Searching: ");

			// override requested map if a query only works on current map
			if (parameters.mapType == QueryMapType.AllMaps)
				sb.Append("TD.AllMaps".Translate());
			else if (parameters.mapType == QueryMapType.CurMap)
				sb.Append("TD.CurrentMap".Translate());
			else if (parameters.searchMaps.Count == 1)
				sb.Append(parameters.searchMaps[0].Parent.LabelCap);
			else
				sb.Append("TD.ChosenMaps".Translate());

			return sb.ToString();
		}



		// Cloning shenanigans
		public enum CloneType { Save, Edit, Use }//Reference? Copy?

		//default(CloneArgs) CloneArgs is CloneType.Save
		public struct CloneArgs
		{
			public CloneType type;
			public Map map;
			public List<Map> maps;
			public string newName;

			public static CloneArgs save = new CloneArgs();
			public static CloneArgs edit = new CloneArgs() { type = CloneType.Edit };
			public static CloneArgs use = new CloneArgs() { type = CloneType.Use };
		}
		public QuerySearch Clone(CloneArgs args)
		{
			return args.type switch
			{
				CloneType.Save => CloneInactive(args.newName),

				CloneType.Edit => CloneInactive(args.newName),

				CloneType.Use =>
				args.maps != null ? CloneForUse(args.maps, args.newName)
				: CloneForUseSingle(args.map, args.newName),

				_ => null
			};
		}

		public QuerySearch CloneInactive(string newName = null)
		{
			QuerySearch newSearch = new QuerySearch()
			{
				name = newName ?? name,
				active = false,
				parameters = parameters.Clone(false)
			};

			newSearch.children = children.Clone(newSearch);

			return newSearch;
		}
		public QuerySearch CloneForUseSingle(Map newMap = null, string newName = null)
		{
			if (newMap != null)
				return CloneForUse(new List<Map> { newMap }, newName);
			else
				return CloneForUse(null, newName);
		}

		public QuerySearch CloneForUse(List<Map> newMaps = null, string newName = null)
		{
			QuerySearch newSearch = new QuerySearch()
			{
				name = newName ?? name,
				active = true,
				parameters = parameters.Clone()
			};


			// If you ask for a map, you're changing the setting.
			if (newMaps != null)
				newSearch.SetSearchMaps(newMaps, false);


			// If you loaded from a query that chose the map, but didn't choose, I guess we'll choose for you.
			if (newSearch.parameters.mapType == QueryMapType.ChosenMaps && newSearch.parameters.searchMaps.Count == 0)
				newSearch.SetSearchMap(Find.CurrentMap, false);



			newSearch.children = children.Clone(newSearch);


			newSearch.RemakeList();

			return newSearch;
		}

		private Map boundMap;
		private void BindToMap(Map map)
		{
			if (boundMap == map) return;

			boundMap = map;

			DoResolveRef(boundMap);
		}


		public void DoResolveRef(Map map)
		{
			Children.ForEach(f => f.DoResolveRef(map));
		}



		// Here we are finally
		// Actually searching and finding the list of things:
		public void RemakeList()
		{
			changed = true;

			// inactive = Don't do anything!
			if (!active)
				return;

			// Set up the maps:
			result.resultMaps.Clear();
			if (CurMapOnly())
				result.resultMaps.Add(Find.CurrentMap);
			else if (AllMaps)
				result.resultMaps.AddRange(Find.Maps);
			else
				result.resultMaps.AddRange(parameters.searchMaps);


			// Peform the search on the maps:
			result.allThings.Clear();
			result.mapThings.Clear();

			foreach (Map map in result.resultMaps)
			{
				List<Thing> things = new(Get(map, ListType));

				// SORT. TODO: more sensical than shortHash.
				things.SortBy(t => t.def.shortHash, t => t.Stuff?.shortHash ?? 0, t => t.Position.x + t.Position.z * 1000);

				result.mapThings[map] = things;
				result.allThings.AddRange(things);
			}

			newListedThings.Clear();

			//Btw, were we looking with godmode?
			result.godMode = DebugSettings.godMode;
		}

		private List<Thing> newListedThings = new();
		private List<Thing> newQueriedThings = new();
		private List<Thing> Get(Map searchMap, SearchListType searchListType)
		{ 
			BindToMap(searchMap);

			List<Thing> baseList = searchListType switch
			{
				SearchListType.Selectable => searchMap.listerThings.AllThings,
				SearchListType.Everyone => searchMap.listerThings.ThingsInGroup(ThingRequestGroup.Pawn),
				SearchListType.Items => searchMap.listerThings.ThingsInGroup(ThingRequestGroup.HaulableAlways),
				SearchListType.Buildings => searchMap.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial),
				SearchListType.Plants => searchMap.listerThings.ThingsInGroup(ThingRequestGroup.HarvestablePlant),
				SearchListType.Natural => searchMap.listerThings.AllThings,
				SearchListType.ItemsAndJunk => searchMap.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver),
				SearchListType.All => searchMap.listerThings.AllThings,
				SearchListType.Inventory => searchMap.listerThings.ThingsInGroup(ThingRequestGroup.ThingHolder),

				SearchListType.Haulables => searchMap.listerHaulables.ThingsPotentiallyNeedingHauling(),
				SearchListType.Mergables => searchMap.listerMergeables.ThingsPotentiallyNeedingMerging(),
				SearchListType.FilthInHomeArea => searchMap.listerFilthInHomeArea.FilthInHomeArea,
				_ => null
			};

			// newListedThings is what we're gonna return
			newListedThings.Clear();

			bool Listable(Thing t) =>
				DebugSettings.godMode ||
				ValidDef(t.def) && !t.Position.Fogged(searchMap);

			// Filter a bit more:
			switch (searchListType)
			{
				case SearchListType.Selectable: //Known as "Map"
					newListedThings.AddRange(baseList.Where(t => t.def.selectable && Listable(t)));
					break;

				case SearchListType.Natural:
					newListedThings.AddRange(baseList.Where(t => t.def.filthLeaving == ThingDefOf.Filth_RubbleRock && Listable(t)));
					break;

				case SearchListType.Inventory:
					foreach (Thing t in baseList.Where(Listable))
						if (t is IThingHolder holder && t is not Corpse && t is not MinifiedThing)
							ContentsUtility.AddAllKnownThingsInside(holder, newListedThings);
					break;

				default:
					newListedThings.AddRange(baseList.Where(Listable));
					break;
			}


			// Apply the actual queries, finally

			var queries = Children.queries.FindAll(f => f.Enabled);
			if (MatchAllQueries)
			{
				// ALL
				foreach (ThingQuery query in queries)
				{
					// Clears newQueriedThings, fills with newListedThings which pass the query.
					query.Apply(newListedThings, newQueriedThings);

					// newQueriedThings is now the list of things ; swap them
					(newListedThings, newQueriedThings) = (newQueriedThings, newListedThings);
				}
			}
			else
			{
				// ANY

				newQueriedThings.Clear();
				foreach (Thing thing in newListedThings)
					if (queries.Any(f => f.AppliesTo(thing)))
						newQueriedThings.Add(thing);

				(newListedThings, newQueriedThings) = (newQueriedThings, newListedThings);
			}

			newQueriedThings.Clear();

			return newListedThings;
		}

		//Probably a good filter
		public static bool ValidDef(ThingDef def) =>
			!typeof(Mote).IsAssignableFrom(def.thingClass) &&
			!typeof(Projectile).IsAssignableFrom(def.thingClass) &&
			def.drawerType != DrawerType.None;	//non-drawers are weird abstract things.
	}
}

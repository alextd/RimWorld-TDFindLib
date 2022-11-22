using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;

namespace TD_Find_Lib
{
	// FindDescription is the class to do a TDFindLib Thing Query Search, 
	// There's a few parts:
	// - The BaseListType narrows the basic type of thing you're searching
	// - The filters (the bulk of the lib) are countless query options
	//    to select from every detail about a thing.
	// - And then, which map/maps to run the search on.


	// BaseListType:
	// What basic type of thing are you searching.
	public enum BaseListType
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
	// TODO filter for colony/raid maps.
	public enum QueryMapType	{ CurMap, AllMaps, ChosenMaps}
	public class BasicQueryParameters
	{
		// What basic list to search (TODO: list of types.)
		public BaseListType baseType; //default is Selectable

		// How to look
		public QueryMapType mapType; //default is CurMap

		// Where to look
		public List<Map> searchMaps = new();

		public BasicQueryParameters Clone(bool includeMaps = true)
		{
			BasicQueryParameters result = new();

			result.baseType = baseType;
			result.mapType = mapType;

			if(includeMaps)
				result.searchMaps.AddRange(searchMaps);
			
			return result;
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

	// The FindDescription is the root of a TDFindLib search
	// - BaseListType which narrows what that things to look at
	// - owner of a set of filters
	// - What maps to search on
	// - Performs the search
	// - Holds the list of found things.
	public class FindDescription : IExposable, IFilterHolder
	{
		public string name = "TD.NewFindFilters".Translate();

		// Basic query settings:
		private BasicQueryParameters parameters = new();
		// What to filter for
		public FilterHolder children;
		// Resulting things
		public QueryResult result = new();


		// "Inactive" is for the saved library of filters to Clone from.
		// inactive won't actually fill their lists,
		// which normally happens whenever filters are edited
		public bool active;

		// If you clone a FindDesciption it starts unchanged.
		// Not used directly but good to know if a save is needed.
		public bool changed;


		// from IFilterHolder: FindDescription is the end of the chain of any nested filter's parent up to this root.
		public FindDescription RootFindDesc => this;

		public BaseListType BaseType
		{
			get => parameters.baseType;
			set
			{
				parameters.baseType = value;

				RemakeList();
			}
		}

		public FilterHolder Children => children;


		//A new FindDescription, inactive, current map
		public FindDescription()
		{
			children = new(this);
		}

		//A new FindDescription, active, with this map
		// (Or just calls base constructor when null)
		public FindDescription(Map map = null) : this()
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

			parameters = default;
			children.Clear();
			result = default;
		}


		public void ExposeData()
		{
			Scribe_Values.Look(ref name, "name");
			Scribe_Values.Look(ref active, "active");
			Scribe_Values.Look(ref parameters.baseType, "baseType");
			Scribe_Values.Look(ref parameters.mapType, "mapType");

			Scribe_Collections.Look(ref parameters.searchMaps, "searchMaps", LookMode.Reference);

			Children.ExposeData();
		}



		//Map shenanigans setters
		public void SetSearchChosenMaps()
		{
			//Pretty much for inactive filters right?
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

		// Certain filters only work on the current map, so the entire tree will only work on the current map
		public bool CurMapOnly() =>
			parameters.mapType == QueryMapType.CurMap || Children.Any(f => f.CurMapOnly);


		public string GetMapNameSuffix()
		{
			StringBuilder sb = new(" <i>(");

			// override requested map if a filter only works on current map
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

			// override requested map if a filter only works on current map
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
		public FindDescription Clone(CloneArgs args)
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

		public FindDescription CloneInactive(string newName = null)
		{
			FindDescription newDesc = new FindDescription()
			{
				name = newName ?? name,
				active = false,
				parameters = parameters.Clone(false)
			};

			newDesc.children = children.Clone(newDesc);

			return newDesc;
		}
		public FindDescription CloneForUseSingle(Map newMap = null, string newName = null)
		{
			if (newMap != null)
				return CloneForUse(new List<Map> { newMap }, newName);
			else
				return CloneForUse(null, newName);
		}

		public FindDescription CloneForUse(List<Map> newMaps = null, string newName = null)
		{
			FindDescription newDesc = new FindDescription()
			{
				name = newName ?? name,
				active = true,
				parameters = parameters.Clone()
			};


			// If you ask for a map, you're changing the setting.
			if (newMaps != null)
				newDesc.SetSearchMaps(newMaps, false);


			// If you loaded from a filter that chose the map, but didn't choose, I guess we'll choose for you.
			if (newDesc.parameters.mapType == QueryMapType.ChosenMaps && newDesc.parameters.searchMaps.Count == 0)
				newDesc.SetSearchMap(Find.CurrentMap, false);



			newDesc.children = children.Clone(newDesc);


			newDesc.RemakeList();

			return newDesc;
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
				List<Thing> things = new(Get(map, BaseType));

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
		private List<Thing> newFilteredThings = new();
		private List<Thing> Get(Map searchMap, BaseListType baseListType)
		{ 
			BindToMap(searchMap);

			List<Thing> baseList = baseListType switch
			{
				BaseListType.Selectable => searchMap.listerThings.AllThings,
				BaseListType.Everyone => searchMap.listerThings.ThingsInGroup(ThingRequestGroup.Pawn),
				BaseListType.Items => searchMap.listerThings.ThingsInGroup(ThingRequestGroup.HaulableAlways),
				BaseListType.Buildings => searchMap.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial),
				BaseListType.Plants => searchMap.listerThings.ThingsInGroup(ThingRequestGroup.HarvestablePlant),
				BaseListType.Natural => searchMap.listerThings.AllThings,
				BaseListType.ItemsAndJunk => searchMap.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver),
				BaseListType.All => searchMap.listerThings.AllThings,
				BaseListType.Inventory => searchMap.listerThings.ThingsInGroup(ThingRequestGroup.ThingHolder),

				BaseListType.Haulables => searchMap.listerHaulables.ThingsPotentiallyNeedingHauling(),
				BaseListType.Mergables => searchMap.listerMergeables.ThingsPotentiallyNeedingMerging(),
				BaseListType.FilthInHomeArea => searchMap.listerFilthInHomeArea.FilthInHomeArea,
				_ => null
			};

			// newListedThings is what we're gonna return
			newListedThings.Clear();

			bool Listable(Thing t) =>
				DebugSettings.godMode ||
				ValidDef(t.def) && !t.Position.Fogged(searchMap);

			//Filter a bit more:
			switch (baseListType)
			{
				case BaseListType.Selectable: //Known as "Map"
					newListedThings.AddRange(baseList.Where(t => t.def.selectable && Listable(t)));
					break;

				case BaseListType.Natural:
					newListedThings.AddRange(baseList.Where(t => t.def.filthLeaving == ThingDefOf.Filth_RubbleRock && Listable(t)));
					break;

				case BaseListType.Inventory:
					foreach (Thing t in baseList.Where(Listable))
						if (t is IThingHolder holder && t is not Corpse && t is not MinifiedThing)
							ContentsUtility.AddAllKnownThingsInside(holder, newListedThings);
					break;

				default:
					newListedThings.AddRange(baseList.Where(Listable));
					break;
			}


			// Apply the actual filters, finally
			foreach (ListFilter filter in Children.filters.FindAll(f => f.Enabled))
			{
				// Clears newFilteredThings, fills with newListedThings which pass filter.
				filter.Apply(newListedThings, newFilteredThings);

				// newFilteredThings is now the list of things ; swap them
				(newListedThings, newFilteredThings) = (newFilteredThings, newListedThings);
			}

			newFilteredThings.Clear();

			return newListedThings;
		}

		//Probably a good filter
		public static bool ValidDef(ThingDef def) =>
			!typeof(Mote).IsAssignableFrom(def.thingClass) &&
			!typeof(Projectile).IsAssignableFrom(def.thingClass) &&
			def.drawerType != DrawerType.None;	//non-drawers are weird abstract things.
	}
}

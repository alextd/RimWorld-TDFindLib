using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;

namespace TD_Find_Lib
{
	// The FindDescription is the root owner of a set of filters,
	// (It's a little more than a mere ListFilterGroup)
	// - Holds the list of things and performs the search
	// - BaseListType which narrows what that things to look at
	// - Checkbox bool allMaps that apply to all nested filters
	// - Apparently support for alerts which I'll probably separate out
	public class FindDescription : IExposable, IFilterHolder
	{
		public string name = "TD.NewFindFilters".Translate();
		
		private List<Thing> listedThings = new();
		private BaseListType _baseType;
		private FilterHolder children;
		private Map _map;
		private bool _allMaps;

		// "Inactive" is for the saved library of filters to Clone from.
		// inactive won't actually fill their lists
		// (inactive filters won't have a map anyway.)
		public bool active;


		// from IFilterHolder
		public FindDescription RootFindDesc => this;

		public IEnumerable<Thing> ListedThings => listedThings;

		public BaseListType BaseType
		{
			get => _baseType;
			set
			{
				_baseType = value;
				RemakeList();
			}
		}

		public FilterHolder Children => children;

		// the Map for when mode == Map
		public Map map
		{
			get => _map;
			set
			{
				_map = value;
				if (map != null)
				{
					// The only reason to set map to null, would be to save, or for allmaps.
					// Probably redundant to check for null here.
					active = true;
					_allMaps = false;
				}

				MakeMapLabel();
			}
		}
		public bool allMaps
		{
			get => _allMaps;
			set
			{
				_allMaps = value;

				MakeMapLabel();
			}
		}

		// Certain filters only work on the current map, so the entire tree will only work on the current map
		public bool FiltersCurrentMapOnly() => Children.Check(f => f.CurrentMapOnly);

		public string mapLabel;
		private void MakeMapLabel()
		{
			StringBuilder sb = new(" (");

			// override requested map if a filter only works on current map
			if (FiltersCurrentMapOnly())
				sb.Append("Current Map");
			else if (active)
			{
				if (allMaps)
					sb.Append("TD.AllMaps".Translate());
				else
					sb.Append(map.Parent.LabelCap);
			}
			else
			{
				if (allMaps)
					sb.Append("(inactive-allmaps)");
				else
					sb.Append("(inactive)");
			}

			sb.Append(")");

			mapLabel = sb.ToString();
		}


		public FindDescription()
		{
			children = new FilterHolder(this);
			active = true;
			allMaps = true;
		}

		public FindDescription(Map m)
		{
			children = new FilterHolder(this);
			map = m;
		}

		public void RemakeList()
		{
			//  inactive = Don't do anything!
			if (!active) return;


			listedThings.Clear();

			// "Current map only" overrides other choices
			if (FiltersCurrentMapOnly())
				listedThings.AddRange(Get(Find.CurrentMap));

			// All maps
			else if (allMaps)
				foreach (Map m in Find.Maps)
					listedThings.AddRange(Get(m));

			// Single map
			else
				listedThings.AddRange(Get(map));
		}


		public void ExposeData()
		{
			Scribe_Values.Look(ref name, "name");
			Scribe_Values.Look(ref _baseType, "baseType");
			Scribe_Values.Look(ref active, "active");
			Scribe_Values.Look(ref _allMaps, "allMaps");

			//no need to save map null
			if (Scribe.mode != LoadSaveMode.Saving || _map != null)
				Scribe_References.Look(ref _map, "map");

			Children.ExposeData();

			if (Scribe.mode == LoadSaveMode.PostLoadInit && active)
				ResolveNames();
		}

		public FindDescription CloneForSave() =>
			Clone(makeActive: false);

		public FindDescription CloneForUse(Map newMap = null) =>
			Clone(newMap: newMap);

		private FindDescription Clone(bool makeActive = true, Map newMap = null)
		{
			bool newAllMaps = allMaps;
			if(!makeActive && newMap != null)
			{
				Verse.Log.Warning($"Tried to clone FindDescription ({name}) as inactve with a map. Ignoring the map.");
				newMap = null;
			}
			if(makeActive && newMap != null && newAllMaps)
			{
				Verse.Log.Warning($"Tried to clone FindDescription ({name}) with a map and allMaps. Just using the map.");
				newAllMaps = false;
			}
			if (makeActive && newMap == null && !newAllMaps)
			{
				Verse.Log.Warning($"Tried to clone FindDescription ({name}) with neither map nor allMaps. Setting allMaps = true.");
				newAllMaps = true;
			}
			FindDescription newDesc = new FindDescription()
			{
				name = name,
				active = makeActive,
				_baseType = _baseType,
				_map = newMap,
				_allMaps = newAllMaps,
			};

			newDesc.children = children.Clone(newDesc);
			if (makeActive)
				newDesc.ResolveNames();

			return newDesc;
		}

		// To be called after loading or cloning.
		public void ResolveNames()
		{
			foreach (var f in Children.Filters)
			{
				// map may be null, that's okay.
				// Filters for an "all maps" search should not need maps.
				// But they do need to resolve other names (like defs)
				f.DoResolveLoadName(map);
			}
		}

		private IEnumerable<Thing> Get(Map searchMap)
		{
			IEnumerable<Thing> allThings = Enumerable.Empty<Thing>();
			switch (BaseType)
			{
				case BaseListType.Selectable: //Known as "Map"
					allThings = searchMap.listerThings.AllThings.Where(t => t.def.selectable);
					break;
				case BaseListType.Buildings:
					allThings = searchMap.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial);
					break;
				case BaseListType.Natural:
					allThings = searchMap.listerThings.AllThings.Where(t => t.def.filthLeaving == ThingDefOf.Filth_RubbleRock);
					break;
				case BaseListType.Plants:
					allThings = searchMap.listerThings.ThingsInGroup(ThingRequestGroup.Plant);
					break;
				case BaseListType.Inventory:
					List<IThingHolder> holders = new List<IThingHolder>();
					searchMap.GetChildHolders(holders);
					List<Thing> list = new List<Thing>();
					foreach (IThingHolder holder in holders.Where(ContentsUtility.CanPeekInventory))
						list.AddRange(ContentsUtility.AllKnownThings(holder));
					allThings = list;
					break;
				case BaseListType.Items:
					allThings = searchMap.listerThings.ThingsInGroup(ThingRequestGroup.HaulableAlways);
					break;
				case BaseListType.Everyone:
					allThings = searchMap.mapPawns.AllPawnsSpawned.Cast<Thing>();
					break;
				case BaseListType.Colonists:
					allThings = searchMap.mapPawns.FreeColonistsSpawned.Cast<Thing>();
					break;
				case BaseListType.Animals:
					allThings = searchMap.mapPawns.AllPawnsSpawned.Where(p => !p.RaceProps.Humanlike).Cast<Thing>();
					break;
				case BaseListType.All:
					allThings = ContentsUtility.AllKnownThings(searchMap);
					break;

				//Devmode options:
				case BaseListType.Haulables:
					allThings = searchMap.listerHaulables.ThingsPotentiallyNeedingHauling();
					break;
				case BaseListType.Mergables:
					allThings = searchMap.listerMergeables.ThingsPotentiallyNeedingMerging();
					break;
				case BaseListType.FilthInHomeArea:
					allThings = searchMap.listerFilthInHomeArea.FilthInHomeArea;
					break;
			}

			//Filters
			allThings = allThings.Where(t => !(t.ParentHolder is Corpse) && !(t.ParentHolder is MinifiedThing));
			if (!DebugSettings.godMode)
			{
				allThings = allThings.Where(t => ValidDef(t.def));
				allThings = allThings.Where(t => !t.PositionHeld.Fogged(searchMap));
			}
			foreach (ListFilter filter in Children.Filters)
				allThings = filter.Apply(allThings);

			//Sort
			return allThings.OrderBy(t => t.def.shortHash).ThenBy(t => t.Stuff?.shortHash ?? 0).ThenBy(t => t.Position.x + t.Position.z * 1000).ToList();
		}

		//Probably a good filter
		public static bool ValidDef(ThingDef def) =>
			!typeof(Mote).IsAssignableFrom(def.thingClass) &&
			def.drawerType != DrawerType.None;
	}

	public enum BaseListType
	{
		Selectable,
		Everyone,
		Colonists,
		Animals,
		Items,
		Buildings,
		Natural,
		Plants,
		Inventory,
		All,

		//devmode options
		Haulables,
		Mergables,
		FilthInHomeArea
	}

	public static class BaseListNormalTypes
	{
		public static readonly BaseListType[] normalTypes =
			{ BaseListType.Selectable, BaseListType.Everyone, BaseListType.Colonists, BaseListType.Animals, BaseListType.Items,
			BaseListType.Buildings, BaseListType.Natural, BaseListType.Plants, BaseListType.Inventory, BaseListType.All};
	}
}

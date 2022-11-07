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
		// activating a finddesc should have ResolveRefs called
		public bool active;

		// If you clone a FindDesciption it starts unchanged.
		public bool changed;


		// There's 4 states to be in active/inactive + singlemap/allmaps
		// Map _map is there for when active and singlemap
		// But _map == null doesn't imply allmaps, since it would be null for inactive singlemap 
		// _map != null with allMaps is fine - it won't use the map, but allMaps could be checked off and it has a map to use.


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

		// the Map for when active and !allMaps
		public Map map
		{
			get => _map;
			set
			{
				_map = value;
				if (map != null)
				{
					// The only reason to set map would be for active maps
					active = true;
					_allMaps = false;
				}
				/* Do not set allmaps false - an inactive findDesc for a single map has null map
				else
				{
					_allMaps = true;
				}*/


				MakeMapLabel();
				RemakeList();
			}
		}
		public bool allMaps
		{
			get => _allMaps;
			set
			{
				_allMaps = value;
				//But keep the map around just in case this gets checked off

				MakeMapLabel();
				RemakeList();
			}
		}

		// Certain filters only work on the current map, so the entire tree will only work on the current map
		public bool FiltersCurrentMapOnly() => Children.Any(f => f.CurrentMapOnly);

		public string mapLabel;
		public void MakeMapLabel()
		{
			mapLabel = GetMapLabel();
		}

		private string GetMapLabel()
		{
			StringBuilder sb = new(" (");

			// override requested map if a filter only works on current map
			if (FiltersCurrentMapOnly())
				sb.Append("Current Map");
			else if (allMaps)
				sb.Append("TD.AllMaps".Translate());
			else if (map != null)
				sb.Append(map.Parent.LabelCap);
			else return "";

			sb.Append(")");

			return sb.ToString();
		}


		//A new FindDescription, inactive, single map
		public FindDescription()
		{
			children = new FilterHolder(this);
		}

		//A new FindDescription, active, with this map
		public FindDescription(Map m = null) : this()
		{
			map = m;
		}


		public void Reset()
		{
			changed = true;
			children.Clear();
			_baseType = default;
			listedThings.Clear();
		}

		public void RemakeList()
		{
			changed = true;

			// inactive = Don't do anything!
			if (!active)
				return;

			// Nothing to filter? Nevermind!
			if (Children.Filters.Count() == 0)
				return;


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

			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				MakeMapLabel();
			}
		}

		public FindDescription CloneForSave()
		{
			FindDescription newDesc = new FindDescription(null)
			{
				name = name,
				active = false,
				_baseType = _baseType,
				_allMaps = allMaps,
			};

			newDesc.children = children.Clone(newDesc);

			newDesc.MakeMapLabel();

			return newDesc;
		}

		public FindDescription CloneForEdit()
		{
			FindDescription newDesc = new FindDescription()
			{
				name = name,
				active = false,
				_baseType = _baseType,
				_map = _map,
				_allMaps = allMaps,
				mapLabel = mapLabel,
			};

			newDesc.children = children.Clone(newDesc);

			return newDesc;
		}

		public FindDescription CloneForUse(Map newMap = null)
		{
			FindDescription newDesc = new FindDescription()
			{
				name = name,
				active = true,
				_baseType = _baseType,
				_map = newMap ?? _map,
				_allMaps = allMaps,
			};

			newDesc.children = children.Clone(newDesc);

			// If cloning from inactive filters, or setting a new map,
			// Must resolve refs
			if (!active || newMap != null)
				newDesc.Children.ForEach(f => f.DoResolveRef(map));

			newDesc.MakeMapLabel();

			return newDesc;
		}

		public IEnumerable<Thing> Get(Map searchMap)
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
			return allThings.OrderBy(t => t.def.shortHash).ThenBy(t => t.Stuff?.shortHash ?? 0).ThenBy(t => t.Position.x + t.Position.z * 1000);
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

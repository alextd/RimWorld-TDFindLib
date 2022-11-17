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


		private void Setup()
		{
			SetBaseList();
			MakeMapLabel();
		}

		//internal pointer to live lists do not edit!
		private List<Thing> baseList;
		public BaseListType BaseType
		{
			get => _baseType;
			set
			{
				_baseType = value;

				SetBaseList();
				RemakeList();
			}
		}
		private void SetBaseList()
		{
			if (!active) return;

			baseList = _baseType switch
			{
				BaseListType.Selectable => _map.listerThings.AllThings,
				BaseListType.Buildings => _map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial),
				BaseListType.Natural => _map.listerThings.AllThings,
				BaseListType.Plants => _map.listerThings.ThingsInGroup(ThingRequestGroup.HarvestablePlant),
				BaseListType.Inventory => _map.listerThings.ThingsInGroup(ThingRequestGroup.ThingHolder),
				BaseListType.Items => _map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableAlways),
				BaseListType.ItemsAndJunk => _map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver),
				BaseListType.Everyone => _map.listerThings.ThingsInGroup(ThingRequestGroup.Pawn),
				BaseListType.All => _map.listerThings.AllThings,
				BaseListType.AllAndInventory => _map.listerThings.AllThings,
				BaseListType.Haulables => _map.listerHaulables.ThingsPotentiallyNeedingHauling(),
				BaseListType.Mergables => _map.listerMergeables.ThingsPotentiallyNeedingMerging(),
				BaseListType.FilthInHomeArea => _map.listerFilthInHomeArea.FilthInHomeArea,
				_ => null
			};
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


		public string mapLabel;
		public void MakeMapLabel()
		{
			mapLabel = GetMapLabel();
		}

		private string GetMapLabel()
		{
			StringBuilder sb = new(" <i>(");

			// override requested map if a filter only works on current map
			if (allMaps)
				sb.Append("TD.AllMaps".Translate());
			else if (map != null)
				sb.Append(map.Parent.LabelCap);
			else return "";

			sb.Append(")</i>");

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
			// the same as map property setter except don't remake list
			_map = m;
			if (map != null)
			{
				// The only reason to set map would be for active maps
				active = true;
				_allMaps = false;
			}
			Setup();
		}


		public void Reset()
		{
			changed = true;
			children.Clear();
			_baseType = default;
			SetBaseList();
			listedThings.Clear();
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
				Setup();
			}
		}


		public enum CloneType { Save, Edit, Use }//Dont? Copy?

		//default(CloneArgs) CloneArgs is CloneType.Save
		public struct CloneArgs
		{
			public CloneType type;
			public Map map;
			public string newName;

			public static CloneArgs save = new CloneArgs();
			public static CloneArgs edit = new CloneArgs() { type = CloneType.Edit };
		}
		public FindDescription Clone(CloneArgs args)
		{
			return args.type switch
			{
				CloneType.Save => CloneForSave(args.newName),
				CloneType.Edit => CloneForEdit(args.newName),
				CloneType.Use => CloneForUse(args.map, args.newName),
				_ => null
			};
		}

		public FindDescription CloneForSave(string newName = null)
		{
			FindDescription newDesc = new FindDescription(null)
			{
				name = newName ?? name,
				active = false,
				_baseType = _baseType,
				_allMaps = allMaps,
			};

			newDesc.children = children.Clone(newDesc);

			newDesc.Setup();

			return newDesc;
		}

		public FindDescription CloneForEdit(string newName = null)
		{
			FindDescription newDesc = new FindDescription()
			{
				name = newName ?? name,
				active = false,
				_baseType = _baseType,
				_map = _map,
				_allMaps = allMaps
			};

			newDesc.children = children.Clone(newDesc);

			newDesc.Setup();

			return newDesc;
		}

		public FindDescription CloneForUse(Map newMap, string newName = null)
		{
			FindDescription newDesc = new FindDescription()
			{
				name = newName ?? name,
				active = true,
				_baseType = _baseType,
				_map = newMap ?? _map,
				_allMaps = allMaps,
			};


			if (newMap == null && _map == null && !allMaps)
			{
				newDesc._allMaps = true;
				Verse.Log.Warning("Tried to CloneForUse a singlemap filter with null map. Setting allMaps instead!");
			}

			newDesc.children = children.Clone(newDesc);

			// If cloning from inactive filters, or setting a new map,
			// Must resolve refs
			if (!active || newMap != null)
				newDesc.Children.ForEach(f => f.DoResolveRef());

			newDesc.Setup();
			newDesc.RemakeList();

			return newDesc;
		}


		public void RemakeList()
		{
			changed = true;

			// inactive = Don't do anything!
			if (!active)
				return;


			listedThings.Clear();

			// All maps
			if (allMaps)
				foreach (Map m in Find.Maps)
					Get(m);

			// Single map
			else
				Get(map);
		}

		public void Get(Map searchMap)
		{
			List<Thing> newThings;
			//Filter a but more:
			switch (_baseType)
			{
				/*
				case BaseListType.Selectable: //Known as "Map"
					newThings = baseList.Where(d => d.def.selectable);
					break;
					*/
				case BaseListType.Natural:
					newThings = baseList.FindAll(t => t.def.filthLeaving == ThingDefOf.Filth_RubbleRock);
					break;
				case BaseListType.Inventory:
					newThings = baseList.SelectMany(t => t.TryGetInnerInteractableThingOwner() ?? Enumerable.Empty<Thing>()).ToList();
					break;
				case BaseListType.AllAndInventory:
					newThings = baseList.SelectMany(t => new[] { t }.ConcatIfNotNull((t.TryGetInnerInteractableThingOwner() as IEnumerable<Thing>))).ToList();
					break;
				default:
					newThings = baseList;
					break;
			}

			foreach (Thing t in newThings)
			{
				bool include = true;
				foreach (ListFilter filter in Children.Filters)
				{
					if (!filter.Enabled) continue;
					if (!filter.AppliesTo(t))
					{
						include = false;
						break;
					}
				}
				if (include)
					listedThings.Add(t);
			}

			/*
			 * 
			//Filters
			IEnumerable<Thing> enumerator = GetThings.Where(t => !(t.ParentHolder is Corpse) && !(t.ParentHolder is MinifiedThing));
			if (!DebugSettings.godMode)
			{
				enumerator = enumerator.Where(t => ValidDef(t.def));
				enumerator = enumerator.Where(t => !t.PositionHeld.Fogged(searchMap));
			}

			//Sort
			return enumerator.OrderBy(t => t.def.shortHash).ThenBy(t => t.Stuff?.shortHash ?? 0).ThenBy(t => t.Position.x + t.Position.z * 1000).ToList();
			
			 */
		}

		//Probably a good filter
		public static bool ValidDef(ThingDef def) =>
			!typeof(Mote).IsAssignableFrom(def.thingClass) &&
			!typeof(Projectile).IsAssignableFrom(def.thingClass) &&
			def.drawerType != DrawerType.None;
	}
		

	public enum BaseListType
	{
		Selectable,
		Everyone,
		Items,
		ItemsAndJunk,
		Buildings,
		Natural,
		Plants,
		Inventory,
		All,
		AllAndInventory,

		//devmode options
		Haulables,
		Mergables,
		FilthInHomeArea
	}

	public static class BaseListNormalTypes
	{
		public static readonly BaseListType[] normalTypes =
			{ BaseListType.Selectable, BaseListType.Everyone, BaseListType.Items, BaseListType.ItemsAndJunk, BaseListType.Buildings, BaseListType.Natural,
			BaseListType.Plants, BaseListType.Inventory, BaseListType.All, BaseListType.AllAndInventory};
	}
}

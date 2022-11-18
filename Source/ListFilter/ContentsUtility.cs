using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;
using UnityEngine;

namespace TD_Find_Lib
{
	[StaticConstructorOnStartup]
	public static class ContentsUtility
	{
		public static bool IsValidHolder(this IThingHolder holder)
			=> holder.IsEnclosingContainer() && !(holder is MinifiedThing);


		// or godmode.
		public static bool CanPeekInventory(this IThingHolder holder) =>
			holder is Building_Casket c ? c.contentsKnown :
			true;

		private static List<Thing> _allKnownThingsList = new();
		public static void AddAllKnownThingsInside(IThingHolder holder, List<Thing> outThings)
		{
			//outThings would get cleared inside so can't be added to directly
			ThingOwnerUtility.GetAllThingsRecursively(holder, _allKnownThingsList, true, DebugSettings.godMode ? null : ContentsUtility.CanPeekInventory);
			outThings.AddRange(_allKnownThingsList);
		}

		// I assume you won't ask about fogged things, or tradeships
		public static void AllKnownThingsInside(IThingHolder holder, List<Thing> outThings)
		{
			ThingOwnerUtility.GetAllThingsRecursively(holder, outThings, true, DebugSettings.godMode ? null : ContentsUtility.CanPeekInventory);
		}

		private static bool CanPeekMap(Map map, IThingHolder holder)
		{
			//Let's not look at tradeships
			if (holder is TradeShip) return false;

			//After this, godmode can peek in
			if (DebugSettings.godMode) return true;

			// Can't list what you don't know
			if (holder is Building_Casket c && !c.contentsKnown)
				return false;

			// Can't list what you can't see
			if (holder is Thing t && t.Position.Fogged(map))
				return false;

			return true;
		}

		private static IEnumerable<Thing> AllKnownThings(Map map)
		{
			ThingOwnerUtility.GetAllThingsRecursively(map, _allKnownThingsList, true, h => ContentsUtility.CanPeekMap(map, h));
			
			return _allKnownThingsList;
		}

		public static HashSet<T> AvailableInGame<T>(Func<Thing, IEnumerable<T>> validGetter)
		{
			HashSet<T> ret = new HashSet<T>();
			foreach(Map map in Find.Maps)
				foreach (Thing t in ContentsUtility.AllKnownThings(map))
					foreach (T tDef in validGetter(t))
						ret.Add(tDef);

			_allKnownThingsList.Clear();
			return ret;
		}

		public static HashSet<T> AvailableInGame<T>(Func<Thing, T> validGetter)
		{
			HashSet<T> ret = new HashSet<T>();
			foreach (Map map in Find.Maps)
				foreach (Thing t in ContentsUtility.AllKnownThings(map))
					if(validGetter(t) is T def)
						ret.Add(def);

			_allKnownThingsList.Clear();
			return ret;
		}
	}
}

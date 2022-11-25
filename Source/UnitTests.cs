using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace TD_Find_Lib
{
	public static class UnitTests
	{
		public static void Run()
		{
			// I dunno how this'll work but hey, it'll probably throw errors on failure.

			foreach (var group in Mod.settings.searchGroups)
				foreach (var search in group)
					search.CloneForUse();

			QuerySearch searchTest = new();
			foreach(var queryDef in DefDatabase<ThingQueryDef>.AllDefsListForReading)
			{
				var query = ThingQueryMaker.MakeQuery(queryDef);
				searchTest.Children.Add(query, remake: Find.CurrentMap != null);
			}
		}
	}
}

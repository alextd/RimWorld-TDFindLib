using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using RimWorld;

namespace TD_Find_Lib
{
	[DefOf]
	[StaticConstructorOnStartup]
	public static class ListFilterMaker
	{
		public static ListFilterDef Filter_Name;

		// The result is to be added to a IFilterHolder with Add()
		// (Either a FindDescription or a ListFilterGroup)
		public static ListFilter MakeFilter(ListFilterDef def)
		{
			ListFilter filter = (ListFilter)Activator.CreateInstance(def.filterClass);
			filter.def = def;
			return filter;
		}

		// example filter
		public static ListFilter NameFilter() =>
			ListFilterMaker.MakeFilter(ListFilterMaker.Filter_Name);


		// Categories and Filters that aren't grouped under a Category
		private static readonly List<ListFilterSelectableDef> rootFilters;

		static ListFilterMaker()
		{
			rootFilters = DefDatabase<ListFilterSelectableDef>.AllDefs.ToList();
			foreach (var listDef in DefDatabase<ListFilterCategoryDef>.AllDefs)
				foreach (var subDef in listDef.SubFilters)
					rootFilters.Remove(subDef);
		}

		public static IEnumerable<ListFilterSelectableDef> SelectableList =>
			rootFilters.Where(d => (DebugSettings.godMode || !d.devOnly));
	}
}

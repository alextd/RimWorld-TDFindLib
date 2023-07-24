using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace TD_Find_Lib
{
	//Both ThingQueryDef and ThingQueryCategoryDef extend ThingQuerySelectableDef, so they show up in the main list alongside each other in order of the xml
	public abstract class ThingQuerySelectableDef : Def
	{
		public bool devOnly; //only shown in menu when godmode is on
		public bool obsolete; //not shown in menus ; only kept for backcompat.
		public string mod;	//lowercase even though it's capitalized in about.xml
		public bool topLevelSelectable;// Even if it's in a category, show it in main menu too.

		// For modded Queries to use:
		public ThingQueryCategoryDef insertCategory;
		public List<ThingQueryCategoryDef> insertCategories;
	}

	// There are too many query subclasses to globally list them
	// So group them in categories
	// Then only the queries not nested under category will be globally listed,
	// subqueries pop up when the category is selected
	public class ThingQueryCategoryDef : ThingQuerySelectableDef
	{ 
		public List<ThingQuerySelectableDef> subQueries = null;
		public IEnumerable<ThingQuerySelectableDef> SubQueries =>
			subQueries ?? Enumerable.Empty<ThingQuerySelectableDef>();

		public override IEnumerable<string> ConfigErrors()
		{
			if (subQueries.NullOrEmpty() && this != ThingQueryDefOf.Category_Mod)
				yield return "ThingQueryCategoryDef needs to set subQueries";
		}
	}
}

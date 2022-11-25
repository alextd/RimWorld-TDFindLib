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
		public bool devOnly;
	}

	// There are too many query subclasses to globally list them
	// So group them in categories
	// Then only the queries not nested under category will be globally listed,
	// subqueries popup when the category is selected
	public class ThingQueryCategoryDef : ThingQuerySelectableDef
	{ 
		private List<ThingQueryDef> subQueries = null;
		public IEnumerable<ThingQueryDef> SubQueries =>
			subQueries ?? Enumerable.Empty<ThingQueryDef>();

		public override IEnumerable<string> ConfigErrors()
		{
			if (subQueries.NullOrEmpty())
				yield return "ThingQueryCategoryDef needs to set subQueries";
		}
	}
}

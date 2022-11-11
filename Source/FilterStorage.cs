using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;

namespace TD_Find_Lib
{
	public static class FilterStorageUtil
	{
		public static void ButtonOpenSettings(WidgetRow row)
		{
			if (row.ButtonIcon(FindTex.Book))
				Find.WindowStack.Add(new TDFindLibListWindow());
		}

		public static void ButtonChooseLoadFilter(WidgetRow row, Action<FindDescription> onLoad)
		{
			if (row.ButtonIcon(FindTex.Paste))
				ChooseLoadFilter(onLoad);
		}

		public static void ChooseLoadFilter(Action<FindDescription> onLoad)
		{
			List<FloatMenuOption> groupOptions = new();
			foreach (FilterGroup group in Mod.settings.groupedFilters)
			{
				groupOptions.Add(new FloatMenuOption(group.name, () =>
				{
					List<FloatMenuOption> descOptions = new();
					foreach (FindDescription desc in group)
						descOptions.Add(new FloatMenuOption(desc.name, () => onLoad(desc)));

					Find.WindowStack.Add(new FloatMenu(descOptions));
				}));
			}
			Find.WindowStack.Add(new FloatMenu(groupOptions));
			
		}
	}
}

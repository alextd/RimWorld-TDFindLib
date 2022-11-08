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
			List<FloatMenuOption> options = new();
			foreach(FindDescription desc in Mod.settings.savedFilters)
				options.Add(new FloatMenuOption(desc.name, () => onLoad(desc)));
			Find.WindowStack.Add(new FloatMenu(options));
		}
	}
}

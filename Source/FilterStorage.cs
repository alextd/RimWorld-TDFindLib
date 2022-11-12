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

		public static void ChooseExportFilter(FindDescription desc, string source = null)
		{
			List<FloatMenuOption> exportOptions = new();

			//Save to groups
			exportOptions.Add(new FloatMenuOption("Save", () =>
			{
				List<FloatMenuOption> groupOptions = new();
					
				foreach (FilterGroup group in Mod.settings.groupedFilters)
				{
					groupOptions.Add(new FloatMenuOption(group.name, () => group.Add(desc.CloneForSave())));
				}
					
				Find.WindowStack.Add(new FloatMenu(groupOptions));
			}));

			//TODO: Other options to export to!

			//Except don't export to where this comes from
			if(source != null)
				exportOptions.RemoveAll(o => o.Label == source);

			if (exportOptions.Count == 0)
			{
				Verse.Sound.SoundStarter.PlayOneShotOnCamera(SoundDefOf.ClickReject);
				Messages.Message("You have no mods to export to! This is just a library mod. I suggest getting Ctrl-F to start", MessageTypeDefOf.RejectInput);
			}
			else
				Find.WindowStack.Add(new FloatMenu(exportOptions));
		}

		public static void ChooseLoadFilter(Action<FindDescription> onLoad, Map map = null)
		{
			List<FloatMenuOption> groupOptions = new();
			foreach (FilterGroup group in Mod.settings.groupedFilters)
			{
				groupOptions.Add(new FloatMenuOption(group.name, () =>
				{
					List<FloatMenuOption> descOptions = new();
					foreach (FindDescription desc in group)
						descOptions.Add(new FloatMenuOption(desc.name, () => onLoad(desc.CloneForUse(map ?? Find.CurrentMap))));

					Find.WindowStack.Add(new FloatMenu(descOptions));
				}));
			}
			Find.WindowStack.Add(new FloatMenu(groupOptions));
			
		}
	}
}

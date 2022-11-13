using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;
using UnityEngine;

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
			if (row.ButtonIcon(FindTex.Import, "Import from..."))
				ChooseLoadFilter(onLoad);
		}

		public static void ButtonChooseExportFilter(WidgetRow row, FindDescription desc, string source = null, string name = null)
		{
			if (Current.Game == null) return;

			if (row.ButtonIcon(FindTex.Export, "Export to..."))
				ChooseExportFilter(desc, source, name);
		}

		public static void ChooseExportFilter(FindDescription desc, string source = null, string name = null)
		{ 
			List<FloatMenuOption> exportOptions = new();

			//Save to groups
			exportOptions.Add(new FloatMenuOption("Save", () =>
			{
				if (Mod.settings.groupedFilters.Count == 1)
				{
					SaveToGroup(desc, Mod.settings.groupedFilters[0], name);
				}
				else
				{
					List<FloatMenuOption> groupOptions = new();

					foreach (FilterGroup group in Mod.settings.groupedFilters)
					{
						groupOptions.Add(new FloatMenuOption(group.name, () => SaveToGroup(desc, group, name) ));
					}

					Find.WindowStack.Add(new FloatMenu(groupOptions));
				}
			}));


			// Export to Clipboard
			exportOptions.Add(new FloatMenuOption("Copy to clipboard", () =>
			{
				GUIUtility.systemCopyBuffer = Scribe.saver.DebugOutputFor(desc.CloneForSave());
			}));


			// Export to File
			//TODO: Other options to export to!

			//Except don't export to where this comes from
			if (source != null)
				exportOptions.RemoveAll(o => o.Label == source);

			Find.WindowStack.Add(new FloatMenu(exportOptions));
		}

		public static void SaveToGroup(FindDescription desc, FilterGroup group, string name = null)
		{
			if (name != null)
				group.TryAdd(desc.CloneForSave(name));
			else
				Find.WindowStack.Add(new Dialog_Name(desc.name, n => group.TryAdd(desc.CloneForSave(n)), $"Save to {group.name}"));
		}

		public static void ChooseLoadFilter(Action<FindDescription> onLoad, Map map = null)
		{
			if (Mod.settings.groupedFilters.Count == 1)
			{
				LoadFromGroup(Mod.settings.groupedFilters[0], onLoad, map);
			}
			else
			{
				List<FloatMenuOption> groupOptions = new();
				foreach (FilterGroup group in Mod.settings.groupedFilters)
				{
					groupOptions.Add(new FloatMenuOption(group.name, () => LoadFromGroup(group, onLoad, map)));
				}
				Find.WindowStack.Add(new FloatMenu(groupOptions));
			}
		}
		public static void LoadFromGroup(FilterGroup group, Action<FindDescription> onLoad, Map map = null)
		{
			List<FloatMenuOption> descOptions = new();
			foreach (FindDescription desc in group)
				descOptions.Add(new FloatMenuOption(desc.name, () => onLoad(desc.CloneForUse(map ?? Find.CurrentMap))));

			Find.WindowStack.Add(new FloatMenu(descOptions));
		}
	}
}

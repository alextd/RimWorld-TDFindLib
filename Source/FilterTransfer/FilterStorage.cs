using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;
using UnityEngine;
using CloneArgs = TD_Find_Lib.FindDescription.CloneArgs;

namespace TD_Find_Lib
{
	public static class FilterStorageUtil
	{
		public static void ButtonOpenSettings(WidgetRow row)
		{
			if (row.ButtonIcon(FindTex.Book, "Open the library of filters"))
				Find.WindowStack.Add(new TDFindLibListWindow());
		}



		public static void ButtonChooseLoadFilter(WidgetRow row, Action<FindDescription> onLoad, string source = null, CloneArgs cloneArgs = default)
		{
			var options = LoadFilterOptions(onLoad, source, cloneArgs);
			if (options.Count > 0 && row.ButtonIcon(FindTex.Import, "Import filter from..."))
				Find.WindowStack.Add(new FloatMenu(options));
		}
		public static List<FloatMenuOption> LoadFilterOptions(Action<FindDescription> onLoad, string source = null, CloneArgs cloneArgs = default)
		{
			List<FloatMenuOption> loadOptions = new();

			foreach(IFilterProvider provider in FilterTransfer.providers)
			{
				if (provider.Source != null && provider.Source == source) continue;

				switch(provider.ProvideMethod())
				{
					case IFilterProvider.Method.None:
						continue;
					case IFilterProvider.Method.Single:
						loadOptions.Add(new FloatMenuOption(provider.ProvideName, () =>
						{
							onLoad(provider.ProvideSingle().Clone(cloneArgs));
						}));
						continue;
					case IFilterProvider.Method.Selection:
						loadOptions.Add(new FloatMenuOption(provider.ProvideName, () =>
						{
							LoadFromListSubmenu(provider.ProvideSelection(), onLoad, cloneArgs);
						}));
						continue;
					case IFilterProvider.Method.Grouping:
						loadOptions.Add(new FloatMenuOption(provider.ProvideName, () =>
						{
							List<FloatMenuOption> submenuOptions = new();

							foreach (FilterGroup group in provider.ProvideGrouping())
							{
								submenuOptions.Add(new FloatMenuOption("+ " + group.name, () => LoadFromListSubmenu(group, onLoad, cloneArgs)));
							}

							Find.WindowStack.Add(new FloatMenu(submenuOptions));
						}));
						continue;
					//TODO no way we want 3 nested sublists right?
				}
			}

			return loadOptions;
		}

		public static void LoadFromListSubmenu(List<FindDescription> descs, Action<FindDescription> onLoad, CloneArgs cloneArgs = default)
		{
			List<FloatMenuOption> descOptions = new();
			foreach (FindDescription desc in descs)
				descOptions.Add(new FloatMenuOption(desc.name, () => onLoad(desc.Clone(cloneArgs))));

			Find.WindowStack.Add(new FloatMenu(descOptions));
		}



		public static void ButtonChooseExportFilter(WidgetRow row, FindDescription desc, string source = null)
		{
			if (row.ButtonIcon(FindTex.Export, "Export filter to..."))
				ChooseExportFilter(desc, source);
		}

		public static void ChooseExportFilter(FindDescription desc, string source = null)
		{
			List<FloatMenuOption> exportOptions = new();

			foreach(IFilterReceiver receiver in FilterTransfer.receivers)
			{
				if (receiver.Source != null && receiver.Source == source) continue;

				exportOptions.Add(new FloatMenuOption(receiver.ReceiveName, () => receiver.Receive(desc)));
			}

			Find.WindowStack.Add(new FloatMenu(exportOptions));
		}



		public static void ButtonChooseLoadFilterGroup(WidgetRow row, Action<FilterGroup> onLoad, string source = null)
		{
			var options = LoadFilterGroupOptions(onLoad, source);
			if (options.Count > 0 && row.ButtonIcon(FindTex.ImportGroup, "Import group from..."))
				Find.WindowStack.Add(new FloatMenu(options));
		}
		public static List<FloatMenuOption> LoadFilterGroupOptions(Action<FilterGroup> onLoad, string source, CloneArgs cloneArgs = default)
		{
			List<FloatMenuOption> importOptions = new();

			//Load from saved groups
			//todo: in submenu "Load"
			/*
			foreach (FilterGroup group in Mod.settings.groupedFilters)
			{
				importOptions.Add(new FloatMenuOption(group.name, () => onLoad(group)));
			}
			*/


			//Load from clipboard
			string clipboard = GUIUtility.systemCopyBuffer;
			if (ScribeXmlFromString.IsValid<FilterGroup>(clipboard))
				importOptions.Add(new FloatMenuOption("Paste from clipboard", () =>
				{
					FilterGroup group = ScribeXmlFromString.LoadFromString<FilterGroup>(clipboard, null, null);
					onLoad(group.Clone(cloneArgs));
				}));



			//Except don't export to where this comes from
			importOptions.RemoveAll(o => o.Label == source);

			return importOptions;
		}



		public static void ButtonChooseExportFilterGroup(WidgetRow row, FilterGroup desc, string source = null)
		{
			if (row.ButtonIcon(FindTex.ExportGroup, "Export group to..."))
				ChooseExportFilterGroup(desc, source);
		}

		public static void ChooseExportFilterGroup(FilterGroup group, string source = null)
		{
			List<FloatMenuOption> exportOptions = new();

			foreach (IFilterGroupReceiver receiver in FilterTransfer.groupReceivers)
			{
				if (receiver.Source != null && receiver.Source == source) continue;

				exportOptions.Add(new FloatMenuOption(receiver.ReceiveName, () => receiver.Receive(group)));
			}

			Find.WindowStack.Add(new FloatMenu(exportOptions));
		}
	}
}

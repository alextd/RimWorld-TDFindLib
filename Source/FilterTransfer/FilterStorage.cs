using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;
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



		public static void ButtonChooseImportFilter(WidgetRow row, Action<FindDescription> handler, string source = null, CloneArgs cloneArgs = default)
		{
			var options = ImportFilterOptions(handler, source, cloneArgs);
			if (options.Count > 0 && row.ButtonIcon(FindTex.Import, "Import filter from..."))
				Find.WindowStack.Add(new FloatMenu(options));
		}
		public static List<FloatMenuOption> ImportFilterOptions(Action<FindDescription> handler, string source = null, CloneArgs cloneArgs = default)
		{
			List<FloatMenuOption> importOptions = new();

			foreach(IFilterProvider provider in FilterTransfer.providers)
			{
				if (provider.Source != null && provider.Source == source) continue;

				switch(provider.ProvideMethod())
				{
					case IFilterProvider.Method.None:
						continue;
					case IFilterProvider.Method.Single:
						importOptions.Add(new FloatMenuOption(provider.ProvideName, () =>
						{
							handler(provider.ProvideSingle().Clone(cloneArgs));
						}));
						continue;
					case IFilterProvider.Method.Selection:
						importOptions.Add(new FloatMenuOption(provider.ProvideName, () =>
						{
							ImportFromListSubmenu(provider.ProvideSelection(), handler, cloneArgs);
						}));
						continue;
					case IFilterProvider.Method.Grouping:
						importOptions.Add(new FloatMenuOption(provider.ProvideName, () =>
						{
							List<FloatMenuOption> submenuOptions = new();

							foreach (FilterGroup group in provider.ProvideGrouping())
							{
								submenuOptions.Add(new FloatMenuOption("+ " + group.name, () => ImportFromListSubmenu(group, handler, cloneArgs)));
							}

							Find.WindowStack.Add(new FloatMenu(submenuOptions));
						}));
						continue;
					//TODO no way we want 3 nested sublists right?
				}
			}

			return importOptions;
		}

		public static void ImportFromListSubmenu(List<FindDescription> descs, Action<FindDescription> handler, CloneArgs cloneArgs = default)
		{
			List<FloatMenuOption> descOptions = new();
			foreach (FindDescription desc in descs)
				descOptions.Add(new FloatMenuOption(desc.name, () => handler(desc.Clone(cloneArgs))));

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

				exportOptions.Add(new FloatMenuOption(receiver.ReceiveName, () => receiver.Receive(desc.Clone(receiver.CloneArgs))));
			}

			Find.WindowStack.Add(new FloatMenu(exportOptions));
		}



		// Basically a copy of ButtonChooseImportFilter, but accepting FilterGroup instead of FindDescription.
		// Single filters are not accepted, and one less submenu is needed to get at options 
		// handler should set parent and siblings, or just extract each item from the list.
		public static void ButtonChooseImportFilterGroup(WidgetRow row, Action<FilterGroup> handler, string source = null)
		{
			var options = ImportFilterGroupOptions(handler, source);
			if (options.Count > 0 && row.ButtonIcon(FindTex.ImportGroup, "Import group from..."))
				Find.WindowStack.Add(new FloatMenu(options));
		}
		public static List<FloatMenuOption> ImportFilterGroupOptions(Action<FilterGroup> handler, string source, CloneArgs cloneArgs = default)
		{
			List<FloatMenuOption> importOptions = new();

			foreach (IFilterProvider provider in FilterTransfer.providers)
			{
				if (provider.Source != null && provider.Source == source) continue;

				switch (provider.ProvideMethod())
				{
					case IFilterProvider.Method.None:
					case IFilterProvider.Method.Single:
						continue;
					case IFilterProvider.Method.Selection:
						importOptions.Add(new FloatMenuOption(provider.ProvideName, () =>
						{
							handler(provider.ProvideSelection().Clone(cloneArgs));
						}));
						continue;
					case IFilterProvider.Method.Grouping:
						importOptions.Add(new FloatMenuOption(provider.ProvideName, () =>
						{
							List<FloatMenuOption> submenuOptions = new();

							foreach (FilterGroup group in provider.ProvideGrouping())
							{
								submenuOptions.Add(new FloatMenuOption("+ " + group.name, () =>
								{
									handler(provider.ProvideSelection().Clone(cloneArgs));
								}));
							}

							Find.WindowStack.Add(new FloatMenu(submenuOptions));
						}));
						continue;
						//TODO no way we want 3 nested sublists right?
				}
			}

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

				exportOptions.Add(new FloatMenuOption(receiver.ReceiveName, () => receiver.Receive(group.Clone(receiver.CloneArgs))));
			}

			Find.WindowStack.Add(new FloatMenu(exportOptions));
		}
	}
}

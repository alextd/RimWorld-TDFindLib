using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using RimWorld;
using CloneArgs = TD_Find_Lib.QuerySearch.CloneArgs;

namespace TD_Find_Lib
{
	public static class SearchStorage
	{
		public static void ButtonOpenLibrary(WidgetRow row)
		{
			if (row.ButtonIcon(FindTex.Book, "TD.OpenTheLibraryOfSearches".Translate()))
				OpenLibrary();
		}

		public static void ButtonOpenLibrary(Rect rect)
		{
			if (Widgets.ButtonImage(rect, FindTex.Book))
				OpenLibrary();

			TooltipHandler.TipRegion(rect, "TD.OpenTheLibraryOfSearches".Translate());
		}

		public static void OpenLibrary()
		{
			Find.WindowStack.Add(new GroupLibraryWindow(Mod.settings));
		}



		public static void ButtonChooseImportSearch(WidgetRow row, Action<QuerySearch> handler, string source = null, CloneArgs cloneArgs = default)
		{
			var options = ImportSearchOptions(handler, source, cloneArgs);
			if (options.Count > 0 && row.ButtonIcon(FindTex.Import, "TD.ImportSearchFrom".Translate()))
				Find.WindowStack.Add(new FloatMenu(options));
		}
		public static List<FloatMenuOption> ImportSearchOptions(Action<QuerySearch> handler, string source = null, CloneArgs cloneArgs = default)
		{
			List<FloatMenuOption> importOptions = new();

			foreach(ISearchProvider provider in SearchTransfer.providers)
			{
				if (provider.Source != null && provider.Source == source) continue;

				switch(provider.ProvideMethod())
				{
					case ISearchProvider.Method.None:
						continue;
					case ISearchProvider.Method.Single:
						importOptions.Add(new FloatMenuOption(provider.ProvideName, () =>
						{
							handler(provider.ProvideSingle().Clone(cloneArgs));
						}));
						continue;
					case ISearchProvider.Method.Selection:
						importOptions.Add(new FloatMenuOption(provider.ProvideName, () =>
						{
							ImportFromListSubmenu(provider.ProvideSelection(), handler, cloneArgs);
						}));
						continue;
					case ISearchProvider.Method.Grouping:
						importOptions.Add(new FloatMenuOption(provider.ProvideName, () =>
						{
							List<FloatMenuOption> submenuOptions = new();

							foreach (SearchGroup group in provider.ProvideGrouping())
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

		public static void ImportFromListSubmenu(List<QuerySearch> searches, Action<QuerySearch> handler, CloneArgs cloneArgs = default)
		{
			List<FloatMenuOption> searchOptions = new();
			foreach (QuerySearch search in searches)
				searchOptions.Add(new FloatMenuOption(search.name, () => handler(search.Clone(cloneArgs))));

			Find.WindowStack.Add(new FloatMenu(searchOptions));
		}



		public static void ButtonChooseExportSearch(WidgetRow row, QuerySearch search, string source = null)
		{
			if (row.ButtonIcon(FindTex.Export, "TD.ExportSearchTo".Translate()))
				ChooseExportSearch(search, source);
		}

		public static void ChooseExportSearch(QuerySearch search, string source = null)
		{
			List<FloatMenuOption> exportOptions = new();

			foreach(ISearchReceiver receiver in SearchTransfer.receivers)
			{
				if (receiver.Source != null && receiver.Source == source) continue;
				if (!receiver.CanReceive()) continue;

				exportOptions.Add(new FloatMenuOption(receiver.ReceiveName, () => receiver.Receive(search.Clone(receiver.CloneArgs))));
			}

			Find.WindowStack.Add(new FloatMenu(exportOptions));
		}



		// Basically a copy of ButtonChooseImportSearch, but accepting SearchGroup instead of QuerySearch.
		// Single searches are not accepted, and one less submenu is needed to get at options 
		// handler should set parent and siblings, or just extract each item from the list.
		public static void ButtonChooseImportSearchGroup(WidgetRow row, Action<SearchGroup> handler, string source = null)
		{
			var options = ImportSearchGroupOptions(handler, source);
			if (options.Count > 0 && row.ButtonIcon(FindTex.ImportGroup, "TD.ImportGroupFrom".Translate()))
				Find.WindowStack.Add(new FloatMenu(options));
		}
		public static List<FloatMenuOption> ImportSearchGroupOptions(Action<SearchGroup> handler, string source, CloneArgs cloneArgs = default)
		{
			List<FloatMenuOption> importOptions = new();

			foreach (ISearchProvider provider in SearchTransfer.providers)
			{
				if (provider.Source != null && provider.Source == source) continue;

				switch (provider.ProvideMethod())
				{
					case ISearchProvider.Method.None:
					case ISearchProvider.Method.Single:
						continue;
					case ISearchProvider.Method.Selection:
						importOptions.Add(new FloatMenuOption(provider.ProvideName, () =>
						{
							handler(provider.ProvideSelection().Clone(cloneArgs));
						}));
						continue;
					case ISearchProvider.Method.Grouping:
						importOptions.Add(new FloatMenuOption(provider.ProvideName, () =>
						{
							List<FloatMenuOption> submenuOptions = new();

							foreach (SearchGroup group in provider.ProvideGrouping())
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



		public static void ButtonChooseExportSearchGroup(WidgetRow row, SearchGroup search, string source = null)
		{
			if (row.ButtonIcon(FindTex.ExportGroup, "TD.ExportGroupTo".Translate()))
				ChooseExportSearchGroup(search, source);
		}

		public static void ChooseExportSearchGroup(SearchGroup group, string source = null)
		{
			List<FloatMenuOption> exportOptions = new();

			foreach (ISearchGroupReceiver receiver in SearchTransfer.groupReceivers)
			{
				if (receiver.Source != null && receiver.Source == source) continue;
				if (!receiver.CanReceive()) continue;

				exportOptions.Add(new FloatMenuOption(receiver.ReceiveName, () => receiver.Receive(group.Clone(receiver.CloneArgs))));
			}

			Find.WindowStack.Add(new FloatMenu(exportOptions));
		}
	}
}

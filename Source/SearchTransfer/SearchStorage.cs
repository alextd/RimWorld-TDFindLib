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
			if (rect.width > rect.height)
				rect.width = rect.height;
			else
				rect.height = rect.width;

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
			{
				if (options.Count == 1)
					options[0].action();
				else
					Find.WindowStack.Add(new FloatMenu(options));
			}
		}
		public static List<FloatMenuOption> ImportSearchOptions(Action<QuerySearch> handler, string source = null, CloneArgs cloneArgs = default)
		{
			List<FloatMenuOption> importOptions = new();

			foreach (ISearchProvider provider in SearchTransfer.providers)
			{
				if (SourceMatch(provider.Source, source)) continue;

				switch (provider.ProvideMethod())
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
							ImportFromListSubmenu(provider.ProvideGroup(), handler, cloneArgs);
						}));
						continue;
					case ISearchProvider.Method.Grouping:
						importOptions.Add(new FloatMenuOption(provider.ProvideName, () =>
						{
							List<FloatMenuOption> submenuOptions = new();

							foreach (SearchGroup group in provider.ProvideLibrary())
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

		public static void ImportFromListSubmenu(SearchGroup searches, Action<QuerySearch> handler, CloneArgs cloneArgs = default)
		{
			List<FloatMenuOption> searchOptions = new();
			foreach (QuerySearch search in searches)
				searchOptions.Add(new FloatMenuOption(search.name, () => handler(search.Clone(cloneArgs))));

			Find.WindowStack.Add(new FloatMenu(searchOptions));
		}



		public static void ButtonChooseExportSearch(WidgetRow row, QuerySearch search, string source = null)
		{
			if (row.ButtonIcon(FindTex.Export, "TD.ExportSearchTo".Translate()))
				Find.WindowStack.Add(new FloatMenu(ExportSearchOptions(search, source)));
		}

		public static List<FloatMenuOption> ExportSearchOptions(QuerySearch search, string source = null)
		{
			List<FloatMenuOption> exportOptions = new();

			foreach (ISearchReceiver receiver in SearchTransfer.receivers)
			{
				if (SourceMatch(receiver.Source, source)) continue;
				if (!receiver.CanReceive()) continue;

				exportOptions.Add(new FloatMenuOption(receiver.ReceiveName, () => receiver.Receive(search.Clone(receiver.CloneArgs))));
			}

			return exportOptions;
		}



		// Basically a copy of ButtonChooseImportSearch, but accepting SearchGroup instead of QuerySearch.
		// Single searches are not accepted, and one less submenu is needed to get at options.
		// handler should set parent and siblings, or just extract each item from the list.
		public static void ButtonChooseImportSearchGroup(WidgetRow row, Action<SearchGroup> handler, string source = null, CloneArgs cloneArgs = default)
		{
			var options = ImportSearchGroupOptions(handler, source, cloneArgs);
			if (options.Count > 0 && row.ButtonIcon(FindTex.ImportGroup, "TD.ImportGroupFrom".Translate()))
				Find.WindowStack.Add(new FloatMenu(options));
		}
		public static List<FloatMenuOption> ImportSearchGroupOptions(Action<SearchGroup> handler, string source, CloneArgs cloneArgs = default)
		{
			List<FloatMenuOption> importOptions = new();

			foreach (ISearchProvider provider in SearchTransfer.providers)
			{
				if (SourceMatch(provider.Source, source)) continue;

				switch (provider.ProvideMethod())
				{
					case ISearchProvider.Method.None:
					case ISearchProvider.Method.Single:
						continue;
					case ISearchProvider.Method.Selection:
						importOptions.Add(new FloatMenuOption(provider.ProvideName, () =>
						{
							handler(provider.ProvideGroup().Clone(cloneArgs));
						}));
						continue;
					case ISearchProvider.Method.Grouping:
						importOptions.Add(new FloatMenuOption(provider.ProvideName, () =>
						{
							List<FloatMenuOption> submenuOptions = new();

							foreach (SearchGroup group in provider.ProvideLibrary())
							{
								submenuOptions.Add(new FloatMenuOption(group.name, () =>
								{
									handler(provider.ProvideGroup().Clone(cloneArgs));
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
				Find.WindowStack.Add(new FloatMenu(ExportSearchGroupOptions(search, source)));
			;
		}

		public static List<FloatMenuOption> ExportSearchGroupOptions(SearchGroup group, string source = null)
		{
			List<FloatMenuOption> exportOptions = new();

			foreach (ISearchGroupReceiver receiver in SearchTransfer.groupReceivers)
			{
				if (SourceMatch(receiver.Source, source)) continue;
				if (!receiver.CanReceive()) continue;

				exportOptions.Add(new FloatMenuOption(receiver.ReceiveName, () => receiver.Receive(group.Clone(receiver.CloneArgs))));
			}

			return exportOptions;
		}

		public static bool SourceMatch(string source1, string source2) =>
			source1 != null && source2 != null &&
			source1.Split(',').Intersect(source2.Split(',')).Count() > 0;
	}
}

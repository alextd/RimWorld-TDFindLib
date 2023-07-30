using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using RimWorld;

namespace TD_Find_Lib
{
	public class Settings : ModSettings, ISearchReceiver, ISearchGroupReceiver, ISearchProvider, ISearchStorageParent
	{
		private bool onlyAvailable = true;
		public bool OnlyAvailable => onlyAvailable != Event.current.shift && Current.Game != null;

		public static string defaultGroupName = "TD.SavedSearches".Translate();

		//Don't touch my searches
		internal List<SearchGroup> searchGroups;
		public Settings()
		{
			SanityCheck();
			SearchTransfer.Register(this);
		}

		//ISearchStorageParent stuff
		public void NotifyChanged() => Write(); //Write() in parent class
		public List<SearchGroup> Children => searchGroups;
		public void Add(SearchGroup group, bool refresh = true)
		{
			Children.Add(group);
			group.parent = this;

			if(refresh)
				Find.WindowStack?.WindowOfType<GroupLibraryWindow>()?.SetupDrawers();
		}

		public void ReorderGroup(int from, int to)
		{
			var group = searchGroups[from];
			if (Event.current.control)
			{
				searchGroups.Insert(to, group.Clone(QuerySearch.CloneArgs.save, group.name + "TD.CopyNameSuffix".Translate(), this));
			}
			else
			{
				searchGroups.RemoveAt(from);
				searchGroups.Insert(from < to ? to - 1 : to, group);
			}
		}

		internal void SanityCheck()
		{
			if (searchGroups == null || searchGroups.Count == 0)
			{
				searchGroups = new();
				Add(new SearchGroup(defaultGroupName, null));
			}
		}

		public void DoWindowContents(Rect inRect)
		{
			Listing_StandardIndent listing = new();
			listing.Begin(inRect);

			//Global Options
			listing.Header("TD.Settings".Translate());

			listing.CheckboxLabeled(
			"TD.OnlyShowQueryOptionsForAvailableThings".Translate(),
			ref onlyAvailable,
			"TD.ForExampleDontShowTheOptionMadeFromPlasteelIfNothingIsMadeFromPlasteel".Translate());

			listing.Gap();

			if(listing.ButtonTextLabeled("TD.ViewQuerySearchLibrary".Translate(), "TD.View".Translate()))
			{
				//Ah gee this triggers settings.Write but that's no real problem
				Find.WindowStack.WindowOfType<Dialog_ModSettings>().Close();
				Find.WindowStack.WindowOfType<Dialog_Options>().Close();

				Find.WindowStack.Add(new GroupLibraryWindow(this));
			}

			listing.End();
		}


		public bool firstUse = true;
		public bool warnedAnyNull = true;
		public bool warnedModdedFilterLibrary = false;
		public override void ExposeData()
		{
			if(Scribe.mode == LoadSaveMode.Saving && !warnedModdedFilterLibrary)
			{
				if (searchGroups.Any(sg => sg.Any(qs => qs.Children.Any(tq => ThingQueryMaker.moddedQueries.Contains(tq.def)))))
				{
					//Popup warning.
					//TODO: Save to separate file.

					Dialog_MessageBox dialog_MessageBox = new Dialog_MessageBox("");
					dialog_MessageBox.image = ContentFinder<Texture2D>.Get("TDbadtime");
					Find.WindowStack.Add(dialog_MessageBox);

					warnedModdedFilterLibrary = true;
				}
			}
			Scribe_Values.Look(ref onlyAvailable, "onlyAvailable", true);
			Scribe_Values.Look(ref firstUse, "firstUse", false);
			Scribe_Values.Look(ref warnedAnyNull, "warnedAnyNull", false);
			Scribe_Values.Look(ref warnedModdedFilterLibrary, "warnedModdedFilterLibrary", false);

			Scribe_Collections.Look(ref searchGroups, "searchGroups", LookMode.Deep, "??Group Name??", this);
			
			SanityCheck();
		}



		// SearchTransfer business
		public static string StorageTransferTag = "Storage";//notranslate
		public string Source => StorageTransferTag;
		public string ReceiveName => "TD.SaveSearches".Translate();
		public string ProvideName => "TD.LoadSearches".Translate();


		// ISearchReceiver things
		public QuerySearch.CloneArgs CloneArgs => default; //save
		public bool CanReceive() => true;

		public void Receive(QuerySearch search)
		{
			//Save to groups

			//TODO: generalize this in SearchStorage if we think many Receivers are going to want to specify which group to receive?
			List<FloatMenuOption> submenuOptions = new();

			foreach (SearchGroup group in searchGroups)
			{
				submenuOptions.Add(new FloatMenuOption(group.name, () => SaveToGroup(search, group)));
			}

			submenuOptions.Add(new FloatMenuOption("TD.AddNewGroup".Translate(), () =>
			{
				Find.WindowStack.Add(new Dialog_Name("TD.NewGroup".Translate(), n =>
				{
					var group = new SearchGroup(n, this);
					Add(group);

					SaveToGroup(search, group);
				},
				"TD.NameForNewGroup".Translate(),
				n => Children.Any(f => f.name == n)));
			}));

			Find.WindowStack.Add(new FloatMenu(submenuOptions));
		}

		public void Receive(SearchGroup newGroup)
		{
			List<FloatMenuOption> submenuOptions = new();

			foreach (SearchGroup group in searchGroups)
			{
				submenuOptions.Add(new FloatMenuOption(group.name, () => group.AddRange(newGroup)));
			}

			submenuOptions.Add(new FloatMenuOption("TD.AddNewGroup".Translate(), () =>
			{
				Find.WindowStack.Add(new Dialog_Name("TD.NewGroup".Translate(), n =>
				{
					newGroup.name = n;

					Add(newGroup);
				},
				"TD.NameForNewGroup".Translate(),
				n => Children.Any(f => f.name == n)));
			}));

			Find.WindowStack.Add(new FloatMenu(submenuOptions));
		}

		public static void SaveToGroup(QuerySearch search, SearchGroup group)
		{
			Find.WindowStack.Add(new Dialog_Name(search.name, n => { search.name = n; group.TryAdd(search); }, "TD.SaveTo0".Translate(group.name)));
		}


		// ISearchProvider things
		public ISearchProvider.Method ProvideMethod()
		{
			return searchGroups.Count > 1 ? ISearchProvider.Method.Grouping :
				(searchGroups[0].Count == 0 ? ISearchProvider.Method.None : ISearchProvider.Method.Selection);
		}

		public QuerySearch ProvideSingle() => null;
		public SearchGroup ProvideGroup() => searchGroups[0]; //IF there's only one group
		public List<SearchGroup> ProvideLibrary() => searchGroups;
	}
}
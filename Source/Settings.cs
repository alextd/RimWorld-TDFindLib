using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using RimWorld;

namespace TD_Find_Lib
{
	public class Settings : ModSettings, ISearchReceiver, ISearchProvider, ISearchStorageParent
	{
		private bool onlyAvailable = true;
		public bool OnlyAvailable => onlyAvailable != Event.current.shift && Find.CurrentMap != null;

		public static string defaultGroupName = "Saved Searches";

		//Don't touch my searches
		internal List<SearchGroup> searchGroups;
		public Settings()
		{
			SanityCheck();
			SearchTransfer.Register(this);
		}

		//ISearchStorageParent stuff
		//public void Write(); //in parent class
		public List<SearchGroup> Children => searchGroups;
		public void Add(SearchGroup group)
		{
			Children.Add(group);
			group.parent = this;
		}
		public void ReorderGroup(int from, int to)
		{
			var group = searchGroups[from];
			if (Event.current.control)
			{
				searchGroups.Insert(to, group.Clone(QuerySearch.CloneArgs.save, group.name + " (Copy)", this));
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
			listing.Header("Settings:");

			listing.CheckboxLabeled(
			"TD.OnlyShowQueryOptionsForAvailableThings".Translate(),
			ref onlyAvailable,
			"TD.ForExampleDontShowTheOptionMadeFromPlasteelIfNothingIsMadeFromPlasteel".Translate());

			listing.Gap();

			if(listing.ButtonTextLabeled("View all Find definitions", "View"))
			{
				//Ah gee this triggers settings.Write but that's no real problem
				Find.WindowStack.WindowOfType<Dialog_ModSettings>().Close();
				Find.WindowStack.WindowOfType<Dialog_Options>().Close();

				Find.WindowStack.Add(new GroupLibraryWindow(this));
			}

			listing.End();
		}


		public override void ExposeData()
		{
			Scribe_Values.Look(ref onlyAvailable, "onlyAvailable", true);

			Scribe_Collections.Look(ref searchGroups, "searchGroups", LookMode.Deep, "??Group Name??", this);
			
			SanityCheck();
		}


		// SearchTransfer business
		public string Source => "Storage";
		public string ReceiveName => "Save";
		public string ProvideName => "Load";


		// ISearchReceiver things
		public QuerySearch.CloneArgs CloneArgs => default; //save
		public bool CanReceive() => true;

		public void Receive(QuerySearch search)
		{
			//Save to groups
			if (searchGroups.Count == 1)
			{
				// Only one group? skip this submenu
				SaveToGroup(search, searchGroups[0]);
			}
			else
			{
				//TODO: generalize this in SearchStorage if we think many Receivers are going to want to specify which group to receive?
				List<FloatMenuOption> submenuOptions = new();

				foreach (SearchGroup group in searchGroups)
				{
					submenuOptions.Add(new FloatMenuOption("+ " + group.name, () => SaveToGroup(search, group)));
				}

				Find.WindowStack.Add(new FloatMenu(submenuOptions));
			}
		}

		public static void SaveToGroup(QuerySearch search, SearchGroup group)
		{
			Find.WindowStack.Add(new Dialog_Name(search.name, n => { search.name = n; group.TryAdd(search); }, $"Save to {group.name}"));
		}


		// ISearchProvider things
		public ISearchProvider.Method ProvideMethod()
		{
			return searchGroups.Count > 1 ? ISearchProvider.Method.Grouping :
				(searchGroups[0].Count == 0 ? ISearchProvider.Method.None : ISearchProvider.Method.Selection);
		}

		public QuerySearch ProvideSingle() => null;
		public SearchGroup ProvideSelection() => searchGroups[0];
		public List<SearchGroup> ProvideGrouping() => searchGroups;
	}
}
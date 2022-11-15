using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using RimWorld;

namespace TD_Find_Lib
{
	public class Settings : ModSettings, IFilterReceiver
	{
		private bool onlyAvailable = true;
		public bool OnlyAvailable => onlyAvailable != Event.current.shift && Find.CurrentMap != null;

		public static string defaultFiltersName = "Saved Filters";

		//Don't touch my filters
		internal List<FilterGroup> groupedFilters;
		public Settings()
		{
			SanityCheck();
			FilterTransfer.Register(this);
		}

		internal void SanityCheck()
		{
			if (groupedFilters == null || groupedFilters.Count == 0)
			{
				groupedFilters = new();
				groupedFilters.Add(new FilterGroup(defaultFiltersName, groupedFilters));
			}
		}

		public void DoWindowContents(Rect inRect)
		{
			Listing_StandardIndent listing = new();
			listing.Begin(inRect);

			//Global Options
			listing.Header("Settings:");

			listing.CheckboxLabeled(
			"TD.OnlyShowFilterOptionsForAvailableThings".Translate(),
			ref onlyAvailable,
			"TD.ForExampleDontShowTheOptionMadeFromPlasteelIfNothingIsMadeFromPlasteel".Translate());

			listing.Gap();

			if(listing.ButtonTextLabeled("View all Find definitions", "View"))
			{
				//Ah gee this triggers settings.Write but that's no real problem
				Find.WindowStack.WindowOfType<Dialog_ModSettings>().Close();
				Find.WindowStack.WindowOfType<Dialog_Options>().Close();

				Find.WindowStack.Add(new TDFindLibListWindow());
			}

			listing.End();
		}


		public override void ExposeData()
		{
			Scribe_Values.Look(ref onlyAvailable, "onlyAvailable", true);

			// Can't pass ctorArgs 'groupedFilters' here because that's by value, and ref groupedFilters gets reset inside.
			Scribe_Collections.Look(ref groupedFilters, "groupedFilters", LookMode.Undefined, "??Group Name??", null);

			// Set siblings here instead
			if (Scribe.mode == LoadSaveMode.LoadingVars)
				foreach (var group in groupedFilters)
					group.siblings = groupedFilters;
			
			SanityCheck();
		}


		// IFilterReceiver business
		public string Source => "Storage";  //always used
		public string ReceiveName => "Save";
		public void Receive(FindDescription desc)
		{
			//Save to groups
			if (groupedFilters.Count == 1)
			{
				// Only one group? skip this submenu
				SaveToGroup(desc, groupedFilters[0]);
			}
			else
			{
				List<FloatMenuOption> submenuOptions = new();

				foreach (FilterGroup group in groupedFilters)
				{
					submenuOptions.Add(new FloatMenuOption("+ " + group.name, () => SaveToGroup(desc, group)));
				}

				Find.WindowStack.Add(new FloatMenu(submenuOptions));
			}
		}

		public static void SaveToGroup(FindDescription desc, FilterGroup group, FindDescription.CloneArgs cloneArgs = default)
		{
			if (cloneArgs.newName != null)
				group.TryAdd(desc.Clone(cloneArgs));
			else
				Find.WindowStack.Add(new Dialog_Name(desc.name, n => { cloneArgs.newName = n; group.TryAdd(desc.Clone(cloneArgs)); }, $"Save to {group.name}"));
		}
	}
}
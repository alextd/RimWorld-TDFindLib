using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using RimWorld;

namespace TD_Find_Lib
{
	public class Settings : ModSettings
	{
		private bool onlyAvailable = true;
		public bool OnlyAvailable => onlyAvailable != Event.current.shift;

		internal List<FindDescription> savedFilters = new();

		public IEnumerable<string> SavedNames() => savedFilters.Select(fd => fd.name);

		public bool Has(string name)
		{
			return savedFilters.Any(fd => fd.name == name);
		}

		public void Save(FindDescription desc, bool overwrite = false)
		{
			if (!overwrite && Has(desc.name))
			{
				Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
					"TD.OverwriteSavedFilter".Translate(),
					 () => Save(desc, true)));
			}
			else
			{
				FindDescription newDesc = desc.CloneForSave();
				savedFilters.Add(newDesc);
			}
			Write();
		}

		public FindDescription Load(string name, Map map = null)
		{
			return savedFilters.First(fd => fd.name == name).CloneForUse(map);
		}


		public void DoWindowContents(Rect inRect)
		{
			//Scrolling!
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
				Find.WindowStack.WindowOfType<Dialog_ModSettings>().Close();
				Find.WindowStack.WindowOfType<Dialog_Options>().Close();

				Find.WindowStack.Add(new TDFindLibListWindow());
			}

			listing.End();
		}


		public override void ExposeData()
		{
			Scribe_Collections.Look(ref savedFilters, "savedFilters");
			Scribe_Values.Look(ref onlyAvailable, "onlyAvailable", true);
		}
	}
}
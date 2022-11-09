using System;
using System.Xml;
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
		public bool OnlyAvailable => onlyAvailable != Event.current.shift && Find.CurrentMap != null;

		//Don't touch my filters
		internal FilterGroup savedFilters = new();
		internal List<FilterGroup> groupedFilters = new();

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
			Scribe_Values.Look(ref onlyAvailable, "onlyAvailable", true);
			
			Scribe_Deep.Look(ref savedFilters, "savedFilters");
			Scribe_Collections.Look(ref groupedFilters, "groupedFilters");
		}
	}

	// Trying to save a List<List<Deep>> doesn't work.
	// Need List to be "exposable" on its own.
	public class FilterGroup : List<FindDescription>, IExposable
	{
		public string name;
		public void Reorder(int from, int to)
		{
			var desc = this[from];
			RemoveAt(from);
			Insert(from < to ? to - 1 : to, desc);
		}

		public void ExposeData()
		{
			Scribe_Values.Look(ref name, "name");

			string label = "descs";

			//Watered down Scribe_Collections.
			if (Scribe.EnterNode(label))
			{
				try
				{
					if (Scribe.mode == LoadSaveMode.Saving)
					{
						foreach (FindDescription desc in this)
						{
							FindDescription target = desc;
							Scribe_Deep.Look(ref target, "li");
						}
					}
					else if (Scribe.mode == LoadSaveMode.LoadingVars)
					{
						XmlNode curXmlParent = Scribe.loader.curXmlParent;
						Clear();

						foreach (XmlNode node in curXmlParent.ChildNodes)
							Add(ScribeExtractor.SaveableFromNode<FindDescription>(node, new object[] { }));
					}
				}
				finally
				{
					Scribe.ExitNode();
				}
			}
		}
	}

}
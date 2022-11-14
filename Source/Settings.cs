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

		public static string defaultFiltersName = "Saved Filters";
		//Don't touch my filters
		internal List<FilterGroup> groupedFilters;
		public Settings() => SanityCheck();

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
	}

	// Trying to save a List<List<Deep>> doesn't work.
	// Need List to be "exposable" on its own.
	public class FilterGroup : List<FindDescription>, IExposable
	{
		public string name;
		public List<FilterGroup> siblings;

		public FilterGroup(string name, List<FilterGroup> siblings)
		{
			this.name = name;
			this.siblings = siblings;
		}

		public void ConfirmPaste(FindDescription newDesc, int i)
		{
			// TODO the weird case where you changed the name in the editor, to a name that already exists.
			// Right now it'll have two with same name instead of overwriting that one.
			Action acceptAction = delegate ()
			{
				this[i] = newDesc;
				Mod.settings.Write();
			};
			Action copyAction = delegate ()
			{
				newDesc.name = newDesc.name + " (Copy)";
				Insert(i + 1, newDesc);
				Mod.settings.Write();
			};
			Verse.Find.WindowStack.Add(new Dialog_MessageBox(
				$"Save changes to {newDesc.name}?",
				"Confirm".Translate(), acceptAction,
				"No".Translate(), null,
				"Change Filter",
				true, acceptAction,
				delegate () { }// I dunno who wrote this class but this empty method is required so the window can close with esc because its logic is very different from its base class
			)
			{
				buttonCText = "Save as Copy",
				buttonCAction = copyAction,
			});
		}

		public void TryAdd(FindDescription desc)
		{
			if (this.FindIndex(d => d.name == desc.name) is int index && index != -1)
				ConfirmPaste(desc, index);
			else
			{
				base.Add(desc);
				Mod.settings.Write();
			}
		}

		public void ExposeData()
		{
			Scribe_Values.Look(ref name, "name", Settings.defaultFiltersName);

			string label = "descs";

			//Watered down Scribe_Collections, doing LookMode.Deep on List<FindDescription>
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
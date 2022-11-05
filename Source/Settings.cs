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
		private Dictionary<string, FindDescription> savedFilters = new Dictionary<string, FindDescription>();

		public IEnumerable<string> SavedNames() => savedFilters.Keys;

		public bool Has(string name)
		{
			return savedFilters.ContainsKey(name);
		}

		public void Save(string name, FindDescription desc, bool overwrite = false)
		{
			if (!overwrite && Has(name))
			{
				Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
					"TD.OverwriteSavedFilter".Translate(),
					 () => Save(name, desc, true)));
			}
			else
			{
				desc.name = name;	//Remember for current copy

				FindDescription newDesc = desc.CloneForSave();
				newDesc.name = name;
				savedFilters[name] = newDesc;
			}
			Write();
		}

		public FindDescription Load(string name, Map map = null)
		{
			return savedFilters[name].CloneForUse(map);
		}

		public void Rename(string name, string newName)
		{
			FindDescription desc = savedFilters[name];
			desc.name = newName;
			savedFilters[newName] = desc;
			savedFilters.Remove(name);
		}


		private const float RowHeight = WidgetRow.IconSize + 6;

		private Vector2 scrollPosition = Vector2.zero;
		private float scrollViewHeight;
		public void DoWindowContents(Rect inRect)
		{
			//Scrolling!
			Listing_StandardIndent listing = new();
			Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, scrollViewHeight);
			listing.BeginScrollView(inRect, ref scrollPosition, viewRect);

			listing.Header("Saved Filters:");

			string remove = null;
			foreach ((var name, var desc) in savedFilters)
			{
				Rect rowRect = listing.GetRect(RowHeight);

				WidgetRow row = new WidgetRow(rowRect.x, rowRect.y, UIDirection.RightThenDown, rowRect.width);
				row.Label(name + desc.mapLabel, rowRect.width / 4);

				if (row.ButtonText("Rename".Translate()))
					Find.WindowStack.Add(new Dialog_Name(newName => Rename(name, newName)));

				if (Current.Game != null &&
					row.ButtonText("Load".Translate()))
					Verse.Log.Error("Todo!");// MainTabWindow_List.OpenWith(desc.CloneForUse(Find.CurrentMap), true);

				if (row.ButtonText("Delete".Translate()))
					remove = name;

				bool allMaps = desc.allMaps;
				if (row.CheckboxLabeled("TD.AllMaps".Translate(), ref allMaps))
					desc.allMaps = allMaps;
			}

			listing.Header("Active Filters:");
			listing.Label("todo()");
			// Edit how often they tick.


			listing.EndScrollView(ref scrollViewHeight);

			if (remove != null)
			{
				if (Event.current.shift)
					savedFilters.Remove(remove);
				else
					Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
						"TD.Delete0".Translate(remove), () => savedFilters.Remove(remove)));
			}
		}


		public override void ExposeData()
		{
			Scribe_Collections.Look(ref savedFilters, "savedFilters");
		}
	}
}
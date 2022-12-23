using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using UnityEngine;

namespace TD_Find_Lib
{
	// Tied to settings
	public class ModdedSearchGroup : SearchGroupBase<QuerySearch>
	{
		public string modId;

		public ModdedSearchGroup()
		{
		}

		public ModdedSearchGroup(string modId)
		{
			this.modId = modId;
		}

		public SearchGroup AsSearchGroup(QuerySearch.CloneArgs cloneArgs = default)
		{
			SearchGroup clone = new(modId);
			foreach (QuerySearch search in this)
			{
				//obviously don't set newName in cloneArgs
				clone.Add(search.Clone(cloneArgs));
			}
			return clone;
		}

		public override void Replace(QuerySearch newSearch, int i)
		{
			base.Replace(newSearch, i);
			Mod.settings.NotifyChanged();
		}

		public override void Copy(QuerySearch newSearch, int i)
		{
			base.Copy(newSearch, i);
			Mod.settings.NotifyChanged();
		}

		public override void DoAdd(QuerySearch newSearch)
		{
			base.DoAdd(newSearch);
			Mod.settings.NotifyChanged();
		}
	}

	public class ModdedSearchGroupDrawer : SearchGroupDrawerBase<ModdedSearchGroup, QuerySearch>
	{
		public ModdedSearchGroupDrawer(ModdedSearchGroup l) : base(l)
		{
		}

		public override string Name => list.modId;


		public override void DoReorderSearch(int from, int to)
		{
			if (Event.current.control)
			{
				QuerySearch newSearch = list[from].CloneInactive();
				newSearch.name += "TD.CopyNameSuffix".Translate();
				list.Insert(to, newSearch);
			}
			else
				base.DoReorderSearch(from, to);
		}

		public void TrashThis()
		{
			Mod.settings.moddedSearchGroups.Remove(list.modId);
			Mod.settings.NotifyChanged();
		}

		public void Trash(int i)
		{
			list.RemoveAt(i);
			Mod.settings.NotifyChanged();
		}

		public void PopUpCreateQuerySearch()
		{
			Find.WindowStack.Add(new Dialog_Name("TD.NewSearch".Translate(), n =>
			{
				var search = new QuerySearch() { name = n };
				list.TryAdd(search);
				Find.WindowStack.Add(new SearchEditorWindow(search, Settings.StorageTransferTag, f => Mod.settings.NotifyChanged()));
			},
			"TD.NameForNewSearch".Translate(),
			name => list.Any(s => s.name == name)));
		}



		public override void DrawExtraHeader(Rect headerRect)
		{
			WidgetRow headerRow = new WidgetRow(headerRect.xMax, headerRect.y, UIDirection.LeftThenDown);

			// Delete Group button
			if (headerRow.ButtonIcon(FindTex.Trash))
			{
				if (Event.current.shift)
					TrashThis();
				else
					Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
						"TD.Delete0".Translate(Name), TrashThis, true));
			}

			// Export Group
			SearchStorage.ButtonChooseExportSearchGroup(headerRow, list.AsSearchGroup(), Settings.StorageTransferTag);


			// Import single search
			SearchStorage.ButtonChooseImportSearch(headerRow, list.Add, Settings.StorageTransferTag);


			// Paste Group and merge
			SearchStorage.ButtonChooseImportSearchGroup(headerRow, list.AddRange, Settings.StorageTransferTag);


			// Add new search button
			if (headerRow.ButtonIcon(FindTex.GreyPlus))
				PopUpCreateQuerySearch();
		}
	}
}

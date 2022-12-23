using System;
using System.Xml;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace TD_Find_Lib
{
	public class SearchGroup : SearchGroupBase<QuerySearch>
	{
		public string name;

		public SearchGroup(string name)
		{
			this.name = name;
		}


		public SearchGroup Clone(QuerySearch.CloneArgs cloneArgs, string newName = null)
		{
			SearchGroup clone = new(newName ?? name);
			foreach (QuerySearch search in this)
			{
				//obviously don't set newName in cloneArgs
				clone.Add(search.Clone(cloneArgs));
			}
			return clone;
		}


		public override void ExposeData()
		{
			Scribe_Values.Look(ref name, "name", Settings.defaultGroupName);

			base.ExposeData();
		}
	}

	// Trying to save a List<List<Deep>> doesn't work.
	// Need List to be "exposable" on its own.
	public abstract class SearchGroupBase<T> : List<T>, IExposable where T : IQuerySearch
	{
		public virtual void Replace(T newSearch, int i)
		{
			this[i] = newSearch;
		}
		public virtual void Copy(T newSearch, int i)
		{
			newSearch.Search.name += "TD.CopyNameSuffix".Translate();
			Insert(i + 1, newSearch);
		}
		public virtual void DoAdd(T newSearch)
		{
			base.Add(newSearch);
		}
		public virtual void DoAddRange(IEnumerable<T> newSearches)
		{
			base.AddRange(newSearches);
		}

		public void ConfirmPaste(T newSearch, int i)
		{
			// TODO the weird case where you changed the name in the editor, to a name that already exists.
			// Right now it'll have two with same name instead of overwriting that one.
			Verse.Find.WindowStack.Add(new Dialog_MessageBox(
				"TD.SaveChangesTo0".Translate(newSearch.Search.name),
				"Confirm".Translate(), () => Replace(newSearch, i),
				"No".Translate(), null,
				"TD.OverwriteSearch".Translate(),
				true, () => Replace(newSearch, i),
				delegate () { }// I dunno who wrote this class but this empty method is required so the window can close with esc because its logic is very different from its base class
			)
			{
				buttonCText = "TD.SaveAsCopy".Translate(),
				buttonCAction = () => Copy(newSearch, i),
			});
		}

		public void TryAdd(T search)
		{
			if (this.FindIndex(d => d.Search.name == search.Search.name) is int index && index != -1)
				ConfirmPaste(search, index);
			else
				DoAdd(search);
		}

		public virtual void ExposeData()
		{
			string label = "searches";//notranslate

			//Watered down Scribe_Collections, doing LookMode.Deep on List<QuerySearch>
			if (Scribe.EnterNode(label))
			{
				try
				{
					if (Scribe.mode == LoadSaveMode.Saving)
					{
						foreach (T search in this)
						{
							T target = search;	//It's what vanilla code does /shrug
							Scribe_Deep.Look(ref target, "li");
						}
					}
					else if (Scribe.mode == LoadSaveMode.LoadingVars)
					{
						XmlNode curXmlParent = Scribe.loader.curXmlParent;
						Clear();

						foreach (XmlNode node in curXmlParent.ChildNodes)
							base.Add(ScribeExtractor.SaveableFromNode<T>(node, new object[] { }));

						// Somehow this happened perhaps with modded subclass of ThingQuery
						base.RemoveAll(i => i == null);
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
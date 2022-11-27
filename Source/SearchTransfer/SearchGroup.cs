using System;
using System.Xml;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using CloneArgs = TD_Find_Lib.QuerySearch.CloneArgs;

namespace TD_Find_Lib
{
	public interface ISearchStorageParent
	{
		public void Write();
		public List<SearchGroup> Children { get; }
		public void Add(SearchGroup group);
		public void ReorderGroup(int from, int to);
	}

	// Trying to save a List<List<Deep>> doesn't work.
	// Need List to be "exposable" on its own.
	public class SearchGroup : List<QuerySearch>, IExposable
	{
		public string name;
		public ISearchStorageParent parent;

		public SearchGroup(string name, ISearchStorageParent parent)
		{
			this.name = name;
			this.parent = parent;
		}


		public SearchGroup Clone(CloneArgs cloneArgs, string newName = null, ISearchStorageParent newParent = null)
		{
			SearchGroup clone = new SearchGroup(newName ?? name, newParent);
			foreach (QuerySearch query in this)
			{
				//obviously don't set newName in cloneArgs
				clone.Add(query.Clone(cloneArgs));
			}
			return clone;
		}

		public void ConfirmPaste(QuerySearch newSearch, int i)
		{
			// TODO the weird case where you changed the name in the editor, to a name that already exists.
			// Right now it'll have two with same name instead of overwriting that one.
			Action acceptAction = delegate ()
			{
				this[i] = newSearch;
				parent.Write();
			};
			Action copyAction = delegate ()
			{
				newSearch.name = newSearch.name + "TD.CopyNameSuffix".Translate();
				Insert(i + 1, newSearch);
				parent.Write();
			};
			Verse.Find.WindowStack.Add(new Dialog_MessageBox(
				"TD.SaveChangesTo0".Translate(newSearch.name),
				"Confirm".Translate(), acceptAction,
				"No".Translate(), null,
				"TD.OverwriteSearch".Translate(),
				true, acceptAction,
				delegate () { }// I dunno who wrote this class but this empty method is required so the window can close with esc because its logic is very different from its base class
			)
			{
				buttonCText = "TD.SaveAsCopy".Translate(),
				buttonCAction = copyAction,
			});
		}

		public void TryAdd(QuerySearch search)
		{
			if (this.FindIndex(d => d.name == search.name) is int index && index != -1)
				ConfirmPaste(search, index);
			else
			{
				base.Add(search);
				parent.Write();
			}
		}

		public void ExposeData()
		{
			Scribe_Values.Look(ref name, "name", Settings.defaultGroupName);

			string label = "TD.Searches".Translate();

			//Watered down Scribe_Collections, doing LookMode.Deep on List<QuerySearch>
			if (Scribe.EnterNode(label))
			{
				try
				{
					if (Scribe.mode == LoadSaveMode.Saving)
					{
						foreach (QuerySearch search in this)
						{
							QuerySearch target = search;
							Scribe_Deep.Look(ref target, "li");
						}
					}
					else if (Scribe.mode == LoadSaveMode.LoadingVars)
					{
						XmlNode curXmlParent = Scribe.loader.curXmlParent;
						Clear();

						foreach (XmlNode node in curXmlParent.ChildNodes)
							Add(ScribeExtractor.SaveableFromNode<QuerySearch>(node, new object[] { }));
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

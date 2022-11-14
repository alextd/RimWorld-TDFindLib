using System;
using System.Xml;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using CloneArgs = TD_Find_Lib.FindDescription.CloneArgs;

namespace TD_Find_Lib
{
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


		public FilterGroup Clone(CloneArgs cloneArgs, string newName = null, List<FilterGroup> newSiblings = null)
		{
			FilterGroup clone = new FilterGroup(newName ?? name, newSiblings);
			foreach (FindDescription filter in this)
			{
				//obviously don't set newName in cloneArgs
				clone.Add(filter.Clone(cloneArgs));
			}
			return clone;
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TD_Find_Lib
{
	public interface ILibraryParent
	{
		public void NotifyChanged();
		public List<LibrarySearchGroup> Children { get; }
		public void Add(LibrarySearchGroup group, bool refresh = true);
		public void ReorderGroup(int from, int to);
	}

	public class LibrarySearchGroup : SearchGroup
	{
		public ILibraryParent parent;

		public LibrarySearchGroup(string name, ILibraryParent parent) : base(name)
		{
			this.parent = parent;
		}


		public LibrarySearchGroup Clone(QuerySearch.CloneArgs cloneArgs, string newName = null, ILibraryParent newParent = null)
		{
			LibrarySearchGroup clone = new(newName ?? name, newParent);
			foreach (QuerySearch search in this)
			{
				//obviously don't set newName in cloneArgs
				clone.Add(search.Clone(cloneArgs));
			}
			return clone;
		}


		public static LibrarySearchGroup CloneFrom(SearchGroup group, ILibraryParent parent)
		{

			LibrarySearchGroup clone = new(group.name, parent);
			foreach (QuerySearch search in group)
			{
				//obviously don't set newName in cloneArgs
				clone.Add(search.CloneInactive());
			}
			return clone;
		}


		public override void Replace(QuerySearch newSearch, int i)
		{
			base.Replace(newSearch, i);
			parent.NotifyChanged();
		}

		public override void Copy(QuerySearch newSearch, int i)
		{
			base.Copy(newSearch, i);
			parent.NotifyChanged();
		}

		public override void DoAdd(QuerySearch newSearch)
		{
			base.DoAdd(newSearch);
			parent.NotifyChanged();
		}
	}
}


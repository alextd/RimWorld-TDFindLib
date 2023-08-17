using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Verse;
using RimWorld;
using UnityEngine;

namespace TD_Find_Lib
{
	// The holder of the receivers and providers
	public static class SearchTransfer
	{
		public static List<ISearchReceiver> receivers = new();
		public static List<ISearchGroupReceiver> groupReceivers = new();
		public static List<ISearchLibraryReceiver> libraryReceivers = new();
		public static List<ISearchProvider> providers = new();
		//providers work overtime, providing none/single/list/group-of-lists


		// I would love to auto-create and register all subclasses of ISearchReceiver,
		// but any class could implement it and we can't be sure it's okay to just create a new one (e.g. Settings)
		// Plus, many receivers are invalidated when a game ends.
		// It's not the easiest thing to hook into "on game end"
		// So it's easier to keep a static receiver that checks if a game exists in CanReceive()
		public static void Register(object obj)
		{
			if (obj is ISearchReceiver receiver)
				receivers.Add(receiver);
			if (obj is ISearchGroupReceiver greceiver)
				groupReceivers.Add(greceiver);
			if (obj is ISearchLibraryReceiver lreceiver)
				libraryReceivers.Add(lreceiver);
			if (obj is ISearchProvider provider)
				providers.Add(provider);
		}
		public static void Deregister(object obj)
		{
			receivers.Remove(obj as ISearchReceiver);
			groupReceivers.Remove(obj as ISearchGroupReceiver);
			libraryReceivers.Remove(obj as ISearchLibraryReceiver);
			providers.Remove(obj as ISearchProvider);
		}
	}


	// You can export  to  a receiver
	// You can import from a provider
	public interface ISearchProvider
	{
		public enum Method { None, Single, Group, Library }

		public string Source { get; }
		public string ProvideName { get; }
		public Method ProvideMethod();

		public QuerySearch ProvideSingle();
		public SearchGroup ProvideGroup();
		public List<SearchGroup> ProvideLibrary();
	}

	public interface ISearchReceiver
	{
		public string Source { get; }
		public string ReceiveName { get; }
		public QuerySearch.CloneArgs CloneArgs { get; }
		public bool CanReceive();
		public void Receive(QuerySearch search);
	}

	public interface ISearchGroupReceiver
	{
		public string Source { get; }
		public string ReceiveName { get; }
		public QuerySearch.CloneArgs CloneArgs { get; }
		public bool CanReceive();
		public void Receive(SearchGroup search);
	}

	public interface ISearchLibraryReceiver
	{
		public string Source { get; }
		public string ReceiveName { get; }
		public QuerySearch.CloneArgs CloneArgs { get; }
		public bool CanReceive();
		public void Receive(List<SearchGroup> search);
	}


	[StaticConstructorOnStartup]
	public class ClipboardTransfer : ISearchReceiver, ISearchProvider, ISearchGroupReceiver, ISearchLibraryReceiver
	{
		static ClipboardTransfer()
		{
			SearchTransfer.Register(new ClipboardTransfer());
		}


		public string Source => null;	//always used

		public string ReceiveName => "TD.CopyToClipboard".Translate();
		public string ProvideName => "TD.PasteFromClipboard".Translate();


		public QuerySearch.CloneArgs CloneArgs => default; //save
		public bool CanReceive() => true;

		public void Receive(QuerySearch search)
		{
			GUIUtility.systemCopyBuffer = ScribeXmlFromString.SaveAsString(search);
		}

		public void Receive(SearchGroup group)
		{
			GUIUtility.systemCopyBuffer = ScribeXmlFromString.SaveAsString(group);
		}

		public void Receive(List<SearchGroup> library)
		{
			GUIUtility.systemCopyBuffer = ScribeXmlFromString.SaveListAsString(library);
		}


		public ISearchProvider.Method ProvideMethod()
		{
			string clipboard = GUIUtility.systemCopyBuffer;
			return ScribeXmlFromString.IsValid<QuerySearch>(clipboard) ? ISearchProvider.Method.Single
				: ScribeXmlFromString.IsValid<SearchGroup>(clipboard) ? ISearchProvider.Method.Group
				: ScribeXmlFromString.IsValidList<SearchGroup>(clipboard) ? ISearchProvider.Method.Library
				: ISearchProvider.Method.None;
		}

		public QuerySearch ProvideSingle()
		{
			string clipboard = GUIUtility.systemCopyBuffer;
			return ScribeXmlFromString.LoadFromString<QuerySearch>(clipboard);
		}

		public SearchGroup ProvideGroup()
		{
			string clipboard = GUIUtility.systemCopyBuffer;
			return ScribeXmlFromString.LoadFromString<SearchGroup>(clipboard, null, null);
		}

		public List<SearchGroup> ProvideLibrary()
		{
			string clipboard = GUIUtility.systemCopyBuffer;
			return ScribeXmlFromString.LoadListFromString<SearchGroup>(clipboard, null, null);
		}
	}

	[StaticConstructorOnStartup]
	public class DefaultSearches : ISearchProvider
	{
		static DefaultSearches()
		{
			SearchTransfer.Register(new DefaultSearches());
		}

		public string Source => "=" + Settings.StorageTransferTag; // only used with settings storage

		public string ProvideName => "Sample Searches";

		public ISearchProvider.Method ProvideMethod() => ISearchProvider.Method.Library;
		public QuerySearch ProvideSingle() => null;
		public SearchGroup ProvideGroup() => null;



		private static List<SearchGroup> _library;
		private static List<SearchGroup> Library => _library ??= ScribeXmlFromString.LoadListFromString<SearchGroup>(
				File.ReadAllText(
					GenFile.ResolveCaseInsensitiveFilePath(
						LoadedModManager.GetMod<Mod>().Content.ModMetaData.RootDir.FullName
						+ Path.DirectorySeparatorChar + "About", "DefaultSearches.xml")),
				null, null);

		public static List<SearchGroup> CopyLibrary => Library.Select(g => g.Clone(default)).ToList();


		public List<SearchGroup> ProvideLibrary() => Library;
			
	}
}

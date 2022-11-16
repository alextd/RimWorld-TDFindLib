using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using UnityEngine;

namespace TD_Find_Lib
{
	// The holder of the receivers and providers
	public static class FilterTransfer
	{
		public static List<IFilterReceiver> receivers = new();
		public static List<IFilterGroupReceiver> groupReceivers = new();
		public static List<IFilterProvider> providers = new();
		//providers work overtime, providing none/single/list/group-of-lists

		public static void Register(object obj)
		{
			if (obj is IFilterReceiver receiver)
				receivers.Add(receiver);
			if (obj is IFilterGroupReceiver greceiver)
				groupReceivers.Add(greceiver);
			if (obj is IFilterProvider provider)
				providers.Add(provider);
		}
		public static void Deregister(object obj)
		{
			receivers.Remove(obj as IFilterReceiver);
			groupReceivers.Remove(obj as IFilterGroupReceiver);
			providers.Remove(obj as IFilterProvider);
		}
	}


	// You can export  to  a receiver
	// You can import from a provider
	public interface IFilterProvider
	{
		public enum Method { None, Single, Selection, Grouping }

		public string Source { get; }
		public string ProvideName { get; }
		public Method ProvideMethod();

		public FindDescription ProvideSingle();
		public FilterGroup ProvideSelection();
		public List<FilterGroup> ProvideGrouping();
	}

	public interface IFilterReceiver
	{
		public string Source { get; }
		public string ReceiveName { get; }
		public FindDescription.CloneArgs CloneArgs { get; }
		public void Receive(FindDescription desc);
	}

	public interface IFilterGroupReceiver
	{
		public string Source { get; }
		public string ReceiveName { get; }
		public FindDescription.CloneArgs CloneArgs { get; }
		public void Receive(FilterGroup desc);
	}


	[StaticConstructorOnStartup]
	public class ClipboardTransfer : IFilterReceiver, IFilterProvider, IFilterGroupReceiver
	{
		static ClipboardTransfer()
		{
			FilterTransfer.Register(new ClipboardTransfer());
		}


		public string Source => null;	//always used

		public string ReceiveName => "Copy to clipboard";
		public string ProvideName => "Paste from clipboard";


		public FindDescription.CloneArgs CloneArgs => default; //save

		public void Receive(FindDescription desc)
		{
			GUIUtility.systemCopyBuffer = ScribeXmlFromString.SaveAsString(desc);
		}

		public void Receive(FilterGroup group)
		{
			GUIUtility.systemCopyBuffer = ScribeXmlFromString.SaveAsString(group);
		}


		public IFilterProvider.Method ProvideMethod()
		{
			string clipboard = GUIUtility.systemCopyBuffer;
			return ScribeXmlFromString.IsValid<FindDescription>(clipboard) ? IFilterProvider.Method.Single
				: ScribeXmlFromString.IsValid<FilterGroup>(clipboard) ? IFilterProvider.Method.Selection
				: IFilterProvider.Method.None;
		}

		public FindDescription ProvideSingle()
		{
			string clipboard = GUIUtility.systemCopyBuffer;
			return ScribeXmlFromString.LoadFromString<FindDescription>(clipboard);
		}

		public FilterGroup ProvideSelection()
		{
			string clipboard = GUIUtility.systemCopyBuffer;
			return ScribeXmlFromString.LoadFromString<FilterGroup>(clipboard, null, null);
		}

		public List<FilterGroup> ProvideGrouping() => null;
	}
}

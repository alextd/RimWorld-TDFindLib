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
		public static List<IFilterProvider> providers = new();

		public static void Register(object obj)
		{
			if (obj is IFilterReceiver receiver)
				receivers.Add(receiver);
			if (obj is IFilterProvider provider)
				providers.Add(provider);
		}
		public static void Deregister(object obj)
		{
			receivers.Remove(obj as IFilterReceiver);
			providers.Remove(obj as IFilterProvider);
		}
	}


	// You can export  to  a receiver
	// You can import from a provider
	public interface IFilterReceiver
	{
		public string Source { get; }
		public string ReceiveName { get; }
		public void Receive(FindDescription desc);
	}

	public interface IFilterProvider
	{
		public string Source { get; }
		public string ProvideName { get; }
		public int ProvideCount();
		public FindDescription ProvideSingle();
	}

	//public interface IFilterGroupReceiver
	//public interface IFilterGroupProvider


	[StaticConstructorOnStartup]
	public class ClipboardTransfer : IFilterReceiver, IFilterProvider
	{
		static ClipboardTransfer()
		{
			FilterTransfer.Register(new ClipboardTransfer());
		}


		public string Source => null;	//always used

		public string ReceiveName => "Copy to clipboard";
		public string ProvideName => "Paste from clipboard";

		public void Receive(FindDescription desc)
		{
			GUIUtility.systemCopyBuffer = ScribeXmlFromString.SaveAsString(desc.CloneForSave());
		}


		public FindDescription ProvideSingle()
		{
			string clipboard = GUIUtility.systemCopyBuffer;
			return ScribeXmlFromString.LoadFromString<FindDescription>(clipboard);
		}

		public int ProvideCount()
		{
			string clipboard = GUIUtility.systemCopyBuffer;
			return ScribeXmlFromString.IsValid<FindDescription>(clipboard) ? 1 : 0;
		}
	}
}

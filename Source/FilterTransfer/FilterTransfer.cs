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
		public static void Register(IFilterReceiver receiver) =>
			receivers.Add(receiver);
		public static void Deregister(IFilterReceiver receiver) =>
			receivers.Remove(receiver);
	}


	// You can export  to  a receiver
	// You can import from a provider
	public interface IFilterReceiver
	{
		public string Source { get; }
		public string Name { get; }
		public void Receive(FindDescription desc);
	}

	[StaticConstructorOnStartup]
	public class ClipboardReceiver : IFilterReceiver
	{
		public string Source => null;	//always used
		public string Name => "Copy to clipboard";
		public void Receive(FindDescription desc) =>
			GUIUtility.systemCopyBuffer = ScribeXmlFromString.SaveAsString(desc.CloneForSave());

		static ClipboardReceiver() =>
			FilterTransfer.Register(new ClipboardReceiver());
	}


	//public interface IFilterProvider
	//public interface IFilterGroupReceiver
	//public interface IFilterGroupProvider
}

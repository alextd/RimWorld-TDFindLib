using System;
using System.Xml;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace TD_Find_Lib
{
	public static class ScribeXmlFromString
	{
		public static string SaveAsString(IExposable obj)
		{
			return "<StupidDummyXMLTag>" + Scribe.saver.DebugOutputFor(obj) + "</StupidDummyXMLTag>";
		}

		// (modeled after ReadModSettings)
		public static T LoadFromString<T>(string xmlText) where T : IExposable, new()
		{
			T target = default;
			try
			{
				InitLoadingFromString(xmlText);
				try
				{
					// name "saveable" from ScribeSaver.DebugOutputFor
					// They didn't bother to write a ScribeLoader!
					Scribe_Deep.Look(ref target, "saveable");
				}
				finally
				{
					Scribe.loader.FinalizeLoading();
				}
			}
			catch (Exception ex)
			{
				Verse.Log.Warning($"Caught exception while loading XML string {xmlText}. The exception was: {ex.ToString()}");
				target = default;
			}
			return target;
		}

		//ScribeLoader.InitLoading but with xml string instead of reading a file
		public static void InitLoadingFromString(string xmlText)
		{
			if (Scribe.mode != 0)
			{
				Verse.Log.Error("Called InitLoading() but current mode is " + Scribe.mode);
				Scribe.ForceStop();
			}
			if (Scribe.loader.curParent != null)
			{
				Verse.Log.Error("Current parent is not null in InitLoading");
				Scribe.loader.curParent = null;
			}
			if (Scribe.loader.curPathRelToParent != null)
			{
				Verse.Log.Error("Current path relative to parent is not null in InitLoading");
				Scribe.loader.curPathRelToParent = null;
			}
			try
			{
				XmlDocument xmlDocument = new XmlDocument();
				xmlDocument.LoadXml(xmlText);
				Scribe.loader.curXmlParent = xmlDocument.DocumentElement;
				Scribe.mode = LoadSaveMode.LoadingVars;
			}
			catch (Exception ex)
			{
				Verse.Log.Warning($"Exception while init loading xml string:\n\n{xmlText}\n\n{ex}");
				Scribe.loader.ForceStop();
				throw;
			}
		}
	}
}

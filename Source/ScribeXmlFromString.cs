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
		// Save!
		public static string SaveAsString(IExposable obj)
		{
			string tag = obj.GetType().ToString();
			return $"<{tag}>\n{Scribe.saver.DebugOutputFor(obj)}\n</{tag}>";
		}


		// Validate before you load!
		public static bool IsValid<T>(string xmlText) => xmlText.StartsWith("<"+typeof(T).ToString()+">");


		// Load!
		// (modeled after ReadModSettings)
		public static T LoadFromString<T>(string xmlText, params object[] ctorArgs) where T : IExposable
		{
			T target = default;
			try
			{
				// the first <tag> is just passed over . . . so no need to pass in string tag to LoadFromString.
				InitLoadingFromString(xmlText);
				try
				{
					// name "saveable" from ScribeSaver.DebugOutputFor
					// They didn't bother to write a ScribeLoader.DebugInputFrom!
					Scribe_Deep.Look(ref target, "saveable", ctorArgs);
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
				XmlDocument xmlDocument = new();
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

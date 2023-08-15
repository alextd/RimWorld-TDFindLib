using System;
using System.Xml;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Verse;

namespace TD_Find_Lib
{
	public static class ScribeXmlFromString
	{
		// Save!
		const string ListPrefix = "List_";
		public static string SaveAsString(IExposable obj)
		{
			string tag = obj.GetType().ToString();
			return $"<{tag}>\n{Scribe.saver.DebugOutputFor(obj)}\n</{tag}>";
		}
		public static string SaveListAsString<T>(List<T> obj) where T : IExposable
		{
			string tag = ListPrefix+typeof(T).ToString();
			return $"<{tag}>\n{Scribe.saver.DebugOutputForList(obj)}\n</{tag}>";
		}


		// Validate before you load!
		public static bool IsValid<T>(string xmlText) => xmlText.StartsWith($"<{typeof(T)}>");
		public static bool IsValidList<T>(string xmlText) => xmlText.StartsWith($"<{ListPrefix}{typeof(T)}>");


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

		public static List<T> LoadListFromString<T>(string xmlText, params object[] ctorArgs) where T : IExposable
		{
			List<T> target = default;
			try
			{
				// the first <tag> is just passed over . . . so no need to pass in string tag to LoadFromString.
				InitLoadingFromString(xmlText);
				try
				{
					// name "saveable" from ScribeSaver.DebugOutputFor
					// They didn't bother to write a ScribeLoader.DebugInputFrom!
					Scribe_Collections.Look(ref target, "saveable", default, ctorArgs);
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


		//ScribeLoader.DebugOutputFor but for a list with Scribe_Collections
		public static string DebugOutputForList<T>(this ScribeSaver saver, List<T> saveable)
		{
			if (Scribe.mode != 0)
			{
				Verse.Log.Error("DebugOutput needs current mode to be Inactive");
				return "";
			}
			try
			{
				using StringWriter stringWriter = new StringWriter();
				XmlWriterSettings xmlWriterSettings = new XmlWriterSettings();
				xmlWriterSettings.Indent = true;
				xmlWriterSettings.IndentChars = "  ";
				xmlWriterSettings.OmitXmlDeclaration = true;
				try
				{
					using (saver.writer = XmlWriter.Create(stringWriter, xmlWriterSettings))
					{
						Scribe.mode = LoadSaveMode.Saving;
						saver.savingForDebug = true;
						Scribe_Collections.Look(ref saveable, "saveable");
					}
					return stringWriter.ToString();
				}
				finally
				{
					saver.ForceStop();
				}
			}
			catch (Exception ex)
			{
				Verse.Log.Error("Exception while getting debug output: " + ex);
				saver.ForceStop();
				return "";
			}
		}

	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Verse;

namespace TD_Find_Lib
{
	public static class TranslateEnumEx
	{
		private static Dictionary<object, string> _enumStringCache = new();

		public static string TranslateEnum(this object e)
		{
			if (_enumStringCache.TryGetValue(e, out string tr))
				return tr;

			string translated = DoTranslateEnum(e);
			_enumStringCache[e] = translated;
			return translated;
		}

		private static string DoTranslateEnum(object e)
		{ 
			string type = e.GetType().Name;
			string name = e.ToString();
			List<string> flagNames = new();
			foreach (string flagName in name.Split(new string[] { ", " }, StringSplitOptions.None))
			{
				string key = $"TD.{type}.{flagName}";//notranslate

				TaggedString result;
				if (key.TryTranslate(out result))
				{
					flagNames.Add(result);
				}
				// fallback on translation in vanilla. Sometimes I plan for this, it gets translations so that's nice.
				else if (name.TryTranslate(out result))
				{
					Verse.Log.Warning($"TD here! Enum {e} Translated to {result} because no key ({key}) was found but {name} was a normal translation");
					flagNames.Add(result);
				}
			}
			//return key.Translate(); //And get markings on letters, nah.
			
			if(!flagNames.NullOrEmpty())
			{
				return String.Join(", ", flagNames);
			}
			Verse.Log.Warning($"TD here! Enum ({e}) Translated to \"{name}\" because no translation was found");

			return name;
		}
	}


	public static class LabelByDefName
	{
		private static Dictionary<Def, string> _defLabelCache = new();

		public static string GetLabel(this Def def)
		{
			if (_defLabelCache.TryGetValue(def, out string tr))
				return tr;

			string translated = DoGetLabel(def);
			_defLabelCache[def] = translated;
			return translated;
		}

		private static string DoGetLabel(Def def)
		{
			Log.Message($"GetLabel({def})");
			{
				if (def.LabelCap is TaggedString label && label != TaggedString.empty)
				{
					Log.Message($"def.LabelCap = {label}");
					return label;
				}
			}
			{
				if ($"TD.DefLabel.{def.defName}".TryTranslate(out TaggedString label))
				{
					Log.Message($"TD.DefLabel.{def.defName} => {label}");
					return label;
				}
			}

			// Just stupid add spaces before capital letters.
			Log.Message($"SplitCamelCase() => {def.defName.SplitCamelCase()}");
			return def.defName.SplitCamelCase();
		}

		public static string SplitCamelCase(this string str) =>
			//GenText with added (?<!_) lookbehind for no _ to create "A_String" not "A_ String"
			// this also adds in nbsp, so the resulting string is still grouped as one chunk.
			Regex.Replace(str, "(\\B(?<!_)[A-Z]+?(?=[A-Z][^A-Z])|\\B(?<!_)[A-Z]+?(?=[^A-Z]))", " $1"); 
	}
}

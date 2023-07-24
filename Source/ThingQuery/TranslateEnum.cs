using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
}

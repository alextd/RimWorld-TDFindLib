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
			string key = ("TD." + type + "." + name);

			TaggedString result;
			if (key.TryTranslate(out result))
				return result;
			if (name.TryTranslate(out result))
				return result;
			//return key.Translate(); //And get markings on letters, nah.
			return name;
		}
	}
}
